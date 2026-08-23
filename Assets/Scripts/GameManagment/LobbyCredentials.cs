namespace GameManagment
{
    /// <summary>
    /// Runtime credentials entered in lobby before the FishNet connection starts.
    /// This is a prototype-only holder; do not store real passwords this way in production.
    /// </summary>
    public static class LobbyCredentials
    {
        public static string Login { get; private set; } = string.Empty;
        public static string Password { get; private set; } = string.Empty;

        public static void Set(string login, string password)
        {
            Login = login ?? string.Empty;
            Password = password ?? string.Empty;
        }

        public static void ClearPassword()
        {
            Password = string.Empty;
        }
    }
}
