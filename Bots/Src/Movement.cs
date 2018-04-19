using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using VirtualSpace.Shared;

namespace VirtualSpace.Bots
{
    internal abstract class Movement
    {
        protected double PositionX;
        protected double PositionZ;
        protected double OrientationX;
        protected double OrientationZ;
        protected Polygon RequiredSpace;

        public Movement() { }

        public Movement(double positionX, double positionZ)
        {
            PositionX = positionX;
            PositionZ = positionZ;
        }

        public Movement(double positionX, double positionZ, double orientationX, double orientationZ, Polygon requiredSpace = null)
        {
            PositionX = positionX;
            PositionZ = positionZ;
            OrientationX = orientationX;
            OrientationZ = orientationZ;
            RequiredSpace = requiredSpace;
        }

        public virtual void Initialize(BotWorker worker)
        {

        }

        // get player position
        public Vector GetPlayerPosition()
        {
            return new Vector(PositionX, PositionZ);
        }

        // get space requirements
        abstract public PolygonList GetSpaceRequirements();

        // calculate next position
        abstract public void AdvanceTurn();

        protected static PolygonList RequireWholeSpace()
        {
            List<Vector> list = new List<Vector>() {
                new Vector(Config.Space.MinX, Config.Space.MinZ),
                new Vector(Config.Space.MaxX, Config.Space.MinZ),
                new Vector(Config.Space.MaxX, Config.Space.MaxZ),
                new Vector(Config.Space.MinX, Config.Space.MaxZ)
            };
            Polygon polygon = new Polygon(list);
            return new PolygonList(new List<Polygon>() { polygon });
        }
    }

    class MoveTowardsPosition : Movement
    {
        public Vector CurrentPosition
        {
            get { return new Vector(PositionX, PositionZ); }
            set
            {
                PositionX = value.X;
                PositionZ = value.Z;
            }
        }

        public double Now { get { return DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond; } }

        List<TimedEvent> _events = new List<TimedEvent>();
        double _lastUpdateTime;
        float _speed = 2f;

        static int startIndex = 0;
        public MoveTowardsPosition()
        {
            Random random = new Random(startIndex++);
            _lastUpdateTime = Now;
            PositionX = -2 + random.NextDouble() * 4;
            PositionZ = -2 + random.NextDouble() * 4;
        }

        public override void Initialize(BotWorker worker)
        {
            worker.AddHandler(typeof(Incentives), _handleIncentives);
        }

        public void _handleIncentives(IMessageBase message)
        {
            //Logger.Debug("handle incentives");
            Incentives incentives = (Incentives)message;

            List<RevokeEvent> revokeEvents = new List<RevokeEvent>();
            List<TimedEvent> actuationEvents = new List<TimedEvent>();
            foreach (TimedEvent newEvent in incentives.Events)
            {
                var newTimedEvent = (TimedEvent)newEvent;
                if (newEvent is RevokeEvent)
                {
                    revokeEvents.Add((RevokeEvent)newTimedEvent);
                } else
                {
                    actuationEvents.Add(newTimedEvent);
                }
            }

            lock (_events)
            {
                _events.RemoveAll(event_ =>
                    revokeEvents.TrueForOne(
                        revokeEvent => revokeEvent.Id == event_.Id
                    )
                );

                foreach (TimedEvent newEvent in actuationEvents)
                {
                    TimedEvent eventToOverwrite = _events.Find(existingEvent => newEvent.StrategyId == existingEvent.StrategyId && newEvent.Id == existingEvent.Id);
                    if (eventToOverwrite == null)
                    {
                        _events.Add(newEvent);
                    } else
                    {
                        eventToOverwrite.OverrideWith(newEvent);
                    }
                }
                
            }

            //double minDistance = double.MaxValue;
            //Vector minPosition = null;
            //Vector currentPosition = CurrentPosition;
            //foreach (Vector position in positions)
            //{
            //    double distance = (currentPosition - position).SqrMagnitude;
            //    if (distance < minDistance)
            //    {
            //        minDistance = distance;
            //        minPosition = position;
            //    }
            //}
            
            // _recommendedPosition = minPosition;
        }

        public void CleanupEvents()
        {
            List<TimedEvent> toDelete = new List<TimedEvent>();
            lock (_events)
            {
                foreach (TimedEvent event_ in _events)
                {
                    if (event_.TurnEnd < VirtualSpaceTime.CurrentTurn)
                    {
                        toDelete.Add(event_);
                    }
                }
                _events.RemoveRange(toDelete);
            }
        }

        public Vector GetRecommendedPosition()
        {
            float minDistance = float.MaxValue;
            Vector minPosition = null;
            //TimedPosition debugPosition = null;

            long turnNow = VirtualSpaceTime.CurrentTurn;
            lock (_events)
            {
                IEnumerable<TimedEvent> potentialEventsEnumerable = _events.Where(potentialEvent =>
                    (potentialEvent.Type == IncentiveType.Recommended ||
                     potentialEvent.Type == IncentiveType.Required) &&
                    potentialEvent.TurnStart <= turnNow && turnNow <= potentialEvent.TurnEnd);

                foreach (TimedEvent event_ in potentialEventsEnumerable)
                {
                    if (event_ is TimedPosition)
                    {
                        TimedPosition position = (TimedPosition) event_;
                        float distance = position.Position.Distance(CurrentPosition);
                        if (distance <= minDistance)
                        {
                            minDistance = distance;
                            minPosition = position.Position;
                        }
                    }
                    else if (event_ is Transition)
                    {
                        Transition transition = (Transition) event_;
                        foreach (TransitionFrame frame in transition.GetActiveFrames(turnNow))
                        {
                            TimedPosition position = frame.Position;
                            float distance = position.Position.Distance(CurrentPosition);
                            if (distance <= minDistance)
                            {
                                minDistance = distance;
                                minPosition = position.Position;
                            }
                        }
                    }
                }
            }

            //Logger.Debug("Moving bot towards " + minPosition);
            //if (debugPosition != null)
            //Logger.Debug("debugPosition: " + debugPosition.Type + ", start: " + debugPosition.TurnStart + ", end: " + debugPosition.TurnEnd + ", strategy id: " + debugPosition.StrategyId);
            return minPosition;
        }

        public override void AdvanceTurn()
        {
            double secondsToMove = Now - _lastUpdateTime;
            _lastUpdateTime = Now;

            CleanupEvents();
            Vector recommendedPosition = GetRecommendedPosition();
            
            if (recommendedPosition == null || recommendedPosition.Distance(CurrentPosition) < 7 * 1e-2) return;

            Vector recommendedDirection = (recommendedPosition - CurrentPosition).Normalized;

            CurrentPosition += recommendedDirection * secondsToMove * _speed;
        }

        public override PolygonList GetSpaceRequirements()
        {
            return RequireWholeSpace();
        }
    }

    class RequestState : MoveTowardsPosition
    {
        private BotWorker _worker;

        private Random _random;

        //private StateMap _map;
        private int _botIndex;

        private static ColorPref[] _botColorPrefs = {
            ColorPref.Blue,
            ColorPref.Green,
            ColorPref.Red,
            ColorPref.Yellow
        };

        public override void Initialize(BotWorker worker)
        {
            base.Initialize(worker);

            _worker = worker;

            _worker.AddHandler(typeof(StateInfo), OnStateInfo);

            _botIndex = _worker.WorkerId;

            _random = new Random((int)DateTime.Now.Ticks);

            //_map = StateMap.Instance;
            //_map.InitializeStatePositions(-2, -2, 2, 2, .5, .2);

            var preferences = new PlayerPreferences();
            preferences.Color = _botColorPrefs[_botIndex];
            var message = new PreferencesMessage();
            message.preferences = preferences;
            _worker.SendReliable(message);
        }

        private TransitionVoting _voting;
        private Thread _sendVotingThread;
        private bool _sendAlive = false;

        public void OnStateInfo(IMessageBase baseMessage)
        {
            if (_sendAlive)
            {
                _sendAlive = false;
                //_sendVotingThread.Abort();
            }

            StateInfo info = (StateInfo) baseMessage;

            TransitionVoting voting = new TransitionVoting();
            voting.StateId = info.StateId;

            //Logger.Debug($"Receive at turn: {VirtualSpaceTime.CurrentTurn}, Millis: {VirtualSpaceTime.CurrentTimeInMillis}");

            //_map.GetPlayerStatus(info.SystemRotationState, info.SystemPlayerInFocusState, info.SystemPlayerNum,
            //    out Vector startPosition, out Polygon startPolygon);

            //if (info.SystemPlayerNum == 0)
            //{
            //    Logger.Debug($"=============");
            //    Logger.Debug($"{info.SystemPlayerNum} at {startPosition}");
            //}

            if (!VirtualSpaceTime.IsInitialized)
            {
                Logger.Warn("Current VS time is not initialized");
                Thread.Sleep(200);
            }

            for (int i = 0; i < info.PossibleTransitions.Count; i++)
            {
                VSUserTransition transition = info.PossibleTransitions[i];

                TransitionVote vote = new TransitionVote();

                //_map.GetPlayerStatus(info.SystemRotationState, info.SystemPlayerInFocusState, transition, info.SystemPlayerNum,
                //    out Vector endPosition, out Polygon endPolygon);

                //if (info.SystemPlayerNum == 0)
                //{
                //    Logger.Debug($"{info.SystemPlayerNum} with {transition} at {endPosition}");
                //    Logger.Debug($"{endPolygon.Points.ToPrintableString()}");
                //    Logger.Debug($"{info.TransitionEndPositions[i]}");
                //    Logger.Debug($"{info.TransitionEndAreas[i].Points.ToPrintableString()}");
                //}

                //var timeMultiplier = 100; // for debugging
                var timeMultiplier = 1000; // realistic

                //if (_botIndex == 0)
                //{
                    //vote.PlanningTimeType = PlanningTimeType.RelativeExecution;
                    vote.PlanningTimestampMs = new List<double>() { 1000, 2000 };
                //} else if (_botIndex == 1)
                //{
                //    vote.PlanningTimeType = PlanningTimeType.Absolute;
                //    var nowMs = VirtualSpaceTime.CurrentTimeInMillis;
                //    vote.PlanningTimestampMs = new List<double>() { nowMs + (2 + 2 * _random.NextDouble()) * timeMultiplier, nowMs + 4 * timeMultiplier };
                //}
                //else
                //{
                //    vote.PlanningTimeType = PlanningTimeType.RelativeArrival;
                //    vote.PlanningTimestampMs = new List<double>() { (2 + _random.NextDouble() * .3) * timeMultiplier, (2 + _random.NextDouble() * .3) * timeMultiplier };
                //}

                vote.Transition = transition;
                vote.ExecutionLengthMs = new List<double> { 1000, 2000 };

                //for (int j = 0; j < vote.PlanningTimestampMs.Count; j++)
                //{
                //    if (vote.PlanningTimeType == PlanningTimeType.RelativeExecution || vote.PlanningTimeType == PlanningTimeType.RelativeArrival)
                //        vote.PlanningTimestampMs[j] *= timeMultiplier;
                //    vote.ExecutionLengthMs[j] *= timeMultiplier;
                //}

                //for (int po = 500; po < 5000; po += 500)
                //{
                //    for (int eo = 500; eo < 5000; eo += 500)
                //    {
                //        vote.PlanningTimestampMs.Add(po + _random.NextDouble() * 100);
                //        vote.ExecutionLengthMs.Add(eo + _random.NextDouble() * 100);
                //    }
                //}

                List<TimeCondition> timeConditions = new List<TimeCondition>();
                var prepTime = new Variable(VariableTypes.PreperationTime);
                var execTime = new Variable(VariableTypes.ExecutionTime);
                var arrivalTime = new Variable(VariableTypes.ArrivalTime);
                var calculationTime = new Variable(VariableTypes.CalculationTime);

                var proxyPrep = new Variable(VariableTypes.Continuous);

                var now = VirtualSpaceTime.CurrentTimeInMillis;
                var offset = 1000;
                var multiple = new Variable(VariableTypes.Integer);
                var intervalLength = 300;
                var tolerance = 100;
                if (_botIndex % 2 == 0)
                {
                    timeConditions.Add(proxyPrep == now + offset + multiple * intervalLength);
                    timeConditions.Add(prepTime >= proxyPrep - tolerance);
                    timeConditions.Add(prepTime <= proxyPrep + tolerance);
                }
                else
                {
                    timeConditions.Add(prepTime >= now + _random.NextDouble() * 200);
                }
                timeConditions.Add(execTime >= 1200 + _random.NextDouble() * 100);
                timeConditions.Add(execTime <= 1400 + _random.NextDouble() * 100);

                vote.TimeConditions = timeConditions;

                if (transition == VSUserTransition.Defocus || (info.YourCurrentTransition == VSUserTransition.Defocus && transition == VSUserTransition.Stay))
                {
                    vote.Value = _random.Next(0, 25);
                } else
                    switch (transition)
                    {
                        case VSUserTransition.Stay:
                            vote.Value = _random.Next(40, 90);
                            if (_botIndex == 0)
                            {
                                //vote.RequiredTransition = _random.NextDouble() < .5f;
                            }

                            break;
                        case VSUserTransition.SwitchLeft:
                        case VSUserTransition.SwitchRight:
                            vote.Value = 0;
                            break;
                        case VSUserTransition.RotateLeft:
                        case VSUserTransition.RotateRight:
                            vote.Value = _random.Next(50, 100);
                            break;
                        case VSUserTransition.Focus:
                            vote.Value = 0;
                            break;
                        case VSUserTransition.Rotate45Left:
                        case VSUserTransition.Rotate45Right:
                            vote.Value = _random.Next(60, 100);
                            break;
                        default:
                            vote.Value = _random.Next(50, 100);
                            break;
                    }
                
                voting.Votes.Add(vote);
            }

            _voting = voting;

            if (_botIndex == 0)
            {
                // send once
                _worker.SendReliable(_voting);
            //}
            //else if (_botIndex == 1)
            //{
            //    // too hard
            //    // recalc + send in loop
            //    _worker.SendReliable(_voting);
            } else
            {
                // send in loop
                _sendAlive = true;
                _sendVotingThread = new Thread(SendVoting);
                _sendVotingThread.Start();
            }
        }

        void SendVoting()
        {
            while (_sendAlive)
            {
                _worker.SendReliable(_voting);
                Thread.Sleep(1000);
            }
        }
    }

    // always stays on the starting position
    class IdleMovement : Movement
    {
        public IdleMovement(double positionX, double positionZ) : base(positionX, positionZ) { }

        public override PolygonList GetSpaceRequirements()
        {
            return RequireWholeSpace();
        }

        public override void AdvanceTurn()
        {
            // do nothing
        }
    }

    class BouncingMovement2D : Movement
    {
        public BouncingMovement2D(double positionX, double positionZ, double orientationX, double orientationZ, Polygon requiredSpace = null) : base(positionX, positionZ, orientationX, orientationZ, requiredSpace) { }

        public override PolygonList GetSpaceRequirements()
        {
            if (RequiredSpace != null) return new PolygonList(RequiredSpace);
            else return RequireWholeSpace();
        }

        public override void AdvanceTurn()
        {

            PositionX += OrientationX;
            if (PositionX > Config.Space.MaxX)
            {
                PositionX = Config.Space.MaxX;
                OrientationX = -OrientationX;
            }
            if (PositionX < Config.Space.MinX)
            {
                PositionX = Config.Space.MinX;
                OrientationX = -OrientationX;
            }
            PositionZ += OrientationZ;
            if (PositionZ > Config.Space.MaxZ)
            {
                PositionZ = Config.Space.MaxZ;
                OrientationZ = -OrientationZ;
            }
            if (PositionZ < Config.Space.MinZ)
            {
                PositionZ = Config.Space.MinZ;
                OrientationZ = -OrientationZ;
            }
        }
    }
}