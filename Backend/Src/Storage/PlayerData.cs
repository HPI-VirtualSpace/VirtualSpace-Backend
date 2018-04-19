using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class PlayerData : IPlayerData
    {
        #region Instance
        public static PlayerData Instance { get; } = new PlayerData();

        private PlayerData()
        {
            
        }
        #endregion

        #region State
        private static ConcurrentDictionary<int, PlayerDataEntry> _dict = new ConcurrentDictionary<int, PlayerDataEntry>();
        #endregion

        #region EntryManagement
        public bool AddPlayer(int playerID, string userName)
        {
            bool success = _dict.TryAdd(playerID, new PlayerDataEntry(userName));
            return success;
        }

        public bool RemovePlayer(int playerID)
        {
            bool success = _dict.TryRemove(playerID, out _);
            return success;
        }
        
        public PlayerDataEntry GetEntry(int playerID)
        {
            _dict.TryGetValue(playerID, out PlayerDataEntry entry);
            return entry;
        }

        public bool TryGetEntry(int playerId, out PlayerDataEntry entry)
        {
            return _dict.TryGetValue(playerId, out entry);
        }
        

        public List<int> GetKeys()
        {
            return _dict.Keys.ToList();
        }

        public List<int> GetKeysWithAllocation()
        {
            return _dict.Keys.Where(key => GetEntry(key).Allocation != null).ToList();
        }
        #endregion

        #region Helper
        public List<Vector> GetCurrentPositions()
        {
            List<Vector> positions = new List<Vector>(_dict.Count);

            foreach (PlayerDataEntry entry in _dict.Values)
            {
                positions.Add(entry.Position);
            }

            return positions;
        }

        public List<Allocation> GetAllocations()
        {
            List<Allocation> allocations = new List<Allocation>(_dict.Count);

            foreach (PlayerDataEntry entry in _dict.Values)
            {
                allocations.Add(entry.Allocation);
            }

            return allocations;
        }
        #endregion
    }
}
