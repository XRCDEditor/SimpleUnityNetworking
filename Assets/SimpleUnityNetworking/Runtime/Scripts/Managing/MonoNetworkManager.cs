using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Modules;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
	[AddComponentMenu("SimpleUnityNetworking/Component/Network Manager (Mono)")]
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
	    [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
	    public Transport Transport => NetworkManager.Transport;
	    public TransportConfiguration TransportConfiguration
	    {
		    get => NetworkManager.TransportConfiguration;
		    set
		    {
			    if (NetworkManager.TransportConfiguration == value) return;
                NetworkManager.TransportConfiguration = _cachedTransportConfiguration = value;
			    
#if UNITY_EDITOR
			    if (value != null)
				    EditorUtility.SetDirty(_cachedTransportConfiguration);
			    if (!EditorApplication.isPlaying)
					EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    [SerializeField] private SerialiserConfiguration _cachedSerialiserConfiguration;
	    public SerialiserSettings SerialiserSettings => NetworkManager.SerialiserSettings;
	    public SerialiserConfiguration SerialiserConfiguration
	    {
		    get => NetworkManager.SerialiserConfiguration;
		    set
		    {
			    if (NetworkManager.SerialiserConfiguration == value) return;
			    NetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;

#if UNITY_EDITOR
			    if (value != null)
				    EditorUtility.SetDirty(_cachedSerialiserConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    [SerializeField] private LoggerConfiguration _cachedLoggerConfiguration;
	    public Logger Logger => NetworkManager.Logger;
	    public LoggerConfiguration LoggerConfiguration
	    {
		    get => NetworkManager.LoggerConfiguration;
		    set
		    {
			    if (NetworkManager.LoggerConfiguration == value) return;
			    NetworkManager.LoggerConfiguration = _cachedLoggerConfiguration = value;

#if UNITY_EDITOR
			    if (value != null)
				    EditorUtility.SetDirty(_cachedLoggerConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    [SerializeField] private List<ModuleConfiguration> _cachedModuleConfigs = new();
	    public ModuleList Modules => NetworkManager.Modules;

	    public Server Server => NetworkManager.Server;
	    public Client Client => NetworkManager.Client;

	    public bool IsServer => NetworkManager.IsServer;
	    public bool IsClient => NetworkManager.IsClient;
	    public bool IsOnline => NetworkManager.IsOnline;
	    public bool IsHost => NetworkManager.IsHost;
	    
	    public event Action OnTransportDisposed
	    {
		    add => NetworkManager.OnTransportDisposed += value;
		    remove => NetworkManager.OnTransportDisposed -= value;
	    }
	    public event Action<ServerReceivedData> OnServerReceivedData
	    {
		    add => NetworkManager.OnServerReceivedData += value;
		    remove => NetworkManager.OnServerReceivedData -= value;
	    }
	    public event Action<ClientReceivedData> OnClientReceivedData
	    {
		    add => NetworkManager.OnClientReceivedData += value;
		    remove => NetworkManager.OnClientReceivedData -= value;
	    }
	    public event Action<ELocalConnectionState> OnServerStateUpdated
	    {
		    add => NetworkManager.OnServerStateUpdated += value;
		    remove => NetworkManager.OnServerStateUpdated -= value;
	    }
	    public event Action<ELocalConnectionState> OnClientStateUpdated
	    {
		    add => NetworkManager.OnClientStateUpdated += value;
		    remove => NetworkManager.OnClientStateUpdated -= value;
	    }
	    public event Action<uint, ERemoteConnectionState> OnConnectionUpdated
	    {
		    add => NetworkManager.OnConnectionUpdated += value;
		    remove => NetworkManager.OnConnectionUpdated -= value;
	    }
	    public event Action<string, EMessageSeverity> OnTransportLogAdded
	    {
		    add => NetworkManager.OnTransportLogAdded += value;
		    remove => NetworkManager.OnTransportLogAdded -= value;
	    }
	    public event Action<uint> OnTickStarted
	    {
		    add => NetworkManager.OnTickStarted += value;
		    remove => NetworkManager.OnTickStarted -= value;
	    }
	    public event Action<uint> OnTickCompleted
	    {
		    add => NetworkManager.OnTickCompleted += value;
		    remove => NetworkManager.OnTickCompleted -= value;
	    }

	    private NetworkManager _networkManager;
	    /// <summary>
	    /// Instance of the internal network manager held by the scene context 
	    /// </summary>
	    public NetworkManager NetworkManager
	    {
		    get
		    {
			    if (_networkManager != null) return _networkManager;
			    _networkManager = new();
			    _networkManager.TransportConfiguration = _cachedTransportConfiguration;
			    _networkManager.SerialiserConfiguration = _cachedSerialiserConfiguration;
			    _networkManager.LoggerConfiguration = _cachedLoggerConfiguration;
			    
			    foreach (var config in _cachedModuleConfigs)
				    Modules.Add(config.GetModule(this));
			    
#if UNITY_EDITOR
			    NetworkManager.Modules.OnModuleAdded += OnModuleAdded;
			    NetworkManager.Modules.OnModuleRemoved += OnModuleRemoved;
			    NetworkManager.Modules.OnModuleInserted += OnModuleInserted;
			    NetworkManager.Modules.OnModuleRemovedAt += OnModuleRemovedAt;
#endif
			    
			    return _networkManager;
		    }
		    private set => _networkManager = value;
	    }

	    public void Tick()
	    {
		    NetworkManager.Tick();
	    }

	    private void OnDestroy()
	    {
		    NetworkManager.Dispose();
		    NetworkManager = null;
	    }

	    public void StartServer()
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartServer();
	    }

	    public void StopServer()
	    {
		    NetworkManager.StopServer();
	    }

	    public void StartClient()
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartClient();
	    }

	    public void StopClient()
	    {
		    NetworkManager.StopClient();
	    }

	    public void StartHost()
	    {
		    NetworkManager.StartHost();
	    }

	    public void StopHost()
	    {
		    NetworkManager.StopHost();
	    }
	    
	    #region private methods

#if UNITY_EDITOR
	    private void OnModuleAdded(ModuleConfiguration config)
	    {
		    _cachedModuleConfigs.Add(config);
		    if (!EditorApplication.isPlaying)
			    EditorSceneManager.MarkSceneDirty(gameObject.scene);
	    }

	    private void OnModuleRemoved(ModuleConfiguration config)
	    {
		    _cachedModuleConfigs.Remove(config);
		    if (!EditorApplication.isPlaying)
			    EditorSceneManager.MarkSceneDirty(gameObject.scene);
	    }

	    private void OnModuleInserted(int index, ModuleConfiguration config)
	    {
		    _cachedModuleConfigs.Insert(index, config);
		    if (!EditorApplication.isPlaying)
			    EditorSceneManager.MarkSceneDirty(gameObject.scene);
	    }

	    private void OnModuleRemovedAt(int index)
	    {
		    _cachedModuleConfigs.RemoveAt(index);
		    if (!EditorApplication.isPlaying)
			    EditorSceneManager.MarkSceneDirty(gameObject.scene);
	    }
#endif
	    
	    #endregion
    }
}
