using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public static class StaticNetworkManager
    {
        /// <summary>
        /// The transport instance defined by the configuration
        /// </summary>
        public static Transport Transport => _networkManager.Transport;
        /// <summary>
        /// The configuration containing the instance of the <see cref="Transport"/>,
        /// which will be used for sending and receiving data
        /// </summary>
        public static TransportConfiguration TransportConfiguration
        {
            get => _networkManager.TransportConfiguration;
            set => _networkManager.TransportConfiguration = value;
        }

        /// <summary>
        /// The configuration for the serialiser used by the network manager.
        /// </summary>
        public static SerialiserConfiguration SerialiserConfiguration
        {
            get => _networkManager.SerialiserConfiguration;
            set => _networkManager.SerialiserConfiguration = value;
        }

        /// <summary>
        /// The logger instance defined by the configuration
        /// </summary>
        public static Logger Logger => _networkManager.Logger;
        /// <summary>
        /// The configuration for the logger, used to show or save messages by the framework.
        /// </summary>
        public static LoggerConfiguration LoggerConfiguration
        {
            get => _networkManager.LoggerConfiguration;
            set => _networkManager.LoggerConfiguration = value;
        }

        /// <summary>
        /// Whether a local server is started or a client is authenticated
        /// </summary>
        public static bool IsOnline => _networkManager.IsOnline;
        /// <summary>
        /// Whether a local server is started
        /// </summary>
        public static bool IsServer => _networkManager.IsServer;
        /// <summary>
        /// Whether a local client is started and authenticated
        /// </summary>
        public static bool IsClient => _networkManager.IsClient;
        /// <summary>
        /// Whether a local server is started and local client is authenticated
        /// </summary>
        public static bool IsHost => _networkManager.IsHost;

        /// <summary>
        /// Information about the local or connected remote server
        /// </summary>
        public static ServerInformation ServerInformation => _networkManager.ServerInformation;
        /// <summary>
        /// The current connection state of the local server
        /// </summary>
        public static ELocalServerConnectionState Server_LocalState => _networkManager.Server_LocalState;
        /// <summary>
        /// The clients that are connected to the local server
        /// </summary>
        public static ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => _networkManager.Server_ConnectedClients;
        /// <summary>
        /// Information about the authenticated local client
        /// </summary>
        public static ClientInformation ClientInformation => _networkManager.ClientInformation;
        /// <summary>
        /// The current connection state of the local client
        /// </summary>
        public static ELocalClientConnectionState Client_LocalState => _networkManager.Client_LocalState;
        /// <summary>
        /// The remote clients that are connected to the same server
        /// </summary>
        public static ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients => _networkManager.Client_ConnectedClients;

        /// <summary>
        /// Called when the local server's connection state has been updated
        /// </summary>
        public static event Action<ELocalServerConnectionState> Server_OnLocalStateUpdated;
        /// <summary>
        /// Called by the local server when a new remote client has been authenticated
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientConnected;
        /// <summary>
        /// Called by the local server when a remote client disconnected
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local server when a remote client updated its information
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientUpdated;

        /// <summary>
        /// Called when the local client's connection state has been updated
        /// </summary>
        public static event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
        /// <summary>
        /// Called by the local client when a new remote client has been authenticated
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientConnected;
        /// <summary>
        /// Called by the local client when a remote client disconnected
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local client when a remote client updated its information
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientUpdated;

        private static NetworkManager _networkManager;
        public static NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager != null) return _networkManager;
                _networkManager = new();
                _networkManager.TransportConfiguration = TransportConfiguration;
                _networkManager.SerialiserConfiguration = SerialiserConfiguration;
                _networkManager.LoggerConfiguration = LoggerConfiguration;
                return _networkManager;
            }
        }

        static StaticNetworkManager()
        {
            NetworkManager.Server_OnLocalStateUpdated += state => Server_OnLocalStateUpdated?.Invoke(state);
            NetworkManager.Server_OnRemoteClientConnected += id => Server_OnRemoteClientConnected?.Invoke(id);
            NetworkManager.Server_OnRemoteClientDisconnected += id => Server_OnRemoteClientDisconnected?.Invoke(id);
            NetworkManager.Server_OnRemoteClientUpdated += id => Server_OnRemoteClientUpdated?.Invoke(id);
            NetworkManager.Client_OnLocalStateUpdated += state => Client_OnLocalStateUpdated?.Invoke(state);
            NetworkManager.Client_OnRemoteClientConnected += id => Client_OnRemoteClientConnected?.Invoke(id);
            NetworkManager.Client_OnRemoteClientDisconnected += id => Client_OnRemoteClientDisconnected?.Invoke(id);
            NetworkManager.Client_OnRemoteClientUpdated += id => Client_OnRemoteClientUpdated?.Invoke(id);
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += PreventPlayMode;
#endif
        }

        /// <summary>
        /// This method updates the incoming and outgoing packets,
        /// effectively dictating the state updates of the network. Should be called once per tick.
        /// </summary>
        public static void Tick()
        {
            _networkManager.Tick();
        }

        /// <summary>
        /// Method to start a local server with the given parameters
        /// </summary>
        /// <param name="servername"></param>
        public static void StartServer(string servername)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartServer(servername);
        }

        /// <summary>
        /// Method to stop the local server
        /// </summary>
        public static void StopServer()
        {
            _networkManager.StopServer();
        }

        /// <summary>
        /// Method to start a local client with the given parameters
        /// </summary>
        /// <param name="username"></param>
        /// <param name="userColour"></param>
        public static void StartClient(string username, Color32 userColour)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartClient(username, userColour);
        }

        /// <summary>
        /// Method to stop the local client 
        /// </summary>
        public static void StopClient()
        {
            _networkManager.StopClient();
        }

        /// <summary>
        /// Method to stop both the local client and server
        /// </summary>
        public static void StopNetwork()
        {
            _networkManager.StopNetwork();
        }

        /// <summary>
        /// Registers a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">Callback which will be invoked after byte data with the given id has been received</param>
        public static void RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.RegisterByteData(byteID, callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">Callback which will be invoked after byte data with the given id has been received</param>
        public static void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.UnregisterByteData(byteID, callback);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.SendByteDataToClient(clientID, byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to all other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.SendByteDataToAll(byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a list of remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.SendByteDataToClients(clientIDs, byteID, byteData, channel);
        }

        /// <summary>
        /// Registers a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">Callback which will be invoked after a struct of the same type has been received</param>
        public static void RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
        {
            _networkManager.RegisterStructData(callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">Callback which will be invoked after a struct of the same type has been received</param>
        public static void UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
        {
            _networkManager.UnregisterStructData(callback);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void SendStructDataToClient<T>(uint clientID, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
        {
            _networkManager.SendStructDataToClient(clientID, structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to all other remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
        {
            _networkManager.SendStructDataToAll(structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a list of remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void SendStructDataToClients<T>(uint[] clientIDs, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
        {
            _networkManager.SendStructDataToClients(clientIDs, structData, channel);
        }

#if UNITY_EDITOR
        private static void PreventPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || !IsOnline) return;
            EditorApplication.isPlaying = false;
            Debug.LogWarning("Playmode is not possible while the static network manager is online!");
        }
#endif
    }
}