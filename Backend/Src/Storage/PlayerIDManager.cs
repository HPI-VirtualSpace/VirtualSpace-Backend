using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal static class PlayerIDManager
    {
        private static Mutex _lock = new Mutex();
        private static List<int> _available = new List<int>();
        private static List<int> _used = new List<int>();

        static PlayerIDManager()
        {
            for (int index = 0; index < Config.MaxPlayers; index++)
                _available.Add(index);
        }

        public static int GetID()
        {
            if(_available.Count == 0) return -1;
            _lock.WaitOne();
            int id = _available.First();
            _available.Remove(id);
            _used.Add(id);
            _lock.ReleaseMutex();
            return id;
        }

        public static bool RequestID(int id)
        {
            _lock.WaitOne();
            if(!_available.Contains(id))
            {
                _lock.ReleaseMutex();
                return false;
            }
            _available.Remove(id);
            _used.Add(id);
            _lock.ReleaseMutex();
            return true;
        }

        public static void FreeID(int id)
        {
            _lock.WaitOne();
            if (!_used.Contains(id))
            {
                Logger.Warn("Tried to release ID which wasn't in use.");
                _lock.ReleaseMutex();
                return;
            }
            _used.Remove(id);
            _available.Add(id);
            _lock.ReleaseMutex();
        }

        public static bool IsAvailable(int id)
        {
            _lock.WaitOne();
            bool result = !_available.Contains(id);
            _lock.ReleaseMutex();
            return result;
        }

    }
}
