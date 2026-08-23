using FishNet.Broadcast;

namespace GameManagment
{
    public struct LoginRequestBroadcast : IBroadcast
    {
        public string Login;
        public string Password;
    }

    public struct LoginResultBroadcast : IBroadcast
    {
        public bool Passed;
        public string Message;
    }
}
