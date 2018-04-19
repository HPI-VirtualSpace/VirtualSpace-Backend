using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf;
using Object = System.Object;
#if UNITY
using UnityEngine;
#endif

namespace VirtualSpace.Shared
{
    [DataContract]
    // setup
    [ProtoInclude(501, typeof(Registration))]
    [ProtoInclude(502, typeof(RegistrationSuccess))]
    [ProtoInclude(503, typeof(Deregistration))]
    [ProtoInclude(504, typeof(DeregistrationSuccess))]
    [ProtoInclude(505, typeof(PlayerAllocationRequest))]
    [ProtoInclude(506, typeof(AllocationDenied))]
    [ProtoInclude(507, typeof(AllocationGranted))]
    // status
    [ProtoInclude(508, typeof(PlayerPosition))]
    [ProtoInclude(512, typeof(TimeMessage))]
    // state handler
    [ProtoInclude(510, typeof(PreferencesMessage))]
    [ProtoInclude(511, typeof(Incentives))]
    [ProtoInclude(517, typeof(StateInfo))]
    [ProtoInclude(518, typeof(TransitionVoting))]
    [ProtoInclude(519, typeof(Tick))]
    [ProtoInclude(520, typeof(RecommendedTicks))]
    [ProtoInclude(521, typeof(StateHandlerProperties))]
    // frontend
    [ProtoInclude(522, typeof(FrontendMessage))]

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class MessageBase : IMessageBase
    {
#if UNITY
        [HideInInspector]
#endif
        public int UserId;

        public MessageBase() { }
        public MessageBase(int userId) { UserId = userId; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class RecommendedTicks : MessageBase
    {
        public List<float> TickSecondsLeft;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Tick : MessageBase
    {
        public float Second;
        public bool InRelationToPreviousTick;

        public Tick()
        {
            Second = VirtualSpaceTime.CurrentTimeInSeconds;
        }
    }

    [ProtoContract]
    public enum VSUserState
    {
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Up,
        Down,
        Left,
        Right,
        UpLeftDefocus,
        UpRightDefocus,
        DownLeftDefocus,
        DownRightDefocus,
        UpLeftFocus,
        UpRightFocus,
        DownLeftFocus,
        DownRightFocus,
    }
    
    [ProtoContract]
    [Flags]
    public enum VSUserTransition
    {
        Stay = 1,
        Focus = 1 << 0x01,
        Unfocus = 1 << 0x02,
        Defocus = 1 << 0x03,
        Undefocus = 1 << 0x04,
        RotateLeft = 1 << 0x05,
        RotateRight = 1 << 0x06,
        Rotate45Left = 1 << 0x07,
        Rotate45Right = 1 << 0x08,
        SwitchLeft = 1 << 0x09,
        SwitchRight = 1 << 0x10,
        None = 1 << 0x11,
    }

    [ProtoContract]
    [Flags]
    public enum StateTransition
    {
        Focus1 = 1,
        Focus2 = 1 << 0x01,
        Focus3 = 1 << 0x02,
        Focus4 = 1 << 0x03,
        Unfocus = 1 << 0x04,
        RotateRight = 1 << 0x05,
        RotateLeft = 1 << 0x06,
        Rotate45Right = 1 << 0x07,
        Rotate45Left = 1 << 0x08,
        Stay = 1 << 0x09,
        Switch1 = 1 << 0x10,
        Switch2 = 1 << 0x11,
        Switch3 = 1 << 0x12,
        Switch4 = 1 << 0x13,
        AssymmetricRotation = 1 << 0x14,
        None = 1 << 0x15,
    }

    public static class TransitionHelper
    {
        public static StateTransition AllStateTransitions =
            StateTransition.Focus1 | StateTransition.Focus2 | StateTransition.Focus3 | StateTransition.Focus4 |
            StateTransition.Unfocus |
            StateTransition.RotateRight | StateTransition.RotateLeft |
            StateTransition.Rotate45Right | StateTransition.Rotate45Left |
            StateTransition.Stay |
            StateTransition.Switch1 | StateTransition.Switch2 | StateTransition.Switch3 | StateTransition.Switch4 |
            StateTransition.AssymmetricRotation;

        public static VSUserTransition AllTransitions =
            VSUserTransition.Stay | VSUserTransition.Focus | VSUserTransition.Unfocus | VSUserTransition.Defocus |
            VSUserTransition.Undefocus | VSUserTransition.RotateLeft | VSUserTransition.RotateRight |
            VSUserTransition.Rotate45Left | VSUserTransition.Rotate45Right | VSUserTransition.SwitchLeft |
            VSUserTransition.SwitchRight;
        public static VSUserTransition StayTransitions = 
            VSUserTransition.Stay | VSUserTransition.Focus | VSUserTransition.Defocus 
            | VSUserTransition.Unfocus | VSUserTransition.Undefocus;
        public static VSUserTransition StaySmallTransitions =
            VSUserTransition.Stay | VSUserTransition.Defocus | VSUserTransition.Undefocus;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class StateInfo : MessageBase
    {
        public int StateId;
        public float FromSeconds;
        public float ToSeconds;
        public float EarliestPossibleNextExecutionSeconds;
        public VSUserState YourFinalState;
        public List<VSUserTransition> PossibleTransitions;

        public List<Vector> TransitionEndPositions;
        public List<Polygon> TransitionEndAreas;
        public Vector ThisTransitionEndPosition;
        public Polygon ThisTransitionEndArea;
        public VSUserTransition YourCurrentTransition;

        public List<TransitionInfo> PastTransitions = new List<TransitionInfo>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TransitionInfo
    {
        public VSUserState FromState;
        public Polygon FromArea;
        public VSUserTransition Transition;
        public Polygon ToArea;
        public VSUserState ToState;
        public float FromSeconds;
        public float ToSeconds;
        public float Duration { get { return ToSeconds - FromSeconds; } }
    }

    [ProtoContract]
    public enum PlanningTimeType
    {
        /// <summary>
        /// Absolute in VirtualSpace milliseconds. Resend regularly.
        /// </summary>
        [ProtoEnum]
        Absolute,
        /// <summary>
        /// Planning times are absolute with planning_time = message_arrival_time + specified_planning_time. Resend regularly.
        /// </summary>
        [ProtoEnum]
        RelativeArrival,
        /// <summary>
        /// Planning times are relative with planning_time = calculation/execution_time + specified_planning_time.
        /// </summary>
        [ProtoEnum]
        RelativeExecution
    }

    [ProtoContract]
    public class TransitionVote
    {
        public string TransitionName { get { return Enum.GetName(typeof(VSUserTransition), Transition); } } 

        [ProtoMember(1)]
        public VSUserTransition Transition;
        [ProtoMember(2)]
        public List<double> PlanningTimestampMs;
        [ProtoMember(3)]
        public List<double> ExecutionLengthMs;
        [ProtoMember(4)]
        public List<TimeCondition> TimeConditions;
        [ProtoMember(5)]
        public Value ValueFunction;
        [ProtoMember(6)]
        public double Value;
        [ProtoMember(7)]
        public bool RequiredTransition;
#if BACKEND
        public float ArrivalTime;
        public double NormalizedValue;
#endif
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TransitionVoting : MessageBase
    {
        public int StateId;
        public List<TransitionVote> Votes = new List<TransitionVote>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class StateHandlerProperties : MessageBase
    {
        public int QueueLength;
        public float TimePriority;
        public float MovePriority;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Registration : MessageBase
    {
        public string UserName;
        public int sessionId;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class RegistrationSuccess : MessageBase
    {
        public bool Reregistration;
    }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Deregistration : MessageBase { }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class DeregistrationSuccess : MessageBase { }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PlayerAllocationRequest : MessageBase
    {
        public int RequestId;
        public Polygon MustHave;
        public Polygon NiceHave;
    }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class AllocationDenied : MessageBase { }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class AllocationGranted : MessageBase
    {
        public Vector Offset;
        public double RotationAroundFirstPoint;
    }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PlayerPosition : MessageBase
    {
        public double MillisSinceStartup;
        public Vector Position;
        public Vector Orientation;

        public PlayerPosition() : this(0, 0, 0, 0) { }

        public PlayerPosition(float px, float pz, float ox, float oz) : this(new Vector(px, pz), new Vector(ox, oz)) { }

        public PlayerPosition(Vector Position, Vector Orientation)
        {
            this.Position = new Vector(Position);
            this.Orientation = new Vector(Orientation);
        }

        public PlayerPosition(double MillisSinceStartup, 
            Vector Position, Vector Orientation) : this(Position, Orientation)
        {
            this.MillisSinceStartup = MillisSinceStartup;
        }

        public PlayerPosition Clone()
        {
            return new PlayerPosition(Position, Orientation);
        }
    }

    [DataContract]
    [ProtoContract]
    public enum ColorPref
    {
        [EnumMember]
        [ProtoEnum]
        Gray,
        [EnumMember]
        [ProtoEnum]
        Red,
        [EnumMember]
        [ProtoEnum]
        Blue,
        [EnumMember]
        [ProtoEnum]
        Yellow,
        [EnumMember]
        [ProtoEnum]
        Green,
        [EnumMember]
        [ProtoEnum]
        Orange,
    }

    /// <summary>
    /// App preferences that the backend should consider.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PlayerPreferences
    {
        public string MainTrackableIdentifier;
        public List<string> OtherTrackableIdentifiers;
        public string SceneName;
        public ColorPref Color;
        public PlayerPreferences() { }

        public static PlayerPreferences Default
        {
            get
            {
                PlayerPreferences Default_ = new PlayerPreferences()
                {

                };
                return Default_;
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PreferencesMessage : MessageBase
    {
        public PlayerPreferences preferences;

        public PreferencesMessage() { }
        public PreferencesMessage(PlayerPreferences preferences) {
            this.preferences = preferences;
        }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TimeMessage : MessageBase
    {
        [DataMember]
        public double Millis;
        [DataMember]
        public double TripTime;

        private TimeMessage() { }

        public TimeMessage(int userId, double millis, double tripTime)
            : base(userId)
        {
            Millis = millis;
            TripTime = tripTime;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Incentives : MessageBase
    {
        public List<TimedEvent> Events = new List<TimedEvent>();

        public Incentives() { }
        public Incentives(List<TimedEvent> Events) { this.Events = Events; }

        /* legacy */
        public PolygonList NegativeAreas { get { return GetPolygons(IncentiveType.Blocked); } }
        public PolygonList PositiveAreas { get { return GetPolygons(IncentiveType.Recommended); } }
        public List<Vector> NegativePositions { get { return GetVectors(IncentiveType.Blocked); } }
        public List<Vector> PositivePositions { get { return GetVectors(IncentiveType.Recommended); } }
        public PolygonList GetPolygons(IncentiveType type)
        {
            PolygonList polygons = new PolygonList();
            foreach (TimedArea area in GetAllAreas(type))
            {
                polygons.Add(area.Area);
            }
            return polygons;
        }
        public List<Vector> GetVectors(IncentiveType type)
        {
            List<Vector> vectors = new List<Vector>();
            foreach (TimedPosition position in GetAllPositions(type))
            {
                vectors.Add(position.Position);
            }
            return vectors;
        }

        public static Incentives GenerateEmptyMessage()
        {
            Incentives incentives = new Incentives();
            incentives.Events = new List<TimedEvent>();
            return incentives;
        }

        public IEnumerable<TimedArea> GetAllAreas(IncentiveType type)
        {
            foreach (TimedEvent timedEvent in Events)
            {
                TimedArea timedArea = timedEvent as TimedArea;
                if (timedArea != null && (type == IncentiveType.All || timedArea.Type == type))
                {
                    yield return timedArea;
                }
            }
        }

        public IEnumerable<TimedPosition> GetAllPositions(IncentiveType type) {
            foreach (TimedEvent timedEvent in Events)
            {
                TimedPosition timedPosition = timedEvent as TimedPosition;
                if (timedPosition != null && (type == IncentiveType.All || timedPosition.Type == type))
                {
                    yield return timedPosition;
                }
            }
        }

        public IEnumerable<RevokeEvent> GetAllRevokeEvents()
        {
            foreach (TimedEvent potentialEvent in Events)
            {
                if (potentialEvent is RevokeEvent)
                {
                    yield return (RevokeEvent)potentialEvent;
                }
            }
        }

        public void AddIncentives(Incentives other)
        {
            Events.AddRange(other.Events);
        }

        public bool Equals(Incentives other)
        {
            return other != null &&
                (Events.Count == other.Events.Count) && !Events.Except(other.Events).Any();
        }

        public override bool Equals(Object other)
        {
            return other != null && other is Incentives && Equals((Incentives)other);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}