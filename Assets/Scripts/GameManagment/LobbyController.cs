using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameManagment
{
    /// <summary>
    /// Simple lobby UI controller: login/password, server selection, host/client start.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [Header("FishNet")]
        [SerializeField] private NetworkManager networkManager;

        [Header("UI")]
        [SerializeField] private TMP_InputField loginInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_Dropdown serverDropdown;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Servers")]
        [SerializeField] private LobbyServerEntry[] servers =
        {
            new LobbyServerEntry { DisplayName = "Localhost", Address = "localhost", Port = 7770 }
        };

        private void Awake()
        {
            if (networkManager == null)
                networkManager = FindObjectOfType<NetworkManager>();
        }

        private void OnEnable()
        {
            if (connectButton != null) connectButton.onClick.AddListener(ConnectToSelectedServer);
            if (hostButton != null) hostButton.onClick.AddListener(StartHost);

            if (networkManager != null)
            {
                networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
                networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            }

            RebuildServerDropdown();
        }

        private void OnDisable()
        {
            if (connectButton != null) connectButton.onClick.RemoveListener(ConnectToSelectedServer);
            if (hostButton != null) hostButton.onClick.RemoveListener(StartHost);

            if (networkManager != null)
            {
                networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            }
        }

        public void ConnectToSelectedServer()
        {
            if (!TryPrepareConnection(out LobbyServerEntry server))
                return;

            SetStatus($"Подключение к {server.Address}:{server.Port}...");
            networkManager.ClientManager.StartConnection(server.Address, server.Port);
        }

        public void StartHost()
        {
            if (!TryPrepareConnection(out LobbyServerEntry server))
                return;

            SetStatus($"Запуск сервера на порту {server.Port}...");
            bool serverStarted = networkManager.ServerManager.StartConnection(server.Port);
            if (!serverStarted)
            {
                SetStatus("Не удалось запустить сервер.");
                return;
            }

            networkManager.ClientManager.StartConnection("localhost", server.Port);
        }

        private bool TryPrepareConnection(out LobbyServerEntry server)
        {
            server = default;

            if (networkManager == null)
            {
                SetStatus("NetworkManager не найден в сцене.");
                return false;
            }

            string login = loginInput != null ? loginInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Введите логин и пароль.");
                return false;
            }

            if (!TryGetSelectedServer(out server))
            {
                SetStatus("Сервер не выбран или заполнен неверно.");
                return false;
            }

            LobbyCredentials.Set(login, password);
            return true;
        }

        private bool TryGetSelectedServer(out LobbyServerEntry server)
        {
            if (servers == null || servers.Length == 0)
                servers = new[] { LobbyServerEntry.Localhost() };

            int index = serverDropdown != null ? serverDropdown.value : 0;
            index = Mathf.Clamp(index, 0, servers.Length - 1);
            server = servers[index];
            return server.IsValid;
        }

        private void RebuildServerDropdown()
        {
            if (serverDropdown == null)
                return;

            serverDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            foreach (LobbyServerEntry server in servers)
                options.Add(server.Label);

            serverDropdown.AddOptions(options);
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    SetStatus("Подключено. Проверка аккаунта...");
                    break;
                case LocalConnectionState.Stopped:
                    SetStatus("Соединение остановлено.");
                    break;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                SetStatus("Сервер запущен.");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;

            Debug.Log(message);
        }
    }
}
