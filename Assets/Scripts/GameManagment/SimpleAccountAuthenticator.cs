using System;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace GameManagment
{
    /// <summary>
    /// Minimal FishNet authenticator for lobby prototypes.
    /// Accounts are configured in the inspector and checked on the server.
    /// </summary>
    public sealed class SimpleAccountAuthenticator : Authenticator
    {
        [Serializable]
        private struct Account
        {
            public string Login;
            public string Password;
        }

        [SerializeField] private Account[] accounts =
        {
            new Account { Login = "test", Password = "1234" }
        };

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);

            NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            NetworkManager.ClientManager.RegisterBroadcast<LoginResultBroadcast>(OnLoginResultBroadcast);
            NetworkManager.ServerManager.RegisterBroadcast<LoginRequestBroadcast>(OnLoginRequestBroadcast, false);
        }

        private void OnDestroy()
        {
            if (NetworkManager == null)
                return;

            NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            NetworkManager.ClientManager.UnregisterBroadcast<LoginResultBroadcast>(OnLoginResultBroadcast);
            NetworkManager.ServerManager.UnregisterBroadcast<LoginRequestBroadcast>(OnLoginRequestBroadcast);
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            NetworkManager.ClientManager.Broadcast(new LoginRequestBroadcast
            {
                Login = LobbyCredentials.Login,
                Password = LobbyCredentials.Password
            });
        }

        private void OnLoginRequestBroadcast(NetworkConnection connection, LoginRequestBroadcast request, Channel channel)
        {
            if (connection.IsAuthenticated)
            {
                connection.Disconnect(true);
                return;
            }

            bool passed = IsValidAccount(request.Login, request.Password);
            string message = passed ? "Вход выполнен." : "Неверный логин или пароль.";

            NetworkManager.ServerManager.Broadcast(connection, new LoginResultBroadcast
            {
                Passed = passed,
                Message = message
            }, false);

            OnAuthenticationResult?.Invoke(connection, passed);
        }

        private void OnLoginResultBroadcast(LoginResultBroadcast result, Channel channel)
        {
            LobbyCredentials.ClearPassword();
            Debug.Log(result.Message);
        }

        private bool IsValidAccount(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return false;

            foreach (Account account in accounts)
            {
                if (string.Equals(account.Login, login, StringComparison.Ordinal) &&
                    string.Equals(account.Password, password, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
