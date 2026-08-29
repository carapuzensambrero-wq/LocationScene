using System;
using UnityEngine;

namespace GameManagment
{
    [Serializable]
    public struct LobbyServerEntry
    {
        public string DisplayName;
        public string Address;
        public ushort Port;

        public string Label => string.IsNullOrWhiteSpace(DisplayName)
            ? $"{Address}:{Port}"
            : $"{DisplayName} ({Address}:{Port})";

        public bool IsValid => !string.IsNullOrWhiteSpace(Address) && Port > 0;

        public static LobbyServerEntry Localhost(ushort port = 7770)
        {
            return new LobbyServerEntry
            {
                DisplayName = "Localhost",
                Address = "localhost",
                Port = port
            };
        }
    }
}
