using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class Server
    {
        #region fields
        
        /// <summary>
        /// Whether the local server has been started or not
        /// </summary>
        public bool IsActive => LocalState == ELocalServerConnectionState.Started;
        
        /// <summary>
        /// Listen endpoint of the local server
        /// </summary>
        public IPEndPoint ServerEndpoint { get; private set; }
        /// <summary>
        /// Name of the local server
        /// </summary>
        /// <remarks>
        /// TODO : synchronise updated set
        /// </remarks>
        public string Servername { get; set; } = "New Server";
        /// <summary>
        /// Max number of connected clients of the local server
        /// </summary>
        public uint MaxNumberOfClients { get; private set; }
        /// <summary>
        /// The current connection state of the local server
        /// </summary>
        public ELocalServerConnectionState LocalState { get; private set; } = ELocalServerConnectionState.Stopped;
        /// <summary>
        /// The clients that are connected to the local server
        /// </summary>
        public ConcurrentDictionary<uint, ClientInformation> ConnectedClients { get; } = new();
        /// <summary>
        /// The number of client connected to the local server
        /// </summary>
        public uint NumberOfConnectedClients => (uint)ConnectedClients.Count;
        
        /// <summary>
        /// Called when the local server's connection state has been updated
        /// </summary>
        public event Action<ELocalServerConnectionState> OnLocalStateUpdated;
        /// <summary>
        /// Called by the local server when a new remote client has been authenticated
        /// </summary>
        public event Action<uint> OnRemoteClientConnected;
        /// <summary>
        /// Called by the local server when a remote client disconnected
        /// </summary>
        public event Action<uint> OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local server when a remote client updated its information
        /// </summary>
        public event Action<uint> OnRemoteClientUpdated;
        
        private readonly ConcurrentDictionary<uint, byte[]> _authenticatingClients = new();

        private readonly NetworkManager _networkManager;
        
        #endregion

        public Server(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _networkManager.OnTransportDisposed += OnTransportDisposed;
            _networkManager.OnServerStateUpdated += OnServerStateUpdated;
            _networkManager.OnConnectionUpdated += OnRemoteConnectionUpdated;
            _networkManager.OnServerReceivedData += OnServerReceivedData;
        }
        
        #region private methods

        private void OnTransportDisposed()
        {
            _authenticatingClients.Clear();
            ConnectedClients.Clear();
            LocalState = ELocalServerConnectionState.Stopped;
        }
        
        private void OnServerStateUpdated(ELocalConnectionState state)
        {
            switch (state)
            {
                case ELocalConnectionState.Starting:
                    _networkManager.Logger?.Log("Server is starting...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Started:
                    ServerEndpoint = _networkManager.Transport.ServerEndpoint;
                    MaxNumberOfClients = _networkManager.Transport.MaxNumberOfClients;
                    _networkManager.Logger?.Log("Server was started", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopping:
                    _networkManager.Logger?.Log("Server is stopping...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopped:
                    ServerEndpoint = null;
                    MaxNumberOfClients = 0;
                    _networkManager.Logger?.Log("Server was stopped", EMessageSeverity.Log);
                    break;
            }
            LocalState = (ELocalServerConnectionState)state;
            OnLocalStateUpdated?.Invoke(LocalState);
        }
        
        private void OnRemoteConnectionUpdated(uint clientID, ERemoteConnectionState state)
        {
            switch (state)
            {
                case ERemoteConnectionState.Connected:
                    HandleRemoteClientConnected(clientID);
                    break;
                case ERemoteConnectionState.Disconnected:
                    HandleRemoteClientDisconnected(clientID);
                    break;
            }
        }

        private void HandleRemoteClientConnected(uint clientID)
        {
            if (ConnectedClients.ContainsKey(clientID))
            {
                _networkManager.Logger?.Log($"An already existing connection was overwritten. Connection for client {clientID} was dropped!");
                _networkManager.Transport?.DisconnectClient(clientID);
                return;
            }
            
            // create and save challenge
            Random rnd = new();
            var challenge = (ulong)(rnd.NextDouble() * ulong.MaxValue);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(challenge));
            _authenticatingClients[clientID] = hashedChallenge;

            // send challenge to client
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(ConnectionChallengePacket.PacketType);
            ConnectionChallengePacket.Write(writer, new(challenge));
            _networkManager.Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }

        private void HandleRemoteClientDisconnected(uint clientID)
        {
            // ignore authenticating or missing client IDs
            if (_authenticatingClients.TryRemove(clientID, out _)) return;
            if (!ConnectedClients.TryRemove(clientID, out _)) return;
            
            OnRemoteClientDisconnected?.Invoke(clientID);
            _networkManager.Logger?.Log($"Server: Remote client {clientID} was disconnected", EMessageSeverity.Log);

            // inform other clients of disconnected client
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket.Write(writer, new(clientID));
            foreach (var id in ConnectedClients.Keys)
                _networkManager.Transport?.SendDataToClient(id, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);

        }
        
        private void OnServerReceivedData(ServerReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, _networkManager.SerialiserSettings);
                var packetType = (EPacketType)reader.ReadByte();
                // Debug.Log($"Server Packet: {packetType} from {data.ClientID}");

                switch (packetType)
                {
                    case EPacketType.ChallengeAnswer:
                        HandleChallengeAnswerPacket(data.ClientID, reader);
                        break;
                    case EPacketType.ClientUpdate:
                        HandleClientUpdatePacket(data.ClientID, reader);
                        break;
                    case EPacketType.Data:
                        HandleDataPacket(data.ClientID, reader, data.Channel);
                        break;
                    default:
                        return;
                }
            }
            catch (Exception e)
            {
                _networkManager.Logger?.Log(e.Message);
            }
        }

        private void HandleChallengeAnswerPacket(uint clientID, Reader reader)
        {
            if (!_authenticatingClients.TryGetValue(clientID, out var challenge))
                return;

            var packet = ChallengeAnswerPacket.Read(reader);
            if (!CompareByteArrays(challenge, packet.ChallengeAnswer))
            {
                _networkManager.Transport?.DisconnectClient(clientID);
                _authenticatingClients.TryRemove(clientID, out _);
                return;
            }
            
            // inform client of authentication
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(ConnectionAuthenticatedPacket.PacketType);
            ConnectionAuthenticatedPacket authentication = new(clientID, Servername, MaxNumberOfClients);
            ConnectionAuthenticatedPacket.Write(writer, authentication);
            _networkManager.Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
            writer.Clear();
            
            // inform client of other clients
            writer.WriteByte(ClientUpdatePacket.PacketType);
            var pos = writer.Position;
            foreach (var kvp in ConnectedClients)
            {
                writer.Position = pos;
                var clientInfo = kvp.Value;
                ClientUpdatePacket existingClient = new(clientInfo.ID, ClientUpdatePacket.UpdateType.Connected,
                    clientInfo.Username, clientInfo.UserColour);
                ClientUpdatePacket.Write(writer, existingClient);
                _networkManager.Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
            }
            writer.Clear();
            
            // inform other clients of new client
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket update = new(clientID, ClientUpdatePacket.UpdateType.Connected, packet.Username,
                packet.Colour);
            ClientUpdatePacket.Write(writer, update);
            var data = writer.GetBuffer();
            foreach (var id in ConnectedClients.Keys)
                _networkManager.Transport?.SendDataToClient(id, data, ENetworkChannel.ReliableOrdered);
            writer.Clear();
            
            // authenticate client
            ConnectedClients[clientID] = new(clientID, packet.Username, packet.Colour);
            _authenticatingClients.TryRemove(clientID, out _);
            OnRemoteClientConnected?.Invoke(clientID);
            _networkManager.Logger?.Log($"Server: Remote client {clientID} was connected", EMessageSeverity.Log);
        }

        private void HandleClientUpdatePacket(uint clientID, Reader reader)
        {
            if (!ConnectedClients.ContainsKey(clientID))
                return;

            var packet = ClientUpdatePacket.Read(reader);
            if (packet.ClientID != clientID || packet.Type != ClientUpdatePacket.UpdateType.Updated)
                return;
            
            // apply update
            ConnectedClients[clientID].Username = packet.Username;
            ConnectedClients[clientID].UserColour = packet.Colour;
            OnRemoteClientUpdated?.Invoke(clientID);

            // inform other clients of update
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in ConnectedClients.Keys)
            {
                if (id == clientID) continue;
                _networkManager.Transport?.SendDataToClient(id, data, ENetworkChannel.ReliableOrdered);
            }
        }

        private void HandleDataPacket(uint clientID, Reader reader, ENetworkChannel channel)
        {
            if (!ConnectedClients.TryGetValue(clientID, out _))
                return;

            var packet = DataPacket.Read(reader);
            uint[] targetIDs = { };
            switch (packet.DataType)
            {
                case DataPacket.DataPacketType.Forwarded:
                    return;
                // ReSharper disable once PossibleInvalidOperationException
                case DataPacket.DataPacketType.ToClient:
                    targetIDs = new[] { (uint)packet.TargetID };
                    break;
                case DataPacket.DataPacketType.ToClients:
                    targetIDs = packet.TargetIDs;
                    break;
                case DataPacket.DataPacketType.ToServer:
                    if (packet.IsStructData)
                        // ReSharper disable once PossibleInvalidOperationException
                        ReceiveStructData(packet.DataID, clientID, packet.Data);
                    else
                        // ReSharper disable once PossibleInvalidOperationException
                        ReceiveByteData(packet.DataID, clientID, packet.Data);
                    break;
            }
            
            // forward data to defined clients
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket forwardedPacket = new(DataPacket.DataPacketType.Forwarded, clientID, packet.IsStructData,
                packet.DataID, packet.Data);
            DataPacket.Write(writer, forwardedPacket);
            var data = writer.GetBuffer();
            foreach (var id in targetIDs)
            {
                if (id == clientID) continue;
                _networkManager.Transport?.SendDataToClient(id, data, channel);
            }
        }
        
        private static bool CompareByteArrays(IEnumerable<byte> a, IEnumerable<byte> b)
        {
            return a.SequenceEqual(b);
        }
        
        #endregion
        
        #region byte data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredServerByteDataCallbacks = new();

        /// <summary>
        /// Registers a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public void RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredServerByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
            {
                callbacks = new();
                _registeredServerByteDataCallbacks.TryAdd(byteDataHash, callbacks);
            }

            var key = callback.GetHashCode();
            var del = CreateByteDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
        }
        
        /// <summary>
        /// Unregisters a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredServerByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        /// <summary>
        /// Sends byte data with a given id from the local server to a given remote client.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToClient(uint clientID, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(new [] { clientID }, byteID, byteData, channel);
        }
        
        /// <summary>
        /// Sends byte data with a given id from the local server to all other remote clients.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToAll(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(ConnectedClients.Keys.ToArray(), byteID, byteData, channel);
        }
        
        /// <summary>
        /// Sends byte data with a given id from the local server to a list of remote clients.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local server must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (ConnectedClients.ContainsKey(id)) continue;
                _networkManager.Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket packet = new(DataPacket.DataPacketType.Forwarded, 0, false,
                Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in clientIDs)
            {
                _networkManager.Transport?.SendDataToClient(id, data, channel);
            }
        }

        private void ReceiveByteData(uint byteID, uint clientID, byte[] data)
        {
            if (!_registeredServerByteDataCallbacks.TryGetValue(byteID, out var callbacks))
                return;

            foreach (var callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }
        
        #endregion
        
        #region struct data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredServerStructDataCallbacks = new();

        /// <summary>
        /// Registers a callback for a sent struct
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public void RegisterStructData<T>(Action<uint, T> callback) where T : struct
        {
	        var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredServerStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
			{
                callbacks = new();
                _registeredServerStructDataCallbacks.TryAdd(structDataHash, callbacks);
			}

			var key = callback.GetHashCode();
			var del = CreateStructDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del); 
        }
        
        /// <summary>
        /// Unregisters a callback for a sent struct
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public void UnregisterStructData<T>(Action<uint, T> callback) where T : struct
		{
			var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredServerStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        /// <summary>
        /// Sends a struct from the local server to a given remote client.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToClient<T>(uint clientID, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            SendStructDataToClients(new [] { clientID }, structData, channel); 
        }
        
        /// <summary>
        /// Sends a struct from the local server to all remote clients.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToAll<T>(T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            SendStructDataToClients(ConnectedClients.Keys.ToArray(), structData, channel); 
        }
        
        /// <summary>
        /// Sends a struct from the local server to a list of remote clients.
        /// Can only be called after the local server has been started
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToClients<T>(uint[] clientIDs, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local server must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (ConnectedClients.ContainsKey(id)) continue;
                _networkManager.Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket packet = new(DataPacket.DataPacketType.Forwarded, 0, true,
                Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in clientIDs)
            {
                _networkManager.Transport?.SendDataToClient(id, data, channel);
            } 
        }
		
		private void ReceiveStructData(uint structHash, uint clientID, byte[] data)
		{
			if (!_registeredServerStructDataCallbacks.TryGetValue(structHash, out var callbacks))
				return;

			foreach (var callback in callbacks.Values)
			{
				callback?.Invoke(clientID, data);
			}
        }
        
        #endregion
        
        #region utilities
        
        private delegate void ByteDataCallback(uint senderID, byte[] data);
        private delegate void StructDataCallback(uint senderID, byte[] data);
        
        private ByteDataCallback CreateByteDataDelegate(Action<uint, byte[]> callback)
        {
            return ParseDelegate;
            void ParseDelegate(uint senderID, byte[] data)
            {
                callback?.Invoke(senderID, data);
            }
        }
        
        private StructDataCallback CreateStructDataDelegate<T>(Action<uint, T> callback)
        {
            return ParseDelegate;
            void ParseDelegate(uint senderID, byte[] data)
            {
                Reader reader = new(data, _networkManager.SerialiserSettings);
                callback?.Invoke(senderID, reader.Read<T>());
            }
        }
        
        #endregion
    }
    
    public enum ELocalServerConnectionState
    {
        /// <summary>
        /// Signifies the start of a local connection
        /// </summary>
        Starting = 0,
        /// <summary>
        /// Signifies that a local connection has been successfully established
        /// </summary>
        Started = 1,
        /// <summary>
        /// Signifies that an established local connection is being closed
        /// </summary>
        Stopping = 2,
        /// <summary>
        /// Signifies that an established local connection was closed
        /// </summary>
        Stopped = 3
    }
}
