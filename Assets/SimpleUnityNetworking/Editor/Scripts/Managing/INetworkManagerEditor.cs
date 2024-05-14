using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    internal class INetworkManagerEditor
    {
        public enum EAllowStart
        {
            Anywhere,
            OnlyEditor,
            OnlyPlaymode
        }

        #region fields

        private readonly INetworkManager _manager;
        private readonly Action _repaint;
        private readonly EAllowStart _allowStart;

        private readonly GUIStyle _style = new();

        private bool _showServerWindow = true;
        private string _servername = "New Server";
        private Vector2 _serverClientsViewPos;

        private bool _showClientWindow = true;
        private string _username = "Username";
        private Color32 _userColour = new(153, 191, 97, 255);
        private Vector2 _clientClientsViewPos;

        #endregion

        #region lifecycle

        public INetworkManagerEditor(INetworkManager manager, Action repaint, EAllowStart allowStart)
        {
            _manager = manager;
            _repaint = repaint;
            _allowStart = allowStart;
        }

        public void ManagerGUIs()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Managers:", EditorStyles.boldLabel);
            {
                ServerGUI();
                ClientGUI();
            }
        }

        #endregion

        #region configs

        public T ConfigurationGUI<T>(ScriptableObject configuration, string title, ref bool showSection) where T : ScriptableObject
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout(title, ref showSection);
            if (showSection)
            {
                if (!_manager.IsOnline)
                    configuration = (T)EditorGUILayout.ObjectField("Asset:", configuration, typeof(T), false);
                configuration ??= LoadOrCreateConfiguration<T>(typeof(T).Name);

                if (configuration)
                    Editor.CreateEditor(configuration).OnInspectorGUI();
            }
            GUILayout.EndVertical();

            return configuration as T;
        }

        #endregion

        #region managing

        private void ServerGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Server", ref _showServerWindow, _manager.IsServer, "Is Server:");
            if (_showServerWindow)
            {
                if (!_manager.IsServer)
                {
                    _servername = EditorGUILayout.TextField(new GUIContent("Servername:"), _servername);
                    if (GUILayout.Button(new GUIContent("Start Server")) && AllowStart())
                        _manager.StartServer(_servername);
                }
                else
                {
                    EditorGUILayout.TextField("Servername:", _manager.ServerInformation.Servername);
                    EditorGUILayout.TextField("Connected Clients:", $"{_manager.Server_ConnectedClients.Count}/{_manager.ServerInformation.MaxNumberConnectedClients}");
                    if (GUILayout.Button(new GUIContent("Stop Server")))
                        _manager.StopServer();

                    _serverClientsViewPos = EditorGUILayout.BeginScrollView(_serverClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Server_ConnectedClients.Count == 0)
                    {
                        GUILayout.Label($"There are no clients connected to the local server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Server_ConnectedClients.Count; i++)
                        {
                            var client = _manager.Server_ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.Colour;
                                GUILayout.Label($"#{client.ID} {client.Username}", _style);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        _style.normal.textColor = defaultColour;
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.EndVertical();
        }

        private void ClientGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Client", ref _showClientWindow, _manager.IsClient, "Is Client:");
            if (_showClientWindow)
            {
                if (!_manager.IsClient)
                {
                    _username = EditorGUILayout.TextField(new GUIContent("Username:"), _username);
                    _userColour = EditorGUILayout.ColorField(new GUIContent("User colour:"), _userColour);
                    if (GUILayout.Button(new GUIContent("Start Client")) && AllowStart())
                        _manager.StartClient(_username, _userColour);
                }
                else
                {
                    EditorGUILayout.TextField("ID:", $"{_manager.ClientInformation.ID}");
                    EditorGUILayout.TextField("Username:", _manager.ClientInformation.Username);
                    EditorGUILayout.ColorField("User colour:", _manager.ClientInformation.Colour);
                    EditorGUILayout.TextField("Servername:", _manager.ServerInformation.Servername);
                    EditorGUILayout.TextField("Connected Clients:", $"{_manager.Server_ConnectedClients.Count}/{_manager.ServerInformation.MaxNumberConnectedClients}");
                    if (GUILayout.Button(new GUIContent("Stop Client")))
                        _manager.StopClient();

                    _clientClientsViewPos = EditorGUILayout.BeginScrollView(_clientClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Client_ConnectedClients.Count == 0)
                    {
                        GUILayout.Label($"There are no other clients connected to the server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Client_ConnectedClients.Count; i++)
                        {
                            var client = _manager.Client_ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.Colour;
                                GUILayout.Label($"#{client.ID} {client.Username}", _style);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        _style.normal.textColor = defaultColour;
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.EndVertical();
        }

        #endregion

        #region utilities

        public bool AllowStart()
        {
            return _allowStart switch
            {
                EAllowStart.Anywhere => true,
                EAllowStart.OnlyEditor => !EditorApplication.isPlaying,
                EAllowStart.OnlyPlaymode => EditorApplication.isPlaying,
                _ => false
            };
        }

        public static void DrawToggleFoldout(string title, ref bool isExpanded,
            bool? checkbox = null, string checkboxLabel = null)
        {
            Color normalColour = new(0.24f, 0.24f, 0.24f);
            Color hoverColour = new(0.27f, 0.27f, 0.27f);
            var currentColour = normalColour;

            var backgroundRect = GUILayoutUtility.GetRect(1f, 17f);
            var labelRect = backgroundRect;
            labelRect.xMin += 16f;
            labelRect.xMax -= 2f;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;

            var toggleRect = backgroundRect;
            toggleRect.x = backgroundRect.width - 7f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;

            var toggleLabelRect = backgroundRect;
            toggleLabelRect.x = -10f;

            var e = Event.current;
            if (labelRect.Contains(e.mousePosition))
                currentColour = hoverColour;
            EditorGUI.DrawRect(backgroundRect, currentColour);

            if (isExpanded)
            {
                var borderBot = GUILayoutUtility.GetRect(1f, 0.6f);
                EditorGUI.DrawRect(borderBot, new(0, 0, 0));
            }

            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            isExpanded = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

            if (checkbox is not null)
            {
                if (checkboxLabel is not null)
                {
                    var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                    EditorGUI.LabelField(toggleLabelRect, checkboxLabel, labelStyle);
                }
                EditorGUI.Toggle(toggleRect, (bool)checkbox, new("ShurikenToggle"));
            }

            if (e.type == EventType.MouseDown && labelRect.Contains(e.mousePosition) && e.button == 0)
            {
                isExpanded = !isExpanded;
                e.Use();
            }

        }

        public static T LoadOrCreateConfiguration<T>(string name = "Configuration", string path = "Assets/Resources/") where T : ScriptableObject
        {
            T configuration = Resources.Load<T>(Path.GetFileNameWithoutExtension(name));

            string fullPath = path + name + ".asset";

            if (!configuration)
            {
                if (EditorApplication.isCompiling)
                {
                    Debug.LogError("Can not load settings when editor is compiling!");
                    return null;
                }
                if (EditorApplication.isUpdating)
                {
                    Debug.LogError("Can not load settings when editor is updating!");
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

        #endregion
    }
}
