using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal class StrategyManager
    {
        private readonly object _activeLock = new object();
        private ConflictStrategy _activeStrategy;
        public static StrategyManager Instance { get; } = new StrategyManager();
        public ConflictStrategy ActiveStrategy => _activeStrategy;
        
        private StrategyManager()
        {

        }

        #region Modification
        internal ConflictStrategy ChangeActiveStrategy()
        {
            RemoveActiveStrategy();

            _activeStrategy = new StateStrategy(BackendWorker.GetInstance(), PlayerData.Instance);

            return _activeStrategy;
        }

        private void RemoveActiveStrategy()
        {
            if (_activeStrategy == null) return;

            var revokePerPlayer = EventMap.Instance.RevokeStrategyEvents(_activeStrategy.strategyId, Time.CurrentTurn);
            foreach (int playerId in revokePerPlayer.Keys)
            {
                if (revokePerPlayer.TryGetValue(playerId, out List<TimedEvent> events))
                {
                    Incentives incentives = new Incentives(events) { UserId = playerId };
                    BackendWorker.GetInstance().SendReliable(incentives);
                }
            }
            
            EventMap.Instance.CleanupWithStrategyId(_activeStrategy.strategyId);

            _activeStrategy.Deinitialize();
            _activeStrategy = null;
        }
        #endregion

        public void UpdatePlayerList()
        {
            lock (_activeLock)
                _activeStrategy?.UpdateUsersRequest();
        }
    }
}