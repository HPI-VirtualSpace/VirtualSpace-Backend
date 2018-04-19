using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker : NetworkingBaseServer
    {
        private static BackendWorker _instance;
        private Thread _workerThread;
        private List<ManagedAllocationIntersection> _intersections;
        private ConcurrentDictionary<int, int> _sessionIds;
        private NetworkEventHandler _networkEventHandler = new NetworkEventHandler();
        
        private readonly Task _sendTimeTask;
        private readonly Task _sendFrontendTask;

        public bool _standardIntersectionStrategyHasChanged;
        public bool StrategyIsUpToDate => !_standardIntersectionStrategyHasChanged;

        public readonly MetricLogger MetricLogger;

        private BackendWorker()
        {
            MetricLogger = new MetricLogger(PlayerData.Instance, EventMap.Instance);
            MetricLogger.StartNewLogging();

            _sessionIds = new ConcurrentDictionary<int, int>();
            _setEventHandler();
            
            _intersections = new List<ManagedAllocationIntersection>();
            
            _workerThread = new Thread(_workerLoop);
            _workerThread.Priority = ThreadPriority.AboveNormal;

            _sendTimeTask = new Task(_sendTimeLoop);
            _sendFrontendTask = new Task(_sendFrontendLoop);

            _sendTimeTask.Start();
            _sendFrontendTask.Start();

            Logger.AddPrinter(new ConsolePrinter());
            //EventMap.Instance.InitializeLogging();
        }

        private void _workerLoop()
        {
            StrategyManager.Instance.ChangeActiveStrategy();

            while (true)
            {
                _waitForNextTurn();
                // send state to logger
                _BeforeUpdate();
                _UpdateLoop();
                MetricLogger.LogMetrics();
                _AfterUpdate();
            }
        }

        private void _waitForNextTurn()
        {
            if (Time.MillisecondsForLastTurn < Config.TurnTimeMs)
            {
                double timeToNextTurn = Config.TurnTimeMs - Time.MillisecondsForLastTurn;
                if (timeToNextTurn >= 10)
                    Thread.Sleep((int)timeToNextTurn - 10);
            }
        }

        private float TimeoutSeconds = 120f;
        private void _BeforeUpdate()
        {
            Time.UpdateFrameStart();

            var playerIds = PlayerData.Instance.GetKeys();
            foreach (int playerId in playerIds)
            {
                if (LastMessageSecondsAgo(playerId) > TimeoutSeconds)
                {
                    Logger.Debug($"Player {playerId} timed out");
                    RemovePlayer(new Deregistration {UserId = playerId});
                }
            }
        }

        private float LastMessageSecondsAgo(int playerId)
        {
            if (!_sessionIds.ContainsKey(playerId))
                return float.MaxValue;
            return LastMessageSecondsAgoSessionId(_sessionIds[playerId]);
        }

        private void _UpdateLoop()
        {
            if (_standardIntersectionStrategyHasChanged)
            {
                lock (_intersectionLock)
                {
                    _standardIntersectionStrategyHasChanged = false;
                }
            }

            StrategyManager.Instance.ActiveStrategy?.UpdateState();
            StrategyManager.Instance.ActiveStrategy?.UpdateIncentives();

            //StrategyManager.Instance.Fallback.GetIncentivesForPlayers();

            /* send incentives */
            List<TimedEvent> eventsToSendToFrontend = new List<TimedEvent>();
            foreach (KeyValuePair<int, Incentives> playerIncentivesPair in EventBundler.Instance.GetIdToIncentivesPairs())
            {
                eventsToSendToFrontend.AddRange(playerIncentivesPair.Value.Events);

                if (PlayerData.Instance.TryGetEntry(playerIncentivesPair.Key, out PlayerDataEntry entry))
                {
                    Incentives incentivesToSend = playerIncentivesPair.Value;
                    incentivesToSend.UserId = playerIncentivesPair.Key;

                    entry.Incentives = playerIncentivesPair.Value;

                    SendReliable(incentivesToSend);
                }
            }
            
            //FrontendAccess.Instance.AddEvents(eventsToSendToFrontend);
            if (eventsToSendToFrontend.Count > 0)
            {
                //Logger.Debug($"Sending {eventsToSendToFrontend.Count} to frontend");
                SendToFrontend(new Incentives { Events = eventsToSendToFrontend });
            }

            EventBundler.Instance.Reset();

            /* cleanup old allocations */
            EventMap.Instance.CleanupUntil(Time.CurrentTurn - 2);
        }

        private void _AfterUpdate()
        {
            Time.UpdateFrameEnd();
        }
    }   
}
