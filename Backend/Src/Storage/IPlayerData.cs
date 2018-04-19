using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public interface IPlayerData
    {
        bool AddPlayer(int playerID, string userName);
        bool RemovePlayer(int playerID);
        PlayerDataEntry GetEntry(int playerID);
        bool TryGetEntry(int playerId, out PlayerDataEntry entry);
        List<int> GetKeys();
        List<Vector> GetCurrentPositions();
        List<Allocation> GetAllocations();
    }
}