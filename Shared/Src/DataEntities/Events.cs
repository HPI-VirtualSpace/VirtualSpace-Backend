using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf;

namespace VirtualSpace.Shared
{
    [DataContract]
    public enum IncentiveType
    {
        [EnumMember]
        Required,
        [EnumMember]
        Recommended,
        [EnumMember]
        Unblocked,
        [EnumMember]
        Blocked,
        [EnumMember]
        RequestAccepted,
        [EnumMember]
        RequestPending,
        [EnumMember]
        All,
        [EnumMember]
        None,
        [EnumMember]
        Interpolation
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TimedArea : TimedEvent
    {
        [DataMember]
        public Polygon Area;
        [DataMember]
        public string DisplayName;

        public TimedArea() { }

        public TimedArea(Polygon Area, long TurnStart, long TurnEnd, long Id, IncentiveType Type, 
            int playerId = -1, int strategyId = -1, string displayName = "")
            : base(TurnStart, TurnEnd, Id, playerId, Type, EventType.Area, strategyId)
        {
            this.Area = Area;
            DisplayName = displayName;
        }

        public TimedArea(Polygon Area, long Turn, long Id, IncentiveType Type, int playerId = -1, int strategyId = -1)
            : base(Turn, Turn, Id, playerId, Type, EventType.Area, strategyId)
        {
            this.Area = Area;
        }

        public override void OverrideWith(TimedEvent other)
        {
            OverrideWith((TimedArea)other);
        }

        public void OverrideWith(TimedArea otherArea)
        {
            if (otherArea == null)
            {
                throw new ArgumentException("Overriding argument is not of type " + this.GetType());
            }

            base.OverrideWith(otherArea);
            Area = otherArea.Area;
            DisplayName = otherArea.DisplayName;
        }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TransitionFrame {
        [DataMember] public TimedPosition Position;
        [DataMember] public TimedArea Area;
        [DataMember] public PolygonList TailArea;

        public TransitionFrame()
        {

        }

        public TransitionFrame(TimedPosition position, TimedArea area)
        {
            Position = position;
            Area = area;
        }

        public void OverrideWith(TransitionFrame other)
        {
            Position.OverrideWith(other.Position);
            if (Area == null)
                Area = other.Area;
            else 
                Area.OverrideWith(other.Area);
            if (TailArea == null)
                TailArea = other.TailArea;
            else 
                TailArea = other.TailArea;
        }
    }

    [DataContract]
    public enum TransitionForm
    {
        [EnumMember]
        Static,
        [EnumMember]
        Linear
    }

    [DataContract]
    public enum TransitionType
    {
        [EnumMember]
        Preperation,
        [EnumMember]
        Chill,
        [EnumMember]
        Explosive,
        [EnumMember]
        Wait
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TransitionKeyframe : TimedEvent
    {
        [ProtoMember(10, AsReference = true)]
        [DataMember]
        public Transition PrecedingTransition;
        [ProtoMember(11, AsReference = true)]
        [DataMember]
        public Transition SucceedingTransition;

        public TransitionKeyframe(long turn, long id, int playerId, IncentiveType incentiveType, int strategyId)
            : base(turn, turn, id, playerId, incentiveType, EventType.Keyframe, strategyId)
        {
            
        }
}

    public enum TransitionContext
    {
        Prepare,
        Animation,
        Static
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Transition : TimedEvent
    {
        [DataMember] public int PreviousTransitionId;
        [DataMember] public int TransitionId;
        [DataMember] public List<TransitionFrame> Frames;
        [DataMember] public float Speed;
        [DataMember] public VSUserTransition TransitionType;
        [DataMember] public TransitionContext TransitionContext;

        [ProtoMember(10, AsReference = true)]
        [DataMember]
        public TransitionKeyframe StartKeyframe;
        [ProtoMember(11, AsReference = true)]
        [DataMember]
        public TransitionKeyframe EndKeyframe;

        public float SecondsToStart { get { return VirtualSpaceTime.ConvertTurnsToSeconds(TurnStart - VirtualSpaceTime.CurrentTurn); } } 

        public float SecondsToEnd
        {
            get
            {
                long boundedTurnEnd;
                if (TurnEnd == long.MaxValue)
                {
                    if (Frames.Count == 1)
                    {
                        var onlyFrame = Frames.First();
                        var endTurn = onlyFrame.Position.TurnEnd;
                        if (endTurn == long.MaxValue)
                        {
                            return float.MaxValue;
                        }
                        else
                        {
                            boundedTurnEnd = endTurn;
                        }
                    }
                    else
                    {
                        boundedTurnEnd = Frames.ElementAt(Frames.Count - 2).Position.TurnEnd;
                    }
                }
                else
                    boundedTurnEnd = TurnEnd;

                return VirtualSpaceTime.ConvertTurnsToSeconds(boundedTurnEnd - VirtualSpaceTime.CurrentTurn);
            }
        }

        private Transition()
        {

        }
        
        public Transition(long id, int transitionId, int previousId, List<TransitionFrame> frames,
            long turnStart, long turnEnd, int playerId, IncentiveType incentiveType, long strategyId,
            float speed)
            : base(turnStart, turnEnd, id, playerId, incentiveType, EventType.Transition, strategyId)
        {
            PreviousTransitionId = previousId;
            TransitionId = transitionId;
            Frames = frames;
            Speed = speed;
        }

        public Transition(Transition other) : 
            this(other.Id, other.TransitionId, other.PreviousTransitionId, other.Frames, 
                other.TurnStart, other.TurnEnd, other.PlayerId, other.Type, other.StrategyId,
                other.Speed)
        {
            
        }

        public List<TransitionFrame> GetFrames()
        {
            return Frames ?? new List<TransitionFrame>();
        }

        public void CapFrames(long endTurn)
        {
            List<TransitionFrame> frames =
                        Frames.FindAll(potentialFrame => potentialFrame.Position.TurnEnd > endTurn);
            foreach (TransitionFrame frame in frames)
            {
                frame.Position.TurnEnd = endTurn;
                if (frame.Area != null)
                    frame.Area.TurnEnd = endTurn;
            }
        }

        public IEnumerable<TransitionFrame> GetActiveFrames(long turn)
        {
            if (Frames == null) return Enumerable.Empty<TransitionFrame>();
            return Frames.FindAll(potentialFrame => potentialFrame.Position.TurnStart <= turn &&
                                    turn <= potentialFrame.Position.TurnEnd);
        }

        public IEnumerable<TransitionFrame> GetActiveBetween(long from, long to)
        {
            if (Frames == null) return Enumerable.Empty<TransitionFrame>();
            return Frames.FindAll(frame => frame.Area != null && frame.Area.IsActiveInBetween(from, to));
        }

        // (potentialEvent.TurnEnd == long.MaxValue || desiredTurn <= potentialEvent.TurnEnd + turnTolerance)
        public IEnumerable<TimedEvent> GetEvents()
        {
            foreach (TransitionFrame frame in Frames)
            {
                yield return frame.Position;
                yield return frame.Area;
            }
        }

        public override void OverrideWith(TimedEvent other)
        {
            OverrideWith((Transition)other);
        }

        public void OverrideWith(Transition otherTransition)
        {
            if (otherTransition == null)
            {
                throw new ArgumentException("Overriding argument is not of type " + this.GetType());
            }

            base.OverrideWith(otherTransition);

            PreviousTransitionId = otherTransition.PreviousTransitionId;
            TransitionId = otherTransition.TransitionId;
            Speed = otherTransition.Speed;

            List<TransitionFrame> overridingFrames = otherTransition.Frames;
            List<TransitionFrame> framesToOverride = Frames;

            if (framesToOverride == null) framesToOverride = overridingFrames;
            else if (overridingFrames != null)
                foreach (TransitionFrame overridingFrame in overridingFrames)
                {
                    TransitionFrame frameToOveride = framesToOverride.Find(frameToOverwrite =>
                        overridingFrame.Position.Id == frameToOverwrite.Position.Id &&
                        ((overridingFrame.Area == null && frameToOverwrite.Area == null) ||
                        overridingFrame.Area.Id == frameToOverwrite.Area.Id));

                    if (frameToOveride == null)
                    {
                        //throw new ArgumentException("Transition doesn't contain frame.");
                        return;
                    }

                    frameToOveride.OverrideWith(overridingFrame);
                }
        }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TimedPosition : TimedEvent
    {
        [DataMember]
        public Vector Position;
        [DataMember]
        public string DisplayName;

        public TimedPosition() { }

        public TimedPosition(TimedPosition other)
            : this(other.Position.Clone(),
                  other.TurnStart,
                  other.TurnEnd,
                  other.Id,
                  other.Type,
                  other.PlayerId,
                  other.StrategyId,
                  other.DisplayName)
        {

        }

        public TimedPosition(Vector position, long turnStart, long turnEnd, long id, IncentiveType type, int playerId, long strategyId = -1, string displayName="")
            : base(turnStart, turnEnd, id, playerId, type, EventType.Position, strategyId)
        {
            Position = position;
            DisplayName = displayName;
        }

        public TimedPosition(Vector position, long turn, long id, IncentiveType type, int playerId, long strategyId = -1, string displayName = "")
            : base(turn, turn, id, playerId, type, EventType.Position, strategyId)
        {
            Position = position;
            DisplayName = displayName;
        }

        public override void OverrideWith(TimedEvent other)
        {
            OverrideWith((TimedPosition)other);
        }

        public void OverrideWith(TimedPosition otherPosition)
        {
            if (otherPosition == null)
            {
                throw new ArgumentException("Overriding argument is not of type " + this.GetType());
            }

            base.OverrideWith(otherPosition);
            Position = otherPosition.Position;
            DisplayName = otherPosition.DisplayName;
        }
    }

    [DataContract]
    public enum EventType
    {
        [EnumMember]
        Area,
        [EnumMember]
        Position,
        [EnumMember]
        Revoke,
        [EnumMember]
        Transition,
        [EnumMember]
        Keyframe
    }
    
    [DataContract]
    [KnownType(typeof(TimedArea))]
    [ProtoInclude(501, typeof(TimedArea))]
    [KnownType(typeof(TimedPosition))]
    [ProtoInclude(502, typeof(TimedPosition))]
    [ProtoInclude(503, typeof(RevokeEvent))]
    [KnownType(typeof(Transition))]
    [ProtoInclude(504, typeof(Transition))]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public abstract class TimedEvent
    {
        [DataMember]
        public long TurnStart;
        public float MsStart { get { return VirtualSpaceTime.ConvertTurnsToSeconds(TurnStart) * 1000; } }
        [DataMember]
        public long TurnEnd;
        public float MsEnd { get { return VirtualSpaceTime.ConvertTurnsToSeconds(TurnEnd) * 1000; } }
        [DataMember]
        public long Id;
        [DataMember]
        public long StrategyId;
        [DataMember]
        public IncentiveType Type;
        [DataMember]
        public EventType EventType;
        [DataMember]
        public int PlayerId;

        public TimedEvent() { }

        public TimedEvent(long TurnStart, long TurnEnd, long Id, int PlayerId, IncentiveType type, EventType eventType, long StrategyId = -1)
        {
            this.TurnStart = TurnStart;
            this.TurnEnd = TurnEnd;
            this.Id = Id;
            this.PlayerId = PlayerId;
            Type = type;
            this.EventType = eventType;
            this.StrategyId = StrategyId;
        }

        public bool IsActiveAt(long turn)
        {
            return TurnStart <= turn && turn <= TurnEnd; 
        }

        public bool IsActiveInBetween(long fromTurn, long toTurn)
        {
            return fromTurn <= TurnStart && TurnStart <= toTurn || fromTurn <= TurnEnd && TurnEnd <= toTurn
                   || TurnStart <= fromTurn && fromTurn <= TurnEnd || TurnStart <= toTurn && toTurn <= TurnEnd;

        }

        public virtual void OverrideWith(TimedEvent other)
        {
            TurnStart = other.TurnStart;
            TurnEnd = other.TurnEnd;
            Id = other.Id;
            StrategyId = other.StrategyId;
            Type = other.Type;
            EventType = other.EventType;
            PlayerId = other.PlayerId;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class RevokeEvent : TimedEvent
    {
        public RevokeEvent() { }

        public RevokeEvent(long TurnStart, long TurnEnd, long Id, int PlayerId)
            : base(TurnStart, TurnEnd, Id, PlayerId, IncentiveType.None, EventType.Revoke)
        {
        }

        public override void OverrideWith(TimedEvent other)
        {
            base.OverrideWith(other);
        }
    }
}
