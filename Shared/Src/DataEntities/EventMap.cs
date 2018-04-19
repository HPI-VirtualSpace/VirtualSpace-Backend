using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VirtualSpace.Shared
{
    public class EventMap
    {
        private static EventMap _Instance = new EventMap();
        public static EventMap Instance { get { return _Instance; } }

        public List<TimedEvent> Events = new List<TimedEvent>();

        private bool _logToFile;
        private string _logFileName;

        private EventMap()
        {
            
        }

        public void InitializeLogging()
        {
            _logToFile = true;

            string dateString = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            _logFileName = dateString + " Events.binary";
        }

        public void CleanupUntil(long minTurnEnd)
        {
            lock (Events)
            {
                var eventsToDelete = Events.Where(event_ => event_.TurnEnd < minTurnEnd).ToList();

                // save to file 
                if (_logToFile)
                {
                    byte[] bytes = ProtobufUtility.Serialize(eventsToDelete);
                    byte[] lengthBytes = BitConverter.GetBytes(bytes.Length);

                    File.WriteAllBytes(_logFileName, lengthBytes); // overwrites!
                    File.WriteAllBytes(_logFileName, bytes); // overwrites!
                }

                Events.RemoveAll(event_ => event_.TurnEnd < minTurnEnd);
            }
        }

        public void CleanupSendBefore(long minTurnSend)
        {
            lock (Events)
            {
                Events.RemoveAll(event_ => event_.TurnStart < minTurnSend);

            }
        }

        public void CleanupWithStrategyId(int strategyId)
        {
            lock (Events) {
                Events.RemoveAll(event_ => event_.StrategyId == strategyId);
            }
        }

        public void AddOrModifyEvent(TimedEvent newEvent)
        {
            lock (Events)
            {
                TimedEvent eventToOverride = GetEvent(newEvent.StrategyId, newEvent.Id);
                if (eventToOverride == null)
                {
                    Events.Add(newEvent);
                }
                else if (newEvent.GetType() == typeof(RevokeEvent))
                {
                    Events.Remove(eventToOverride);
                } else { 
                    eventToOverride.OverrideWith(newEvent);
                }
            }
        }

        public void AddOrModifyEvents(List<TimedEvent> events)
        {
            lock (Events)
                foreach (var event_ in events)
                    AddOrModifyEvent(event_);
        }

        public List<TimedEvent> GetAllEvents()
        {
            lock (Events)
                return Events.ToList();
        }

        public List<TimedEvent> GetEventsForStrategyId(int strategyId)
        {
            lock (Events)
                return Events.Where(event_ => event_.StrategyId == strategyId).ToList();
        }

        public List<TimedEvent> GetEventsForPlayerId(int playerId)
        {
            lock (Events)
                return Events.Where(event_ => event_.PlayerId == playerId).ToList();
        }

        public TimedEvent GetEvent(long strategyId, long id)
        {
            lock (Events)
                return Events.Find(event_ => (event_.Id == id) && (event_.StrategyId == strategyId));
        }

        public void RemoveEvent(long strategyId, long id)
        {
            lock (Events)
                Events.RemoveAll(event_ => (event_.Id == id) && (event_.StrategyId == strategyId));
        }

        public List<TimedEvent> GetEventsAtTurnOfTypes(long turn, IncentiveType[] types)
        {
            lock (Events)
                return Events.Where(potentialEvent => potentialEvent.IsActiveAt(turn) && types.Contains(potentialEvent.Type)).ToList();
        }

        public List<TimedEvent> GetEventsFromToOfTypes(long fromTurn, long toTurn, IncentiveType[] types)
        {
            lock (Events)
                return Events.Where(potentialEvent => 
                    potentialEvent.IsActiveInBetween(fromTurn, toTurn) && types.Contains(potentialEvent.Type)).ToList();
        }

        public List<TimedEvent> GetEventsForTurn(long turn)
        {
            lock (Events)
                return Events.Where(event_ => event_.TurnStart <= turn && turn <= event_.TurnEnd).ToList();
        }

        public List<TimedEvent> GetEventsForFromTo(long turnStart, long turnEnd)
        {
            lock (Events)
                return Events.Where(
                    event_ => event_.IsActiveInBetween(turnStart, turnEnd)).ToList();
        }

        public List<TimedEvent> GetEventsForFromTo(long turnStart, long turnEnd, int strategyId)
        {
            lock (Events)
                return GetEventsForFromTo(turnStart, turnEnd).Where(event_ => event_.StrategyId == strategyId).ToList();
        }

        public List<TimedEvent> GetEventsForFromTo(long turnStart, long turnEnd, Type type)
        {
            lock (Events)
                return GetEventsForFromTo(turnStart, turnEnd).Where(event_ => event_ .GetType() == type).ToList();
        }

        public List<TimedEvent> GetEventsForTurn(long turn, Type type)
        {
            lock (Events)
                return GetEventsForTurn(turn).Where(event_ => event_.GetType() == type).ToList();
        }

        public List<TimedEvent> GetEventsForTurnExpandingTransitions(long turn, EventType eventType, IncentiveType incentiveType)
        {
            return GetEventsForTurnExpandingTransitions(turn, new[] {eventType}, new[] {incentiveType});
        }

        public List<TimedEvent> GetEventsForTurnExpandingTransitions(long turn, EventType[] eventTypes, IncentiveType[] incentiveTypes)
        {
            return GetEventsForTurnExpandingTransitions(turn, turn, eventTypes, incentiveTypes);
        }

        public List<TimedEvent> GetEventsForTurnExpandingTransitions(long fromTurn, long toTurn, EventType[] eventTypes, IncentiveType[] incentiveTypes)
        {
            lock (Events)
            {
                bool getPositions = eventTypes.Contains(EventType.Position);
                bool getAreas = eventTypes.Contains(EventType.Area);
                bool expandTransitions = getPositions || getAreas;

                List<TimedEvent> returnEvents = null;
                if (expandTransitions)
                {
                    List<TimedEvent> unexpandedEvents = GetEventsForFromTo(fromTurn, toTurn).Where(event_ =>
                        eventTypes.Contains(event_.EventType) || event_.EventType == EventType.Transition).ToList();

                    returnEvents = new List<TimedEvent>();
                    foreach (TimedEvent relevantEvent in unexpandedEvents)
                    {
                        if (relevantEvent.EventType == EventType.Transition)
                        {
                            Transition transition = relevantEvent as Transition;
                            foreach (TransitionFrame frame in transition.GetActiveBetween(fromTurn, toTurn))
                            {
                                if (getPositions)
                                    returnEvents.Add(frame.Position);
                                if (getAreas && frame.Area != null)
                                    returnEvents.Add(frame.Area);
                            }
                        }
                        else
                        {
                            returnEvents.Add(relevantEvent);
                        }
                    }

                }
                else
                {
                    returnEvents = GetEventsForFromTo(fromTurn, toTurn).Where(event_ =>
                        eventTypes.Contains(event_.EventType) && incentiveTypes.Contains(event_.Type)).ToList();
                }
                
                return returnEvents;
            }
        }

        public void Remove(TimedEvent eventToRevoke)
        {
            lock (Events)
                Events.Remove(eventToRevoke);
        }

        public List<TimedEvent> RevokeAndReturnUserEvents(int playerId, long now)
        {
            List<TimedEvent> events = new List<TimedEvent>();

            foreach (TimedEvent te in GetEventsForPlayerId(playerId))
            {
                te.TurnEnd = now - 1;

                events.Add(te);
            }

            return events;
        }

        public Dictionary<int, List<TimedEvent>> RevokeStrategyEvents(int activeStrategyStrategyId, long now)
        {
            Dictionary<int, List<TimedEvent>> eventsPerPlayer = new Dictionary<int, List<TimedEvent>>();



            foreach (TimedEvent te in GetEventsForStrategyId(activeStrategyStrategyId))
            {
                RevokeEvent revokeEvent = new RevokeEvent(now, te.TurnEnd, te.Id, te.PlayerId)
                {
                    StrategyId = te.StrategyId
                };

                // add player if not already there
                List<TimedEvent> events;
                bool success = eventsPerPlayer.TryGetValue(te.PlayerId, out events);
                if (!success)
                {
                    events = new List<TimedEvent>();
                    eventsPerPlayer.Add(te.PlayerId, events);
                }

                events.Add(revokeEvent);
            }
            
            return eventsPerPlayer;
        }


    }
}
