using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.UI;

namespace GameManagment
{
    /// <summary>
    /// Server-side scene switch button. Use it after host/server is started.
    /// </summary>
    public sealed class NetworkSceneLoader : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string gameSceneName = "Locacion";
        [SerializeField] private Button loadGameButton;
        [SerializeField] private bool replaceAllScenes = true;

        private void Awake()
        {
            if (networkManager == null)
                networkManager = FindObjectOfType<NetworkManager>();
        }

        private void OnEnable()
        {
            if (loadGameButton != null)
                loadGameButton.onClick.AddListener(LoadGameScene);
        }

        private void OnDisable()
        {
            if (loadGameButton != null)
                loadGameButton.onClick.RemoveListener(LoadGameScene);
        }

        public void LoadGameScene()
        {
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager не найден в сцене.");
                return;
            }

            if (!networkManager.IsServerStarted)
            {
                Debug.LogWarning("Переход в игровую сцену должен запускать сервер или host.");
                return;
            }

            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("Имя игровой сцены не задано.");
                return;
            }

            SceneLoadData loadData = new SceneLoadData(gameSceneName)
            {
                ReplaceScenes = replaceAllScenes ? ReplaceOption.All : ReplaceOption.OnlineOnly
            };

            networkManager.SceneManager.LoadGlobalScenes(loadData);
        }
    }
}
