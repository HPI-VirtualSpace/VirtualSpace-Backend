using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class EventBundler
    {
        private readonly Dictionary<int, Incentives> _idToIncentives;
        private readonly List<int> _criticalPlayers;
        public static EventBundler Instance { get; } = new EventBundler();

        public EventBundler()
        {
            _idToIncentives = new Dictionary<int, Incentives>();
            _criticalPlayers = new List<int>();
        }

        public void AddEvent(int playerId, TimedEvent newEvent)
        {
            if (playerId < 0)
            {
                Logger.Warn("Event doesn't have a valid player id.");
                return;
            }

            Incentives playerIncentives = _GetPlayerIncentives(newEvent.PlayerId);
            if (!playerIncentives.Events.Contains(newEvent))
                playerIncentives.Events.Add(newEvent);
        }

        public bool Contains(TimedEvent event_)
        {
            Incentives playerIncentives = _GetPlayerIncentives(event_.PlayerId);
            return playerIncentives.Events.Contains(event_);
        }

        public void AddEvent(TimedEvent newEvent)
        {
            AddEvent(newEvent.PlayerId, newEvent);
        }

        public void AddEvents(List<TimedEvent> newEvents)
        {
            newEvents.ForEach(newEvent => AddEvent(newEvent.PlayerId, newEvent));
        }

        public void AddCriticalPlayer(int playerId)
        {
            _criticalPlayers.AddIfNotContained(playerId);
        }

        private Incentives _GetPlayerIncentives(int playerId)
        {
            Incentives playerIncentives;

            if (!_idToIncentives.TryGetValue(playerId, out playerIncentives))
            {
                playerIncentives = new Incentives();
                _idToIncentives[playerId] = playerIncentives;
            }

            return playerIncentives;
        }
        
        public Dictionary<int, Incentives> GetIdToIncentivesPairs()
        {
            return _idToIncentives;
        }

        public List<int> GetCriticalPlayers()
        {
            return _criticalPlayers;
        }

        public void Reset()
        {
            _idToIncentives.Clear();
            _criticalPlayers.Clear();
        }
    }
}
