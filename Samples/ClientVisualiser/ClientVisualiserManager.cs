using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialising;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Samples
{
    public class ClientVisualiserManager : MonoBehaviour
    {
        [SerializeField] private MonoNetworkManager _networkManager;
        [SerializeField] private ClientVisualiser _visualiserPrefab;
        [SerializeField] private Transform _visualiserParent;

        private readonly Dictionary<uint, ClientVisualiser> _visualisers = new();

        private bool _isUpdating;

		#region lifecycle

		private void OnEnable()
        {
            _networkManager.Client.OnLocalStateUpdated += OnClientStateUpdated;
            _networkManager.Client.OnRemoteClientDisconnected += RemoveConnectedClient;
            _networkManager.OnTickStarted += UpdateCameraTick;
        }

		private void OnDisable()
        {
            _networkManager.Client.OnLocalStateUpdated -= OnClientStateUpdated;
            _networkManager.Client.OnRemoteClientDisconnected -= RemoveConnectedClient;
            _networkManager.OnTickStarted -= UpdateCameraTick;
        }

		#endregion

		#region private methods

        private void OnClientStateUpdated(ELocalClientConnectionState state)
        {
            switch (state)
            {
                case ELocalClientConnectionState.Authenticated:
                    SetIsUpdating(true);
                    break;
                case ELocalClientConnectionState.Stopping:
                    SetIsUpdating(false);
                    break;
            }
        }

        private void RemoveConnectedClient(uint id)
        {
            if (!_visualisers.Remove(id, out var visualiser))
                return;

#if UNITY_EDITOR
            GameObject.DestroyImmediate(visualiser.gameObject);
#else
			GameObject.Destroy(visualiser.gameObject);
#endif
        }

        private void SetIsUpdating(bool isUpdating)
		{
            _isUpdating = isUpdating;
            if (isUpdating)
                _networkManager.Client.RegisterByteData("Visualiser", OnReceiveData);
            else
                _networkManager.Client.UnregisterByteData("Visualiser", OnReceiveData);
        }

        private void OnReceiveData(uint sender, byte[] data)
        {
            if (!_networkManager.Client.ConnectedClients.TryGetValue(sender, out var client))
                return;

            if (!_visualisers.TryGetValue(sender, out var visualiser))
            {
                visualiser = Instantiate(_visualiserPrefab, _visualiserParent);
                visualiser.UpdateVisualiser(client.ID, client.Username, client.UserColour);
                _visualisers.Add(sender, visualiser);
            }

            Reader reader = new(data);
            var visualiserData = ClientVisualiserData.ReadClientVisualiserData(reader);

            visualiser.transform.SetPositionAndRotation(visualiserData.Position, visualiserData.Rotation);

            if (!visualiser.gameObject.activeSelf)
                visualiser.gameObject.SetActive(true);
        }

        private void UpdateCameraTick(uint _)
        {
            if (!_isUpdating)
                return;

            Transform cameraTrf = null;
            if (Camera.current && Camera.current.transform.hasChanged)
                cameraTrf = Camera.current.transform;
            
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView.camera.transform.hasChanged)
                cameraTrf = SceneView.lastActiveSceneView.camera.transform;
#endif
            
            if (cameraTrf == null) return;

            ClientVisualiserData clientVisualiserData = new(cameraTrf.position, cameraTrf.rotation);
            Writer writer = new();
            ClientVisualiserData.WriteClientVisualiserData(writer, clientVisualiserData);
            _networkManager.Client.SendByteDataToAll("Visualiser", writer.GetBuffer(), ENetworkChannel.UnreliableOrdered);
            cameraTrf.hasChanged = false;
        }

        #endregion
    }
}
