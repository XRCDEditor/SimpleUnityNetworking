using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public static class StaticNetworkManager
    {
        #region public members

        /// <summary>
        /// The Configuration for the networking.
        /// </summary>
        public static NetworkConfiguration NetworkConfiguration
        {
            get
            {
                if (_networkConfiguration == null)
                    _networkConfiguration = LoadOrCreateConfiguration<NetworkConfiguration>();
                return _networkConfiguration;
            }
            set => _networkConfiguration = value;
        }

        /// <summary>
        /// Wether the local client is currently connected to or hosting a server.
        /// </summary>
        public static bool IsConnected => NetworkManager.IsConnected;
        /// <summary>
        /// Wether the local client is currently hosting a lobby.
        /// </summary>
        public static bool IsHost => NetworkManager.IsHost;
        /// <summary>
        /// The current connection status of the local client.
        /// </summary>
        public static EConnectionStatus ConnectionStatus => NetworkManager.ConnectionStatus;
        /// <summary>
        /// Information on the server the client is currently connected to.
        /// </summary>
        public static ServerInformation ServerInformation => NetworkManager.ServerInformation;
        /// <summary>
        /// Information on the local clients information associated with the server they are connected to.
        /// </summary>
        public static ClientInformation ClientInformation => NetworkManager.ClientInformation;
        /// <summary>
        /// All other clients that are connected to the same server as the local client.
        /// </summary>
        public static ConcurrentDictionary<byte, ClientInformation> ConnectedClients => NetworkManager.ConnectedClients;
        /// <summary>
        /// The number of connected clients.
        /// </summary>
        public static byte NumberConnectedClients => NetworkManager.NumberConnectedClients;

        /// <summary>
        /// Wether the server discovery is currently active or not.
        /// </summary>
        public static bool IsServerDiscoveryActive => NetworkManager.IsServerDiscoveryActive;
        /// <summary>
        /// All open servers that the local client could connect to.
        /// </summary>
        public static List<OpenServer> OpenServers => NetworkManager.OpenServers;

        /// <summary>
        /// Network events.
        /// </summary>
        public static NetworkEvents Events => NetworkManager.Events;

        #endregion

        #region private members

        private static NetworkManager _networkManager;
        private static NetworkConfiguration _networkConfiguration;

        private static NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager == null)
                    _networkManager = new(NetworkConfiguration);
                return _networkManager;
            }
        }

        #endregion

        #region lifecycle

        static StaticNetworkManager()
        {
            StartServerDiscovery();
        }

        #endregion

        #region public methods

        /// <summary>
        /// Creates a new server with the local client has host.
        /// </summary>
        /// <param name="servername"></param>
        /// <param name="maxNumberClients"></param>
        /// <param name="onConnectionEstablished">Will be called once the server was successfully or failed to be created</param>
        public static void CreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
        {
            if (Application.isPlaying) return;

            onConnectionEstablished += ListenForStateChange;

            NetworkManager.CreateServer(servername, maxNumberClients, onConnectionEstablished);
        }

        /// <summary>
        /// Joins an open server as client.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <param name="onConnectionEstablished">Will be called once the connection to the server was successfully or failed to be created</param>
        public static void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            if (Application.isPlaying) return;

            onConnectionEstablished += ListenForStateChange;

            NetworkManager.JoinServer(serverIP, serverPort, onConnectionEstablished);
        }

        /// <summary>
        /// Disconnects from the current server. Also closes the server if the local client is the host.
        /// </summary>
        public static void DisconnectFromServer()
        {
            NetworkManager.DisconnectFromServer();

            ListenForStateChange(false);
        }

        /// <summary>
        /// Starts the Server Discovery unless it is already active.
        /// </summary>
        public static void StartServerDiscovery()
        {
            NetworkManager.StartServerDiscovery();
        }

        /// <summary>
        /// Ends the Server Discovery unless it is already inactive.
        /// </summary>
        public static void EndServerDiscovery()
        {
            NetworkManager.EndServerDiscovery();
        }

        /// <summary>
        /// Restarts the Server Discovery.
        /// </summary>
        public static void RestartServerDiscovery()
        {
            NetworkManager.RestartServerDiscovery();
        }

        /// <summary>
        /// Registers a callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        public static void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.RegisterStructData(callback);
        }

        /// <summary>
        /// Unregisters a registered callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        public static void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.UnregisterStructData(callback);
        }

        /// <summary>
        /// Registers a callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        public static void RegisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.RegisterByteData(dataID, callback);
        }

        /// <summary>
        /// Unregisters a registered callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        public static void UnregisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.UnregisterByteData(dataID, callback);
        }

        /// <summary>
        /// Sends a struct over the network to all other connected clients.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendStructDataToAll<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructDataToAll(structData, networkChannel, onDataSend);
        }

        /// <summary>
        /// Sends a struct over the network to the server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendStructDataToServer<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructDataToServer(structData, networkChannel, onDataSend);
        }

        /// <summary>
        /// Sends a struct over the network to a specific client.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="receiverID"></param>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendStructData<T>(byte receiverID, T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructData(receiverID, structData, networkChannel, onDataSend);
        }

        /// <summary>
        /// Sends a byte array over the network to all other connected clients.
        /// </summary>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendByteDataToAll(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteDataToAll(dataID, data, networkChannel, onDataSend);
        }

        /// <summary>
        /// Sends a byte array over the network to the server.
        /// </summary>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendByteDataToServer(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteDataToServer(dataID, data, networkChannel, onDataSend);
        }

        /// <summary>
        /// Sends a byte array over the network to a specific client.
        /// </summary>
        /// <param name="receiverID"></param>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        public static void SendByteData(byte receiverID, string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteData(receiverID, dataID, data, networkChannel, onDataSend);
        }

        #endregion

        #region private methods

        private static T LoadOrCreateConfiguration<T>(string name = "NetworkConfiguration", string path = "Assets/Resources/") where T : ScriptableObject
        {
            T configuration = Resources.Load<T>(Path.GetFileNameWithoutExtension(name));

            string fullPath = path + name + ".asset";

            if (!configuration)
            {
                if (EditorApplication.isCompiling)
                {
                    UnityEngine.Debug.LogError("Can not load settings when editor is compiling!");
                    return null;
                }
                if (EditorApplication.isUpdating)
                {
                    UnityEngine.Debug.LogError("Can not load settings when editor is updating!");
                    return null;
                }

                configuration = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            }
            if (!configuration)
            {
                string[] allSettings = AssetDatabase.FindAssets($"t:{name}{".asset"}");
                if (allSettings.Length > 0)
                {
                    configuration = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(allSettings[0]));
                }
            }
            if (!configuration)
            {
                configuration = ScriptableObject.CreateInstance<T>();
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(configuration, fullPath);
                AssetDatabase.SaveAssets();
            }

            if (!configuration)
            {
                configuration = ScriptableObject.CreateInstance<T>();
            }

            return configuration;
        }

        private static void ListenForStateChange(bool isActive)
        {
            if (isActive)
                EditorApplication.playModeStateChanged += PreventPlayMode;
            else
                EditorApplication.playModeStateChanged -= PreventPlayMode;
        }

        private static void PreventPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                EditorApplication.isPlaying = false;
        }

        #endregion
    }
}