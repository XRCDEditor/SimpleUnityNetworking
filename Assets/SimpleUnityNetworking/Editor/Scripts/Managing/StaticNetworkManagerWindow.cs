using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Modules;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        private const int PADDING = 10;

        private INetworkManagerEditor _networkManagerEditor;
        private INetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor != null)
                    return _networkManagerEditor;
                return _networkManagerEditor = new(
                    StaticNetworkManager.NetworkManager,
                    INetworkManagerEditor.EAllowStart.OnlyEditor
                );
            }
        }

        [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
        public TransportConfiguration TransportConfiguration
        {
            get => _cachedTransportConfiguration;
            set
            {
                if (StaticNetworkManager.TransportConfiguration == value) return;
                StaticNetworkManager.TransportConfiguration = _cachedTransportConfiguration = value;
                
                if (value != null)
                    EditorUtility.SetDirty(_cachedTransportConfiguration);
            }
        }
        [SerializeField] private SerialiserConfiguration _cachedSerialiserConfiguration;
        public SerialiserConfiguration SerialiserConfiguration
        {
            get => _cachedSerialiserConfiguration;
            set
            {
                if (StaticNetworkManager.SerialiserConfiguration == value) return;
                StaticNetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;

                if (value != null)
                    EditorUtility.SetDirty(_cachedSerialiserConfiguration);
            }
        }
        [SerializeField] private LoggerConfiguration _cachedLoggerConfiguration;
        public LoggerConfiguration LoggerConfiguration
        {
            get => _cachedLoggerConfiguration;
            set
            {
                if (StaticNetworkManager.LoggerConfiguration == value) return;
                StaticNetworkManager.LoggerConfiguration = _cachedLoggerConfiguration = value;

                if (value != null)
                    EditorUtility.SetDirty(_cachedLoggerConfiguration);
            }
        }
        
        [SerializeField] private List<ModuleConfiguration> _cachedModuleConfigs = new();

        [SerializeField] private bool _showTransportWindow = true;
        [SerializeField] private bool _showSerialiserWindow = true;
        [SerializeField] private bool _showLoggerWindow = true;

        [MenuItem("Window/SimpleUnityNetworking/Network Manager (Static)")]
        public static void ShowWindow()
        {
            GetWindow(typeof(StaticNetworkManagerWindow), false, "Network Manager (Static)");
        }
        
        private void Awake()
        {
            foreach (var config in _cachedModuleConfigs)
                StaticNetworkManager.Modules.Add(config.GetModule(StaticNetworkManager.NetworkManager));
			    
            StaticNetworkManager.Modules.OnModuleAdded += OnModuleAdded;
            StaticNetworkManager.Modules.OnModuleRemoved += OnModuleRemoved;
            StaticNetworkManager.Modules.OnModuleInserted += OnModuleInserted;
            StaticNetworkManager.Modules.OnModuleRemovedAt += OnModuleRemovedAt;

            StaticNetworkManager.Client.OnLocalStateUpdated += RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientConnected += RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientDisconnected += RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientUpdated += RepaintOnUpdate;
            StaticNetworkManager.Client.OnServerUpdated += RepaintOnUpdate;
            StaticNetworkManager.Server.OnLocalStateUpdated += RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientConnected += RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientDisconnected += RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientUpdated += RepaintOnUpdate;
            StaticNetworkManager.Server.OnServerUpdated += RepaintOnUpdate;
        }

        private void OnDestroy()
        {
            StaticNetworkManager.Modules.OnModuleAdded -= OnModuleAdded;
            StaticNetworkManager.Modules.OnModuleRemoved -= OnModuleRemoved;
            StaticNetworkManager.Modules.OnModuleInserted -= OnModuleInserted;
            StaticNetworkManager.Modules.OnModuleRemovedAt -= OnModuleRemovedAt;
            
            StaticNetworkManager.Client.OnLocalStateUpdated -= RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientConnected -= RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientDisconnected -= RepaintOnUpdate;
            StaticNetworkManager.Client.OnRemoteClientUpdated -= RepaintOnUpdate;
            StaticNetworkManager.Client.OnServerUpdated -= RepaintOnUpdate;
            StaticNetworkManager.Server.OnLocalStateUpdated -= RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientConnected -= RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientDisconnected -= RepaintOnUpdate;
            StaticNetworkManager.Server.OnRemoteClientUpdated -= RepaintOnUpdate;
            StaticNetworkManager.Server.OnServerUpdated -= RepaintOnUpdate;
        }

        private void RepaintOnUpdate() => Repaint();
        private void RepaintOnUpdate(uint _) => Repaint();
        private void RepaintOnUpdate(ELocalClientConnectionState _) => Repaint();
        private void RepaintOnUpdate(ELocalServerConnectionState _) => Repaint();

        private void OnGUI()
        {
            var area = new Rect(PADDING, PADDING, position.width - PADDING * 2f, position.height - PADDING * 2f);

            GUILayout.BeginArea(area);
            GUILayout.Label("Network Manager (Static)", EditorStyles.largeLabel);

            EditorGUILayout.Space();
            GUILayout.Label("Configurations:", EditorStyles.boldLabel);
            TransportGUI();
            SerialiserGUI();
            LoggerGUI();
            
            EditorGUILayout.Space();
            GUILayout.Label("Modules:", EditorStyles.boldLabel);
            NetworkManagerEditor.ModuleGUI();

            EditorGUILayout.Space();
            GUILayout.Label("Managers:", EditorStyles.boldLabel);
            NetworkManagerEditor.ServerGUI();
            NetworkManagerEditor.ClientGUI();
            
            GUILayout.EndArea();
        }

        private void TransportGUI()
        {
            TransportConfiguration = NetworkManagerEditor.ConfigurationGUI<TransportConfiguration>(_cachedTransportConfiguration, "Transport", ref _showTransportWindow);
        }

        private void SerialiserGUI()
        {
            SerialiserConfiguration = NetworkManagerEditor.ConfigurationGUI<SerialiserConfiguration>(_cachedSerialiserConfiguration, "Serialiser", ref _showSerialiserWindow);
        }

        private void LoggerGUI()
        {
            LoggerConfiguration = NetworkManagerEditor.ConfigurationGUI<LoggerConfiguration>(_cachedLoggerConfiguration, "Logger", ref _showLoggerWindow);
        }
        
        private void OnModuleAdded(ModuleConfiguration config)
        {
            _cachedModuleConfigs.Add(config);
        }

        private void OnModuleRemoved(ModuleConfiguration config)
        {
            _cachedModuleConfigs.Remove(config);
        }

        private void OnModuleInserted(int index, ModuleConfiguration config)
        {
            _cachedModuleConfigs.Insert(index, config);
        }

        private void OnModuleRemovedAt(int index)
        {
            _cachedModuleConfigs.RemoveAt(index);
        }
    }
}
