using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker : NetworkingBaseServer
    {
        public static void Start()
        {
            _instance._workerThread.Start();
            _instance.StartListening();
            //_instance._StartListening();
        }

        public static void Join()
        {
            _instance._workerThread.Join();
        }

        public static void CreateInstance()
        {
            if (_instance == null)
                _instance = new BackendWorker();
        }

        public static BackendWorker GetInstance()
        {
            return _instance;
        }
    }
}
