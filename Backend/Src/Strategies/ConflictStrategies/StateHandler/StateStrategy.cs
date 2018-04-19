    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel.Configuration;
    using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    partial class StateStrategy : ConflictStrategy
    {
        #region Variables
        #region Dependencies
        private readonly SystemState _state;
        private readonly VoronoiSplitter _splitter;
        private readonly BackendWorker _worker;
        private readonly PlayerData _playerData;
        #endregion

        #region Player
        private List<int> _userIds;
        // convienience
        private List<int> _userNums;
        private int _numUsers;
        #endregion

        #region Settings
        private readonly double _regularOffsetFromBounds = .5;
        private readonly double _defocusOffsetFromBounds = .25;
        #endregion

        #region EventCreation
        private long _earliestPossibleNextTransition = -1;
        private long _startTurnOfInfiniteEvent = -1;
        public float NextEarliestStartSeconds => Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent);
        private int _lastTransitionId = 0;
        private static readonly long TurnExecutionDelay = 2;
        #endregion

        #region StateHandling
        private long _compareTransitionValuesNoEarlierThan = -1;
        private long MinTurnsStayInState = 0;
        private float minExecutionMs = 360;
        private long MinPreperationTurns = 6;
        #endregion

        #region Logging
        private MetricLogger _metricLogger;
        #endregion

        #region Voting
        private TimeMatcher _timeMatcher;
        private VotingMechanism _votingMechanism;
        ConcurrentDictionary<int, TransitionVoting> votings = new ConcurrentDictionary<int, TransitionVoting>();
        private long _betweenFailuresTurns = Time.ConvertSecondsToTurns(1f);
        #endregion

        #region Ticks
        private readonly TickManager _tickManager;
        #endregion

        #region Helper
        // aggregation
        private static readonly StateTransition[] UserIndexToSystemFocusStateMapping =
                {StateTransition.Focus1, StateTransition.Focus2, StateTransition.Focus3, StateTransition.Focus4};
        private static readonly Dictionary<VSUserTransition, StateTransition> FocusUserStateToSystemStateMapping = new Dictionary<VSUserTransition, StateTransition>
        {
            {VSUserTransition.Undefocus, StateTransition.Unfocus},
            {VSUserTransition.Unfocus, StateTransition.Unfocus},
            {VSUserTransition.Stay, StateTransition.Stay}
        };

        Dictionary<StateTransition, int> transitionToMinimumNumberOfActors = new Dictionary<StateTransition, int>()
        {
            { StateTransition.Switch1, 2 },
            { StateTransition.Switch2, 2 },
            { StateTransition.Switch3, 2 },
            { StateTransition.Switch4, 2 }
        };

        private static readonly Dictionary<VSUserTransition, StateTransition> DefaultUserStateToSystemStateMapping
            = new Dictionary<VSUserTransition, StateTransition>
            {
                {VSUserTransition.RotateLeft, StateTransition.RotateLeft},
                {VSUserTransition.RotateRight, StateTransition.RotateRight},
                {VSUserTransition.Rotate45Left, StateTransition.Rotate45Left},
                {VSUserTransition.Rotate45Right, StateTransition.Rotate45Right},
                {VSUserTransition.Stay, StateTransition.Stay}
            };
        private static readonly StateTransition[] UserIndexToSystemSwitchStateMapping =
            {StateTransition.Switch1, StateTransition.Switch2, StateTransition.Switch3, StateTransition.Switch4};
        #endregion

        #region InPlanning
        List<TransitionInfo>[] _transitionsPerUser;
        double NowMs => Time.NowMilliseconds;
        double NowSeconds => Time.NowSeconds;
        int NumTransitionsInQueue(int userNum) =>
            _transitionsPerUser[userNum].Count(info => info.FromSeconds > NowSeconds);
        int CurrentNumTransitionsInQueue =>
            _transitionsPerUser.Max(user => user.Count(info => info.FromSeconds > NowSeconds));
        #endregion
        #endregion
        
        #region CreationDestruction
        public StateStrategy(BackendWorker worker, PlayerData playerData)
        {
            Polygon intersectionArea = Config.PlayArea;

            //Logger.Debug($"Creating new state strategy for {playerIds.Count}");
            _metricLogger = worker.MetricLogger;
            
            _worker = worker;
            _playerData = playerData;
            _splitter = new VoronoiSplitter(intersectionArea);
            
            Tuple<Vector, Vector> bounds = intersectionArea.EnclosingVectors;
            double minX = bounds.Item1.X;
            double minY = bounds.Item1.Z;
            double maxX = bounds.Item2.X;
            double maxY = bounds.Item2.Z;

            _state = new SystemState(_splitter);
            _state.InitializeStatePositions(minX, minY, maxX, maxY, _regularOffsetFromBounds, _defocusOffsetFromBounds);

            //_includeTransitions.AddRange(_rotationStates);
            //_includeTransitions.Add(StateTransition.AssymmetricRotation);

            _worker.AddHandler(typeof(StrategySettings), OnStrategySettings);
            _worker.AddHandler(typeof(TransitionVoting), OnTransitionVoting);
            _worker.AddHandler(typeof(StateHandlerProperties), OnStateHandlingProperties);
            _worker.AddHandler(typeof(Tick), OnTick);

            _votingMechanism = new AverageFairness(0, false);
            //_votingMechanism = new Choreography();
            //_timeMatcher = new BucketTimeMatcher(0, _maximumUserPlanningOffsetMs, _maximumUserExecutionOffsetMs);
            _timeMatcher = new ConditionTimeMatcher(0);
            _tickManager = new TickManager();

            
            _userIds = new List<int>();
            _userNums = new List<int>();
        }

        private object _updateLock = new object();
        private bool _updatePlayers = false;
        public override void UpdateUsersRequest()
        {
            lock (_updateLock)
                _updatePlayers = true;
        }

        public void OnTick(IMessageBase message)
        {
            
        }

        public StrategySettings CurrentSettings = StrategySettings.Default;
        
        public void OnStrategySettings(IMessageBase messageBase)
        {
            var settings = (StrategySettings) messageBase;

            if (CurrentSettings.VersionNumber == settings.VersionNumber)
            {
                Logger.Debug("Received new settings from frontend");
                CurrentSettings = settings;

                UpdateQueueLength();
            }
        }

        private void UpdatePlayerListDelayed(bool onlyRemove)
        {
            var oldPlayerIds = _userIds;
            var updatedPlayerIds = _playerData.GetKeysWithAllocation();

            var oldNumUsers = _numUsers;
            var newNumUsers = updatedPlayerIds.Count;

            var idsToRemove = oldPlayerIds.Except(updatedPlayerIds).ToList();
            var idsToAdd = updatedPlayerIds.Except(oldPlayerIds).ToList();
            if (onlyRemove) idsToAdd.Clear();
            // need to preserve order here
            var idsSameOrderedByJoinTime = oldPlayerIds.Intersect(updatedPlayerIds).ToList();

            // need to remove them in order because the referenced structures are order and not id aware
            var userNumsToRemoveLargeToSmall = idsToRemove.Select(id => _userIds.IndexOf(id)).ToList();
            userNumsToRemoveLargeToSmall.Sort((a, b) => b.CompareTo(a)); 
            
            var userNumsToIndices = _state.GetCurrentUserNumToIndex();
            
            foreach (var userNum in userNumsToRemoveLargeToSmall)
            {
                userNumsToIndices.RemoveAt(userNum);
                _votingMechanism.RemoveUser(userNum);
                _tickManager.Remove(userNum);

                // remove player events
                var revokeEvents = EventMap.Instance.RevokeAndReturnUserEvents(_userIds[userNum], Time.CurrentTurn);
                EventBundler.Instance.AddEvents(revokeEvents);
            }
            
            _userIds = idsSameOrderedByJoinTime.ToList();
            _userIds.AddRange(idsToAdd);
            _numUsers = _userIds.Count;

            _timeMatcher.UpdateNumUser(_numUsers);

            // determine free state indices 
            var freeIndices = Enumerable.Range(0, 8).Except(userNumsToIndices).ToList();
            var numFree = freeIndices.Count;

            var freeIndicesWithBuffer = new List<int>();
            for (int i = 0; i < numFree; i++)
            {
                var beforeI = (i + numFree - 1) % numFree;
                var afterI = (i + 1) % numFree;
                var expectedBefore = (freeIndices[i] - 1 + 8) % 8;
                var expectedAfter = (freeIndices[i] + 1) % 8;

                if (freeIndices[beforeI] == expectedBefore && expectedAfter == freeIndices[afterI])
                {
                    freeIndicesWithBuffer.Add(freeIndices[i]);
                }
            }
            
            var numToAdd = idsToAdd.Count;
            for (int i = 0; i < numToAdd; i++)
            {
                _votingMechanism.AddUser();
                _tickManager.Add();

                // get furthest away from anyone
                var userIndex = -1;
                if (!userNumsToIndices.Any())
                {
                    userIndex = freeIndicesWithBuffer.First();
                } else
                {
                    var maxMinDist = int.MinValue;
                    var isEven = userNumsToIndices.First() % 2 == 0;

                    foreach (var freeIndex in freeIndicesWithBuffer)
                    {
                        if (isEven && freeIndex % 2 != 0) continue;
                        if (!isEven && freeIndex % 2 == 0) continue;

                        var minDistToOther = userNumsToIndices.Min(usedIndex => Math.Min(Math.Abs(freeIndex - usedIndex), 8 - Math.Abs(freeIndex - usedIndex)));
                        if (maxMinDist < minDistToOther)
                        {
                            userIndex = freeIndex;
                            maxMinDist = minDistToOther;
                        }
                    }
                }
                
                // remove before, same and after
                freeIndicesWithBuffer.RemoveAll(index => userIndex <= index - 1 && userIndex <= index + 1);

                userNumsToIndices.Add(userIndex);
            }
            
            _state.SetInitialUserNumToIndex(userNumsToIndices.ToArray());
            
            _userNums = Enumerable.Range(0, _numUsers).ToList();

            UpdatePriorities();

            var transitionsPerUser = new List<TransitionInfo>[_numUsers];
            for (int userNum = 0; userNum < _numUsers; userNum++)
            {
                var idAtPosition = _userIds[userNum];
                var oldNum = oldPlayerIds.IndexOf(idAtPosition);

                transitionsPerUser[userNum] = oldNum < 0 ? new List<TransitionInfo>() : _transitionsPerUser[oldNum];
            }
            _transitionsPerUser = transitionsPerUser;
            
            lock (_lastStateInfos)
            {
                _lastStateInfos.Clear();
                foreach (var id in idsToAdd)
                {
                    var playerNum = _userIds.IndexOf(id);

                    StateInfo playerInfo = CreateStateInfo(playerNum);
                    _lastStateInfos.Add(playerInfo);

                    Logger.Debug("Sending initial state info to player " + playerNum);
                    _worker.SendReliable(playerInfo);
                }
            }
        }
        
        private void UpdatePriorities()
        {
            var MovePriorities = new List<float>();
            var TimePriorities = new List<float>();

            _userNums.ForEach(userNum =>
            {
                var containsPreferences = _userProperties.ContainsKey(_userIds[userNum]);
                MovePriorities.Add
                    (containsPreferences ? _userProperties[_userIds[userNum]].MovePriority : 1f);
                TimePriorities.Add
                    (containsPreferences ? _userProperties[_userIds[userNum]].TimePriority : 1f);
            });

            _votingMechanism.SetUserPriorities(MovePriorities);
            _timeMatcher.SetUserPriorities(TimePriorities);
        }

        private int[] FindInitialStateArrangement(Vector[] startPositions)
        {
            var initialPlayerNumToIndex = new int[_numUsers];
            var mustCenters = new Vector[_numUsers];
            for (int i = 0; i < _numUsers; i++)
            {
                initialPlayerNumToIndex[i] = i;

                int playerId = _userIds[i];

                if (PlayerData.Instance.TryGetEntry(playerId, out PlayerDataEntry entry))
                {
                    var allocation = entry.Allocation;
                    var mustCenter = allocation.GlobalMustHave.Center;
                    mustCenters[i] = mustCenter;
                }
                else
                {
                    Logger.Warn($"Initial Arrangement: {playerId} not registered anymore");
                }
            }

            double minDistance = double.MaxValue;
            int[] minInitialPlacement = null;
            do
            {
                double totalDistance = 0;

                for (int userNum = 0; userNum < _numUsers; userNum++)
                {
                    var startPosition = startPositions[initialPlayerNumToIndex[userNum]];
                    var distance = startPosition.Distance(mustCenters[userNum]);
                    totalDistance += distance;
                }

                if (totalDistance < minDistance)
                {
                    minDistance = totalDistance;
                    minInitialPlacement = (int[])initialPlayerNumToIndex.Clone();
                }
            } while (!NextPermutation(initialPlayerNumToIndex));

            for (int i = 0; i < minInitialPlacement.Length; i++)
            {
                minInitialPlacement[i] = minInitialPlacement[i] * 2;
            }

            return minInitialPlacement;
        }

        public static bool NextPermutation<T>(T[] elements) where T : IComparable<T>
        {
            // More efficient to have a variable instead of accessing a property
            var count = elements.Length;

            // Indicates whether this is the last lexicographic permutation
            var done = true;

            // Go through the array from last to first
            for (var i = count - 1; i > 0; i--)
            {
                var curr = elements[i];

                // Check if the current element is less than the one before it
                if (curr.CompareTo(elements[i - 1]) < 0)
                {
                    continue;
                }

                // An element bigger than the one before it has been found,
                // so this isn't the last lexicographic permutation.
                done = false;

                // Save the previous (smaller) element in a variable for more efficiency.
                var prev = elements[i - 1];

                // Have a variable to hold the index of the element to swap
                // with the previous element (the to-swap element would be
                // the smallest element that comes after the previous element
                // and is bigger than the previous element), initializing it
                // as the current index of the current item (curr).
                var currIndex = i;

                // Go through the array from the element after the current one to last
                for (var j = i + 1; j < count; j++)
                {
                    // Save into variable for more efficiency
                    var tmp = elements[j];

                    // Check if tmp suits the "next swap" conditions:
                    // Smallest, but bigger than the "prev" element
                    if (tmp.CompareTo(curr) < 0 && tmp.CompareTo(prev) > 0)
                    {
                        curr = tmp;
                        currIndex = j;
                    }
                }

                // Swap the "prev" with the new "curr" (the swap-with element)
                elements[currIndex] = prev;
                elements[i - 1] = curr;

                // Reverse the order of the tail, in order to reset it's lexicographic order
                for (var j = count - 1; j > i; j--, i++)
                {
                    var tmp = elements[j];
                    elements[j] = elements[i];
                    elements[i] = tmp;
                }

                // Break since we have got the next permutation
                // The reason to have all the logic inside the loop is
                // to prevent the need of an extra variable indicating "i" when
                // the next needed swap is found (moving "i" outside the loop is a
                // bad practice, and isn't very readable, so I preferred not doing
                // that as well).
                break;
            }

            // Return whether this has been the last lexicographic permutation.
            return done;
        }

        public void LogAppNames()
        {
            foreach (int playerId in _userIds)
            {
                if (PlayerData.Instance.TryGetEntry(playerId, out PlayerDataEntry entry))
                {
                    _metricLogger.Log(playerId + " registered as " + entry.Preferences.SceneName);
                    _metricLogger.Log(playerId + " has rotation " + entry.Allocation.Rotation * 180 / Math.PI);
                    _metricLogger.Log(playerId + " has user name " + entry.UserName);
                }
            }
        }

        public void Reset()
        {
            ResetEvents();
            _state.Reset();
            _tickManager.Reset();
            _votingMechanism.Reset();
            _timeMatcher.Reset();
            votings.Clear();
            SendInitialState();

            CurrentSettings.Reset = false;
            CurrentSettings.VersionNumber++;
            UpdateFrontend();
        }

        public override void Deinitialize()
        {
            _worker.RemoveHandler(typeof(TransitionVoting), OnTransitionVoting);
        }
        #endregion
        
        #region EventTransmission

        private void _FindAndCapEmptyArea(long capAt)
        {
            foreach (var playerId in _userIds)
            {
                //Transition transition = (Transition)EventMap.Instance.GetEvent(-1, playerId);

                foreach (var event_ in EventMap.Instance.GetEventsForPlayerId(playerId))
                {
                    var transition = (Transition) event_;

                    if (transition == null)
                    {
                        Logger.Warn($"Couldn't find previous empty event for player {playerId}");
                        continue;
                    }

                    transition.TurnEnd = capAt;

                    transition.CapFrames(capAt);

                    if (!EventBundler.Instance.Contains(transition))
                        EventBundler.Instance.AddEvent(transition);
                }
            }
        }

        private void SendInitialState()
        {
            long now = Time.CurrentTurn;
            
            _FindAndCapEmptyArea(now - 1);

            _state.GetPositionsForCurrentState(out List<List<Vector>> positions);
            
            foreach (var userNum in _userNums)
            {
                var userId = _userIds[userNum];

                var userGeneratorDictionary = new Dictionary<int, Vector>();
                var userGeneratorPositions = positions[userNum];
                for (var i = 0; i < userGeneratorPositions.Count; i++)
                {
                    userGeneratorDictionary[i] = userGeneratorPositions[i];
                }

                var frame = GetTransitionFrame(userGeneratorDictionary, now, long.MaxValue, userId, "initial");
                var playerFrames = new List<TransitionFrame> { frame };
                Transition transition = new Transition(
                    GetUniqueId(userId, now, 3), _lastTransitionId, -1,
                    playerFrames, now, long.MaxValue, userId, IncentiveType.Recommended, strategyId, 0);

                EventMap.Instance.AddOrModifyEvent(transition);
                EventBundler.Instance.AddEvent(transition);
            }

            _earliestPossibleNextTransition = now;
            _startTurnOfInfiniteEvent = now;
            _compareTransitionValuesNoEarlierThan = now + MinTurnsStayInState;

            // states
            Logger.Debug("Sending the initial state to the clients.");
            lock (_lastStateInfos)
            {
                _lastStateInfos.Clear();

                for (int playerNum = 0; playerNum < _numUsers; playerNum++)
                {
                    StateInfo playerInfo = CreateStateInfo(playerNum);

                    _lastStateInfos.Add(playerInfo);

                    Logger.Debug("Sending player update state info");

                    _worker.SendReliable(playerInfo);
                }
            }
        }

        

        private void GenerateComplementAreas(Vector generatorPosition,
            out Vector centroid, out Polygon area)
        {
            Dictionary<int, Vector> mockPositions = new Dictionary<int, Vector>
            {
                [0] = generatorPosition
            };

            for (int rotationIndex = 1; rotationIndex < 4; rotationIndex++)
            {
                var mockPosition = generatorPosition.RotateCounter(rotationIndex * Math.PI / 2);
                mockPositions[rotationIndex] = mockPosition;
            }

            Dictionary<int, Polygon> areas = null;
            try
            {
                areas = _splitter.GetAreasForPositions(mockPositions);
            }
            catch
            {
                Logger.Debug("Error when creating Voronoi areas. Using fallback positions.");
            }

            centroid = areas?[0].Centroid;
            area = areas?[0];
        }

        private void Transition(AssignedVote vote)
        {
            bool moreSubTransitionsInQueue;
            long firstStartTurn = -1;
            bool firstRun = true;

            StateTransition transition = vote.Transition;
            double preperationTimestampMs = vote.PlanningTimestampMs;
            double executionTimeMs = vote.ExecutionLengthMs;
            long now = Time.CurrentTurn;

            do
            {
                // transitions
                _state.GetPositionsForCurrentState(out List<List<Vector>> idsToStartingPositions);
                var currentStateId = _state.CurrentStateId;

                moreSubTransitionsInQueue = _state.Transition(transition, vote.Votes.Select(userVote => userVote.Transition).ToList());

                _state.GetPositionsForCurrentState(out List<List<Vector>> idsToEndingPositions);
                var followingStateId = _state.CurrentStateId;

                // get list of positions and areas for each player
                // either, get list of generator positions for each player Dictionary<int, Dictionary<int, Vector>>


                // timings
                CapByPreferredSettings(vote, ref preperationTimestampMs, ref executionTimeMs,
                    idsToStartingPositions, idsToEndingPositions);

                double boundedExecutionTimeMs;
                long boundedExecutionTurns, executionStartTurn, executionEndTurn, infiniteStartTurn, infiniteEndTurn;
                FindExecutionTimes(preperationTimestampMs, executionTimeMs, now,
                    out boundedExecutionTimeMs, out boundedExecutionTurns,
                    out executionStartTurn, out executionEndTurn,
                    out infiniteStartTurn, out infiniteEndTurn);

                CapPreviousInfinite(executionStartTurn);

                UpdateTimestamps(ref firstStartTurn, ref firstRun, infiniteStartTurn);

                // movement event
                Dictionary<int, Transition> idsToExecuteTransitions =
                    _PrepareTransitions(executionStartTurn, executionEndTurn, (float)(boundedExecutionTimeMs / 1000),
                        idsToStartingPositions, idsToEndingPositions, vote, TransitionContext.Animation);

                _FillTransitionsWithInterpolatedEvents(idsToExecuteTransitions,
                    idsToStartingPositions, idsToEndingPositions,
                    executionStartTurn, 1, boundedExecutionTurns);

                //Logger.Debug("Event 9");


                foreach (var userTransition in idsToExecuteTransitions.Values)
                {
                    if (userTransition.Frames == null || userTransition.Frames.Count == 0)
                    {
                        userTransition.Speed = 0;
                    }
                    else
                    {
                        var lastFrame = userTransition.Frames.Last();
                        var firstFrame = userTransition.Frames.First();
                        var dist = firstFrame.Position.Position.Distance(lastFrame.Position.Position);
                        var executionTime = Time.ConvertTurnsToSeconds(lastFrame.Position.TurnStart - firstFrame.Position.TurnStart);
                        var speed = dist / executionTime;
                        userTransition.Speed = speed;
                    }

                    //Logger.Debug($"Transition has speed {userTransition.Speed}");
                }

                //Logger.Debug("Event 10");


                // static event afterwards
                Dictionary<int, Transition> idsToEndingTransitions =
                    _PrepareTransitions(infiniteStartTurn, infiniteEndTurn, 0,
                        idsToEndingPositions, idsToEndingPositions, vote, TransitionContext.Static);
                //_trustHandler.AddToHistory(_lastTransitionId, transition);

                _FillTransitionsWithSingle(idsToEndingTransitions,
                    idsToEndingPositions,
                    infiniteStartTurn, infiniteEndTurn);

                //Logger.Debug("Event 11");

                // send events
                _SendTransitions(idsToExecuteTransitions);
                //Logger.Debug("Event 12");

                _SendTransitions(idsToEndingTransitions);
            } while (moreSubTransitionsInQueue);

            //Logger.Debug("Event 13");


            lock (_lastStateInfos)
            {
                //Logger.Debug("Sending new state infos for state " + _state.CurrentStateId);
                _lastStateInfos.Clear();

                // send state infos
                for (int playerNum = 0; playerNum < _numUsers; playerNum++)
                {
                    StateInfo stateInfo = CreateStateInfo(vote, firstStartTurn, playerNum);
                    _lastStateInfos.Add(stateInfo);
                    //Logger.Debug("Sending transition state info");
                    _worker.SendReliable(stateInfo);
                }
            }
            
        }

        private void UpdateTimestamps(ref long firstStartTurn, ref bool firstRun, long infiniteStartTurn)
        {
            _startTurnOfInfiniteEvent = infiniteStartTurn;
            _earliestPossibleNextTransition = _startTurnOfInfiniteEvent;
            if (firstRun)
            {
                firstStartTurn = _startTurnOfInfiniteEvent;
                firstRun = false;
            }
            _compareTransitionValuesNoEarlierThan = infiniteStartTurn + MinTurnsStayInState + TurnExecutionDelay;
        }

        private void CapPreviousInfinite(long executionStartTurn)
        {
            if (_startTurnOfInfiniteEvent < 0)
                _FindAndCapEmptyArea(executionStartTurn - 1);
            else
                _FindAndCapPreviousTransitionEvents(executionStartTurn - 1);
        }

        private void FindExecutionTimes(double preperationTimestampMs, double executionTimeMs, long now, out double boundedExecutionTimeMs, out long boundedExecutionTurns, out long executionStartTurn, out long executionEndTurn, out long infiniteStartTurn, out long infiniteEndTurn)
        {
            long preperationStartTurn = Math.Max(now, _startTurnOfInfiniteEvent); // OUCH, need to remember earliest execution time
                                                                                  // execution time
            long preperationEndTurn = Math.Max(preperationStartTurn + MinPreperationTurns,
                Time.ConvertMillisecondsToTurns(preperationTimestampMs));
            // preperationStartTurn + 

            boundedExecutionTimeMs = Math.Max(executionTimeMs, minExecutionMs);
            //Logger.Debug("Execution seconds: " + (minExecutionMs / 1000));
            boundedExecutionTurns = Time.ConvertMillisecondsToTurns(boundedExecutionTimeMs);
            executionStartTurn = preperationEndTurn + 1;
            executionEndTurn = executionStartTurn + boundedExecutionTurns;
            infiniteStartTurn = executionEndTurn - 1;
            infiniteEndTurn = long.MaxValue;
            //Logger.Debug($"now: {now}");
            //Logger.Debug($"earliest compare: {_compareTransitionValuesNoEarlierThan}");
            //Logger.Debug("Start turn: " + Time.ConvertTurnsToSeconds(preperationStartTurn - now));
            //Logger.Debug("Preperation min: " + (Time.ConvertTurnsToSeconds(preperationStartTurn + MinPreperationTurns) - Time.NowSeconds));
            //Logger.Debug("Preperation act: " + (Time.ConvertTurnsToSeconds(Time.ConvertMillisecondsToTurns(preperationTimestampMs))));
            //Logger.Debug($"Prep start turn: {preperationStartTurn}");
            //Logger.Debug($"Prep end turn: {preperationEndTurn}");
        }

        private void CapByPreferredSettings(AssignedVote vote, 
            ref double preperationTimestampMs, ref double executionTimeMs, 
            List<List<Vector>> idsToStartingPositions, List<List<Vector>> idsToEndingPositions)
        {
            if (CurrentSettings.PreferredManeuverSecondDistance > 0)
            {
                preperationTimestampMs = CurrentSettings.PreferredManeuverSecondDistance * 1000;

                Logger.Debug("Preferred interduration overwrites execution time.");
                Logger.Debug("Old: " + vote.PlanningTimestampMs + "ms, New: " + preperationTimestampMs + "ms");
            }

            if (CurrentSettings.PreferredSpeed > 0)
            {
                var avgDist = 0f;
                foreach (var userNum in _userNums)
                {
                    avgDist += Vector.Distance(idsToStartingPositions[userNum].First(), idsToEndingPositions[userNum].First());
                }
                avgDist /= _userNums.Count;

                var newExecutionTime = avgDist / CurrentSettings.PreferredSpeed;

                executionTimeMs = newExecutionTime * 1000;

                Logger.Debug("Preferred speed " + CurrentSettings.PreferredSpeed + " overwrites execution time.");
                Logger.Debug("Old: " + vote.ExecutionLengthMs + "ms, New: " + executionTimeMs + "ms");
            }
        }

        private StateInfo CreateStateInfo(int playerNum)
        {
            StateInfo playerInfo = _state.GetCurrentState(playerNum);
            playerInfo.FromSeconds = Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent);
            playerInfo.ToSeconds = Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent - 1);
            playerInfo.EarliestPossibleNextExecutionSeconds = Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent);
            playerInfo.UserId = _userIds[playerNum];

            playerInfo.StateId = _state.CurrentStateId;

            playerInfo.YourCurrentTransition = VSUserTransition.None;
            playerInfo.PastTransitions = new List<TransitionInfo>();
            return playerInfo;
        }

        private StateInfo CreateStateInfo(AssignedVote vote, long firstStartTurn, int playerNum)
        {
            StateInfo stateInfo = _state.GetCurrentState(playerNum);
            stateInfo.YourCurrentTransition = vote.Votes[playerNum].Transition;
            stateInfo.UserId = _userIds[playerNum];
            stateInfo.StateId = _state.CurrentStateId;
            stateInfo.FromSeconds = Time.ConvertTurnsToSeconds(firstStartTurn);
            stateInfo.ToSeconds = Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent - 1);
            stateInfo.EarliestPossibleNextExecutionSeconds = Time.ConvertTurnsToSeconds(_startTurnOfInfiniteEvent);

            // add to history
            var previousList = _transitionsPerUser[playerNum];
            TransitionInfo previousInfo = null;
            if (previousList.Any()) previousInfo = previousList.Last();

            var transitionInfo = new TransitionInfo()
            {
                FromState = previousInfo == null ? stateInfo.YourFinalState : previousInfo.ToState,
                ToState = stateInfo.YourFinalState,
                Transition = stateInfo.YourCurrentTransition,
                FromArea = previousInfo == null ? new Polygon() : previousInfo.ToArea,
                ToArea = stateInfo.ThisTransitionEndArea,
                FromSeconds = stateInfo.FromSeconds,
                ToSeconds = stateInfo.EarliestPossibleNextExecutionSeconds
            };
            _transitionsPerUser[playerNum].Add(transitionInfo);

            stateInfo.PastTransitions = _transitionsPerUser[playerNum]
                .Skip(_transitionsPerUser[playerNum].Count - 3).Take(3).ToList();
            return stateInfo;
        }

        private void ResetEvents()
        {
            var eventsToRevoke = EventMap.Instance.GetEventsForStrategyId(strategyId);
            var lastTurn = Time.CurrentTurn - 1;
            foreach (var eventToRevoke in eventsToRevoke)
            {
                eventToRevoke.TurnEnd = lastTurn;
            }
            EventBundler.Instance.AddEvents(eventsToRevoke);
        }

        private void _SendTransitions(Dictionary<int, Transition> playerToTransitions)
        {
            //var first = playerToTransitions.First().Value;
            //Logger.Debug($"Sending transition from {first.TurnStart} to {first.TurnEnd}");
            foreach (var transition in playerToTransitions.Values)
            {
                EventMap.Instance.AddOrModifyEvent(transition);
                EventBundler.Instance.AddEvent(transition);
            }
        }

        private void _FindAndCapPreviousTransitionEvents(long undelayedEndTurn)
        {
            //Logger.Debug("Overriding event with last start turn " + _startTurnOfInfiniteEvent);

            if (_startTurnOfInfiniteEvent > 0)
            {
                for (int playerNum = 0; playerNum < _numUsers; playerNum++)
                {
                    int playerId = _userIds[playerNum];

                    Transition transition =
                        (Transition)EventMap.Instance.GetEvent(strategyId,
                            GetUniqueId(playerId, _startTurnOfInfiniteEvent, 3));

                    if (transition == null)
                    {
                        Logger.Warn($"Couldn't find previous infinite event for player {playerId}");
                        continue;
                    }

                    long endTurn = undelayedEndTurn + TurnExecutionDelay;
                    transition.TurnEnd = endTurn;

                    transition.CapFrames(endTurn);

                    //Logger.Debug("Capping " + transition.Id);

                    if (!EventBundler.Instance.Contains(transition))
                        EventBundler.Instance.AddEvent(transition);
                }
            }
        }

        private Dictionary<int, Transition> _PrepareTransitions(
            long turnToStartTransition, long turnToEndTransition, float transitionSeconds,
            List<List<Vector>> idsToStartingPositions, List<List<Vector>> idsToEndingPositions,
            AssignedVote vote, TransitionContext context)
        {
            Dictionary<int, Transition> playerToTransitions = new Dictionary<int, Transition>();
            int previousTransitionId = _lastTransitionId;
            int transitionId = ++_lastTransitionId;

            long startTurn = turnToStartTransition + TurnExecutionDelay;
            long endTurn = turnToEndTransition == long.MaxValue
                ? long.MaxValue : turnToEndTransition + TurnExecutionDelay;

            //Logger.Debug($"New transition, start: {startTurn}, end: {endTurn}");
            for (int playerNum = 0; playerNum < _numUsers; playerNum++)
            {
                int playerId = _userIds[playerNum];

                float distance = idsToEndingPositions[playerNum].First().Distance(idsToStartingPositions[playerNum].First());

                float speed = (Math.Abs(transitionSeconds) < 0.001) ? 0f : (float)distance / transitionSeconds;
                //Logger.Debug("Transition speed: " + speed);

                playerToTransitions[playerId] = new Transition(
                    GetUniqueId(playerId, turnToStartTransition, 3), transitionId,
                    previousTransitionId,
                    new List<TransitionFrame>(),
                    startTurn, endTurn,
                    playerId,
                    IncentiveType.Recommended, strategyId,
                    speed
                );
                playerToTransitions[playerId].TransitionContext = context;
                playerToTransitions[playerId].TransitionType = vote.Votes[playerNum].Transition;

                //if (speed > 0)
                //    Logger.Debug($"Transition speed of {playerId}: {speed}");
            }
            return playerToTransitions;
        }

        private void _FillTransitionsWithSingle(Dictionary<int, Transition> playerToTransitions,
            List<List<Vector>> positions,
            long turnToStartTransition, long turnToEndTransition = long.MaxValue)
        {
            long turnStart = turnToStartTransition;
            long turnEnd = turnToEndTransition;

            foreach (var userNum in _userNums)
            {
                var mockUserPositions = new Dictionary<int, Vector>();
                var mockUserWeights = new Dictionary<int, double>();
                var userGeneratorPositions = positions[userNum];
                for (var i = 0; i < userGeneratorPositions.Count; i++)
                {
                    mockUserPositions[i] = userGeneratorPositions[i];
                    mockUserWeights[i] = IsInTheCenter(userGeneratorPositions[i]) ? CurrentSettings.FocusWeight : 1;
                }

                mockUserPositions = VoronoiWeighting.WeightPositions(mockUserPositions, mockUserWeights);

                var userId = _userIds[userNum];
                var frame = GetTransitionFrame(mockUserPositions,
                            turnStart, turnEnd, userId, "static");

                playerToTransitions[userId].Frames.Add(frame);
            }
            
        }

        bool IsInTheCenter(Vector pos) => Math.Abs(pos.X) < .5f && Math.Abs(pos.Z) < .5f;

        private void _FillTransitionsWithInterpolatedEvents(Dictionary<int, Transition> playerToTransitions,
            List<List<Vector>> idsToStartingPositions, List<List<Vector>> idsToEndingPositions,
            long turnToStartTransition, int sampleEvery, long numTurnsForTransition)
        {
            foreach (var userId in _userIds)
            {
                var userNum = _userIds.IndexOf(userId);

                var mockUserStartingPositions = idsToStartingPositions[userNum];
                var mockUserEndingPositions = idsToEndingPositions[userNum];
                
                var mockUserStartWeights = mockUserStartingPositions.Select
                    (pos => IsInTheCenter(pos) ? CurrentSettings.FocusWeight : 1).ToList();
                var mockUserEndWeights = mockUserEndingPositions.Select
                    (pos => IsInTheCenter(pos) ? CurrentSettings.FocusWeight : 1).ToList();

                for (long turnOffset = 0; turnOffset < numTurnsForTransition; turnOffset++)
                {
                    if (turnOffset % sampleEvery != 0 && turnOffset != numTurnsForTransition - 1) continue;
                    
                    float progressFactor = (float)turnOffset / numTurnsForTransition;

                    Dictionary<int, Vector> lerpPositions = new Dictionary<int, Vector>();
                    Dictionary<int, double> lerpWeights = new Dictionary<int, double>();
                    for (int playerNum = 0; playerNum < 4; playerNum++)
                    {
                        lerpPositions[playerNum] =
                            Map(mockUserStartingPositions[playerNum],
                                mockUserEndingPositions[playerNum], 
                                progressFactor);
                        lerpWeights[playerNum] =
                            Map(mockUserStartWeights[playerNum],
                                mockUserEndWeights[playerNum],
                                progressFactor);
                    }

                    lerpPositions = VoronoiWeighting.WeightPositions(lerpPositions, lerpWeights);

                    long turnStart = turnToStartTransition + turnOffset;
                    long turnEnd = turnStart + sampleEvery - 1;
                    if (turnEnd >= turnStart + numTurnsForTransition) turnEnd = turnStart + numTurnsForTransition - 1;

                    // get voronoi
                    var frame = GetTransitionFrame(lerpPositions, 
                        turnStart, turnEnd, userId, "transition");

                    playerToTransitions[userId].Frames.Add(frame);
                }
            }
        }

        private TransitionFrame GetTransitionFrame(
            Dictionary<int, Vector> generatorPositions,
            long turnStart, long turnEnd, int userId, string displayString)
        {
            Vector framePosition;
            Polygon framePolygon;
            try
            {
                var areas = _splitter.GetAreasForPositions(generatorPositions);
                framePolygon = areas.Values.First();
                framePosition = framePolygon.Centroid;
            }
            catch (Exception e)
            {
                Logger.Error("Error generating user areas: " + e);
                return null;
            }

            TimedPosition positionEvent =
                new TimedPosition(framePosition,
                    turnStart, turnEnd,
                    GetUniqueId(userId, turnStart, 1),
                    IncentiveType.Recommended,
                    userId, strategyId);

            TimedArea areaEvent = null;
            if (framePolygon != null)
            {
                areaEvent =
                    new TimedArea(framePolygon,
                        turnStart, turnEnd,
                        GetUniqueId(userId, turnStart, 2),
                        IncentiveType.Recommended,
                        userId, strategyId, displayString);
            }

            var frame = new TransitionFrame(positionEvent, areaEvent);
            return frame;
        }

        
        
        public override void UpdateIncentives()
        {
            // events are send in UpdateState for convienience
        }
        #endregion

        #region Voting
        private void OnTransitionVoting(IMessageBase baseMessage)
        {
            TransitionVoting voting = (TransitionVoting)baseMessage;
            Logger.Debug("Received new vote from " + voting.UserId);

            if (_state.CurrentStateId != voting.StateId)
            {
                Logger.Warn($"Discarding vote for state {voting.StateId}. Current system state is {_state.CurrentStateId}.");

                ResendStateInfo(voting.UserId);

                return;
            }

            float nowSeconds = Time.NowSeconds;
            
            foreach (var vote in voting.Votes)
            {
                vote.ArrivalTime = nowSeconds;
            }

            votings[voting.UserId] = voting;
        }

        private void ResendStateInfo(int userId)
        {
            lock (_lastStateInfos)
            {
                var stateInfo = _lastStateInfos.Find(info => info.UserId == userId);
                if (stateInfo != null)
                {
                    _worker.SendReliable(stateInfo);
                }
            }
        }

        private readonly List<StateInfo> _lastStateInfos = new List<StateInfo>();
        ConcurrentDictionary<int, StateHandlerProperties> _userProperties = new ConcurrentDictionary<int, StateHandlerProperties>();
        private void OnStateHandlingProperties(IMessageBase baseMessage)
        {
            StateHandlerProperties props = (StateHandlerProperties)baseMessage;

            _userProperties[props.UserId] = props;

            UpdateQueueLength();

            Logger.Debug("Setting queue length to " + TransitionQueueLength);

            UpdatePriorities();
        }

        private void UpdateQueueLength()
        {
            if (_userProperties.Any())
                TransitionQueueLength = _userProperties.Values.Min(prop => prop.QueueLength);
            if (CurrentSettings.TransitionQueueLength > 0)
                TransitionQueueLength = CurrentSettings.TransitionQueueLength;
        }

        float NextEarliestCompareAfterError;
        public int TransitionQueueLength = 0;
        private bool _forceEvenLayout = false;
        private float _secondsLastDecision;
        private float _continueDespiteMissingVotesAfter = 7f;
        public override void UpdateState()
        {
            if (_updatePlayers)
            {
                // check if the configuration is good, otherwise force transition
                if (!_state.HaveEvenLayout())
                {
                    UpdatePlayerListDelayed(true);
                    _forceEvenLayout = true;
                }
                else
                {
                    //Logger.Debug("Adding player");
                    
                    lock (_updateLock)
                    {
                        UpdatePlayerListDelayed(false);

                        _updatePlayers = false;
                    }
                }
            }

            //_trustHandler.UpdateTrust();

            if (_userNums.Count == 0) return;
            
            if (CurrentSettings.Reset)
            {
                Reset();
            }

            if (!CurrentSettings.Run) return;

            if (Time.CurrentTurn < NextEarliestCompareAfterError)
            {
                Logger.Trace("Not comparing votes due to downtime after error");
                return;
            }

            if (TransitionQueueLength < CurrentNumTransitionsInQueue)
            {
                Logger.Trace("Not comparing votes because not missing queue element");
                return;
            }

            // exclude those that were kicked out
            List<TransitionVoting> currentVotings = votings.Values.Where(value => _userIds.Contains(value.UserId)).ToList();
            currentVotings.RemoveAll(voting => voting.StateId != _state.CurrentStateId);

            if (Time.NowSeconds - _secondsLastDecision < _continueDespiteMissingVotesAfter
                    && currentVotings.Count < _numUsers)
            {
                Logger.Warn("Didn't receive votes from all users");
                // TODO send to missing 
                for (int userNum = 0; userNum < _numUsers; userNum++)
                {
                    if (!currentVotings.Exists(voting => voting.UserId == _userIds[userNum]))
                    {
                        Logger.Warn($"Missing user num {userNum}");
                        ResendStateInfo(_userIds[userNum]);
                    }

                }
                NextEarliestCompareAfterError = Time.CurrentTurn + 5;
                return;
            }

            if (currentVotings.Count == 0)
            {
                Logger.Warn("Didn't receive votes. Can't continue despite panic.");
                NextEarliestCompareAfterError = Time.CurrentTurn + 5;
                return;
            }

            var transitionToVoteAggregation = AddUserVotingsToSystemTransitions(currentVotings);

            transitionToVoteAggregation.RemoveAll(vote => vote.Votes.Any(userVote => userVote == null));

            var requiredExists = transitionToVoteAggregation.Any(vote => vote.RequiredTransition);
            if (requiredExists)
            {
                var possibleTransitionsBefore = transitionToVoteAggregation.Count;

                // need to do this user by user narrowing down 
                foreach (var userNum in _userNums)
                {
                    if (!transitionToVoteAggregation.Any(vote => vote.Votes[userNum].RequiredTransition)) continue;

                    transitionToVoteAggregation = transitionToVoteAggregation
                        .Where(vote => vote.Votes[userNum].RequiredTransition)
                        .ToList();
                }
                Logger.Debug($"Filtered {possibleTransitionsBefore - transitionToVoteAggregation.Count}/{possibleTransitionsBefore} transitions due to priority");
            }

            if (_forceEvenLayout)
            {
                Logger.Debug("Forcing even layout");
                // check if the transitions create an even thingie
                // assume it can be done in single step (and it should be if there are no required votes)
                var possibleTransitionsBefore = transitionToVoteAggregation.Count;

                transitionToVoteAggregation = transitionToVoteAggregation
                        .Where(vote =>
                            _state.HaveEvenLayoutAfter(vote.Votes.Select(userVote => userVote.Transition).ToArray()))
                        .ToList();
                 
                Logger.Debug($"Filtered {possibleTransitionsBefore - transitionToVoteAggregation.Count}/{possibleTransitionsBefore} transitions due to force joining");
            }
            
            var transitionsToKeep = 
                transitionToVoteAggregation.Where(aggrVote => 
                    (aggrVote.Transition & CurrentSettings.AllowedTransitions) == aggrVote.Transition).ToList();

            transitionToVoteAggregation = transitionsToKeep;

            if (!transitionToVoteAggregation.Any())
            {
                Logger.Warn("Filtered out all transitions");
                NextEarliestCompareAfterError = Time.CurrentTurn + _betweenFailuresTurns;
                return;
            }

            transitionToVoteAggregation.RemoveAll(vote => transitionToMinimumNumberOfActors.ContainsKey(vote.Transition) && vote.NumActors < transitionToMinimumNumberOfActors[vote.Transition]);
            
            transitionToVoteAggregation.RemoveAll(vote => vote.Votes.Count(userVote => userVote != null) != _numUsers);

            bool customSelected = false;
            if (CurrentSettings.CustomTransitionQueue != null && CurrentSettings.CustomTransitionQueue.Any())
            {
                var transition = CurrentSettings.CustomTransitionQueue.First();
                transitionToVoteAggregation.RemoveAll(aggr => aggr.Transition != transition);
                customSelected = true;
            }

            if (transitionToVoteAggregation.Count <= 0)
            {
                Logger.Warn("No state voted on by all users");
                NextEarliestCompareAfterError = Time.CurrentTurn + _betweenFailuresTurns;
                return;
            }

            _timeMatcher.OffsetTimes(currentVotings);
            transitionToVoteAggregation = _timeMatcher.AggregateTimes(transitionToVoteAggregation);
            _timeMatcher.UnoffsetTimes(currentVotings);

            transitionToVoteAggregation.RemoveAll(aggrVote =>
                aggrVote.ExecutionLengthMs > CurrentSettings.MaxAllowedExecutionSeconds * 1000);

            if (transitionToVoteAggregation.Count == 0)
            {
                Logger.Warn("Couldn't find good time combination for any transition");

                NextEarliestCompareAfterError = Time.CurrentTurn + _betweenFailuresTurns;
                return;
            }

            

            _votingMechanism.SetVotes(currentVotings, transitionToVoteAggregation);
            _votingMechanism.PrepareVoting();
            AssignedVote bestVote = _votingMechanism.GetBestVote();

            if (bestVote == null) return;

            _votingMechanism.ConsiderSelection(bestVote);

            //if (bestVote.Transition != StateTransition.AssymmetricRotation) Logger.Warn("Non-Assymmetric");
            Log($"Transitioning from state {_state.CurrentStateId} with {bestVote.TransitionName}");
            Log($"Avg value: {bestVote.Votes.Select(userVote => _votingMechanism.GetUserValue(userVote, bestVote)).Average()}, Prepreration: {bestVote.PlanningTimestampMs - NowMs}, Execution: {bestVote.ExecutionLengthMs}");
            //foreach (var userVote in bestVote.Votes)
            //{
            //    Logger.Debug($"User transition was request: {userVote.Transition}");
            //    Logger.Debug($"User prepare times: {userVote.PlanningTimestampMs.ToPrintableString()}");

            //    Logger.Debug($"User exec times: {userVote.ExecutionLengthMs.ToPrintableString()}");
            //}

            _votingMechanism.UnprepareVoting();

            _secondsLastDecision = Time.NowSeconds;

            if (customSelected)
            {
                CurrentSettings.CustomTransitionQueue.RemoveAt(0);
                CurrentSettings.VersionNumber++;
                UpdateFrontend();
            }

            Transition(bestVote);
            votings.Clear();
        }

        public override void UpdateFrontend()
        {
            _worker.SendToFrontend(CurrentSettings, false);
        }

        private void Log(string message)
        {
            Logger.Debug(message);
            _metricLogger.Log(message);
        }

        private static AssignedVote _transitionTypeToAssignedVote(List<AssignedVote> votes, StateTransition type)
        {
            return votes.Find(vote => vote.Transition == type);
        }

        private List<AssignedVote> AddUserVotingsToSystemTransitions(List<TransitionVoting> currentVotings)
        {
            List<AssignedVote> aggregatedVotings = new List<AssignedVote>();

            if (_state.IsFocused())
            {
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Unfocus);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Stay);

                foreach (TransitionVoting voting in currentVotings)
                {
                    int playerNum = _userIds.IndexOf(voting.UserId);

                    foreach (TransitionVote vote in voting.Votes)
                    {
                        if (!FocusUserStateToSystemStateMapping.ContainsKey(vote.Transition))
                        {
                            Logger.Warn($"Player {playerNum} send transition vote {vote.TransitionName} that is not relevant in this state");
                            continue;
                        }
                        AssignedVote aggrVote = _transitionTypeToAssignedVote(aggregatedVotings, FocusUserStateToSystemStateMapping[vote.Transition]);
                        AddToAggregatedVote(aggrVote, playerNum, vote);
                    }
                }
            }
            else
            {
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Focus1);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Focus2);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Focus3);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Focus4);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.RotateLeft);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.RotateRight);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Rotate45Left);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Rotate45Right);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Stay);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Switch1);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Switch2);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Switch3);
                CreateAggregationForTransition(aggregatedVotings, StateTransition.Switch4);

                List<TransitionVote>[] rotateWaitVotes = new List<TransitionVote>[_numUsers];
                for (int userNum = 0; userNum < _numUsers; userNum++)
                {
                    rotateWaitVotes[userNum] = new List<TransitionVote>();
                }
                

                foreach (var voting in currentVotings)
                {
                    int playerNum = _userIds.IndexOf(voting.UserId);
                    if (playerNum == -1) continue;
                    int playerStateIndex = _state.GetRotationalIndexFromUserNum(playerNum);
                    //Logger.Debug($"Player {playerNum} has index {playerStateIndex}");

                    foreach (var vote in voting.Votes)
                    {
                        switch (vote.Transition)
                        {
                            case VSUserTransition.Focus:
                                {
                                    AssignedVote aggrVote =
                                        _transitionTypeToAssignedVote(aggregatedVotings, UserIndexToSystemFocusStateMapping[playerStateIndex / 2]);
                                    //Logger.Debug($"Index {playerStateIndex} wants index {UserIndexToSystemFocusStateMapping[playerStateIndex]}");
                                    AddToAggregatedVote(aggrVote, playerNum, vote, true);
                                    break;
                                }
                            case VSUserTransition.Defocus:
                                {
                                    // add to other focus transitions
                                    for (int otherPlayerNum = 0; otherPlayerNum < _numUsers; otherPlayerNum++)
                                    {
                                        if (otherPlayerNum == playerNum) continue;
                                        int otherStateIndex = _state.GetRotationalIndexFromUserNum(otherPlayerNum);
                                        AssignedVote otherAggrVote =
                                            _transitionTypeToAssignedVote(aggregatedVotings, UserIndexToSystemFocusStateMapping[otherStateIndex / 2]);
                                        AddToAggregatedVote(otherAggrVote, playerNum, vote);
                                    }

                                    // add to other switch transitions
                                    {
                                        int nextStateIndex = _state.GetRotationalIndexFromUserNum(playerNum, 1);
                                        AssignedVote otherAggrVote =
                                            _transitionTypeToAssignedVote(aggregatedVotings, UserIndexToSystemSwitchStateMapping[nextStateIndex / 2]);
                                        AddToAggregatedVote(otherAggrVote, playerNum, vote);
                                    }
                                    {
                                        int nextNextStateIndex = _state.GetRotationalIndexFromUserNum(playerNum, 2);
                                        AssignedVote otherAggrVote =
                                            _transitionTypeToAssignedVote(aggregatedVotings, UserIndexToSystemSwitchStateMapping[nextNextStateIndex / 2]);
                                        AddToAggregatedVote(otherAggrVote, playerNum, vote);
                                    }

                                    break;
                                }
                                //int playerStateIndex = _state.GetRotationalIndexFromUserNum(playerNum);

                            case VSUserTransition.SwitchLeft:
                                {
                                    // rotate in switch
                                    AssignedVote aggrVote =
                                        _transitionTypeToAssignedVote(aggregatedVotings, 
                                            UserIndexToSystemSwitchStateMapping[_state.GetRotationalIndexFromUserNum(playerNum, 6) / 2]);
                                    AddToAggregatedVote(aggrVote, playerNum, vote, true);
                                    break;
                                }
                            case VSUserTransition.SwitchRight:
                                {
                                    // focus in switch
                                    AssignedVote aggrVote =
                                        _transitionTypeToAssignedVote(aggregatedVotings, 
                                            UserIndexToSystemSwitchStateMapping[_state.GetRotationalIndexFromUserNum(playerNum) / 2]);
                                    AddToAggregatedVote(aggrVote, playerNum, vote, true);
                                    break;
                                }
                            case VSUserTransition.RotateLeft:
                            case VSUserTransition.RotateRight:
                            case VSUserTransition.Rotate45Left:
                            case VSUserTransition.Rotate45Right:
                            case VSUserTransition.Stay:
                                {
                                    AssignedVote aggrVote =
                                        _transitionTypeToAssignedVote(aggregatedVotings, DefaultUserStateToSystemStateMapping[vote.Transition]);
                                    AddToAggregatedVote(aggrVote, playerNum, vote);
                                    rotateWaitVotes[playerNum].Add(vote);
                                    break;
                                }
                            default:
                                {
                                    Logger.Warn($"Player {playerNum} send transition vote {vote.TransitionName} that is not relevant in this state");
                                    break;
                                }
                        }
                    }
                }

                // create the aggregated vote for the rotation+wait combination
                // find 
                var assymmetricRotations = GetAggregatedVoteForRotationAndWait(rotateWaitVotes);

                aggregatedVotings.AddRange(assymmetricRotations);
            }

            return aggregatedVotings;
        }

        private List<AssignedVote> GetAggregatedVoteForRotationAndWait(List<TransitionVote>[] rotateWaitVotes)
        {
            // go through all combinations
            // find the valid ones
            // if four players it's easy, it's just the same maneuvers
            // in fact those should always match
            // exclude those because we include them with the traditional maneuvers

            List<AssignedVote> votes = new List<AssignedVote>();

            if (_numUsers >= 4) return votes;

            //_state.PrintUserIndices();

            TransitionVote[] userVotes = new TransitionVote[_numUsers];
            var matchingCombinations = ExpandTransitionVoteIfValid(userVotes, rotateWaitVotes, 0);

            // todo debug this 
            //Logger.Debug($"Found {matchingCombinations.Count} combinations");
            //int i = 1;
            foreach (var combination in matchingCombinations)
            {
                // printing combinations
                //Logger.Debug($"Combination {i++}");
                //int userNum = 0;
                //foreach (var vote in combination)
                //{
                //    Logger.Debug($"User {userNum++}: {vote.Transition}");
                //}
                
                AssignedVote assignedVote = new AssignedVote(_numUsers);
                assignedVote.Transition = StateTransition.AssymmetricRotation;
                assignedVote.Votes = combination;
                assignedVote.RequiredTransition = combination.Any(userVote => userVote.RequiredTransition);

                votes.Add(assignedVote);
            }

            return votes;
        }
        
        private List<TransitionVote[]> ExpandTransitionVoteIfValid(TransitionVote[] userVotes, List<TransitionVote>[] rotateWaitVotes, int userNum)
        {
            var nextUserNum = userNum + 1;

            List<TransitionVote> thisUserVotes = rotateWaitVotes[userNum];
            List<TransitionVote[]> matchingCombinations = new List<TransitionVote[]>();

            var myCurrentPosition = _state.GetRotationalIndexFromUserNum(userNum);
            foreach (var userVote in thisUserVotes)
            {
                var myExpectedPosition = _state.GetRotationalIndexFromUserNum(userNum, userVote.Transition);

                bool isValid = true;
                for (int otherUser = 0; otherUser < userNum; otherUser++)
                {
                    var otherCurrentPosition = _state.GetRotationalIndexFromUserNum(otherUser);
                    var otherExpectedPosition = _state.GetRotationalIndexFromUserNum(otherUser, userVotes[otherUser].Transition);

                    // is valid check
                    // (1) shouldn't overlap in final position (implies that they don't while moving)
                    // (2) shouldn't cross, could be that they request rotateLeft and rotateRight which would collide (not excluded by rule (1))
                    var leftDist = Math.Abs(myExpectedPosition - otherExpectedPosition);
                    var rightDist = Math.Abs(8 - leftDist);
                    var minDist = Math.Min(leftDist, rightDist);
                    if (minDist < 2 ||
                        (myExpectedPosition == otherCurrentPosition && myCurrentPosition == otherExpectedPosition))
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    TransitionVote[] newUserVotes = new TransitionVote[_numUsers];
                    for (int i = 0; i < newUserVotes.Length; i++)
                    {
                        newUserVotes[i] = userVotes[i];
                    }
                    newUserVotes[userNum] = userVote;

                    // todo copy array, shouldn't if there is only one valid combination
                    // todo should reuse the array at least
                    if (nextUserNum < _numUsers)
                    {                        
                        var recursiveCombinations = ExpandTransitionVoteIfValid(newUserVotes, rotateWaitVotes, nextUserNum);
                        matchingCombinations.AddRange(recursiveCombinations);
                    } else
                    {
                        // last user in line
                        // do not add if they all have the same transition type
                        var firstUserTransitionType = newUserVotes[0].Transition;

                        userVotes[userNum] = userVote;

                        if (!newUserVotes.All(otherVote => otherVote.Transition == firstUserTransitionType))
                            matchingCombinations.Add(newUserVotes);
                    }
                }
            }
            
            return matchingCombinations;
        }


        private static void AddToAggregatedVote(AssignedVote assignedVote, int playerNum, TransitionVote vote, bool increaseActor = false)
        {
            assignedVote.Votes[playerNum] = vote;
            if (increaseActor) assignedVote.NumActors++;
            assignedVote.RequiredTransition = assignedVote.RequiredTransition || vote.RequiredTransition;
        }

        private void CreateAggregationForTransition(List<AssignedVote> aggregatedVotings, StateTransition transition)
        {
            AssignedVote assignedVote = new AssignedVote(_numUsers);
            assignedVote.Transition = transition;
            aggregatedVotings.Add(assignedVote);
        }
        #endregion

        #region Ticks
        public void OnTickMessage(IMessageBase messageBase)
        {
            var tickMessage = (Tick)messageBase;

            var userNum = _userIds.IndexOf(tickMessage.UserId);
            _tickManager.AddTick(userNum, tickMessage);

            _tickManager.ClearUntil(Time.NowSeconds - 10f);

            // only send so and so often, check last tick time???
            if (_tickManager.LastNecessaryTick < Time.NowSeconds &&
                    _tickManager.TryGetTickRecommendations()) // user who's tick is longest ago
            {
                foreach (var otherNum in _userNums)
                {
                    RecommendedTicks recommendedTickMessage = new RecommendedTicks()
                    {
                        UserId = _userIds[otherNum],
                        TickSecondsLeft = _tickManager.TickRecommendation(otherNum)
                    };

                    _worker.SendReliable(recommendedTickMessage);
                }
            }
        }
        #endregion

        #region Helper
        private bool IsEqual(double a, double b, double epsilon = 10e-6)
            {
                return Math.Abs(b - a) < epsilon;
            }

            private static Vector Map(Vector a, Vector b, float f)
            {
                return a * (1 - f) + b * f;
            }

            private static double Map(double a, double b, double f)
            {
                return a * (1 - f) + b * f;
            }

            private static long GetUniqueId(int playerId, long turn, int eventInTurn)
            {
                return 23 * playerId + 27 * turn + 17 * eventInTurn;
            }
            #endregion
    }
}
