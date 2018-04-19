using System;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class AssignedVote
    {
        public AssignedVote(int numVotes)
        {
            Votes = new TransitionVote[numVotes];
        }

        public string TransitionName =>
            Enum.GetName(typeof(StateTransition), Transition);

        public StateTransition Transition;
        public double PlanningTimestampMs;
        public double ExecutionLengthMs;
        public double Value;
        public TransitionVote[] Votes;
        public int NumActors;
        public double TimeValue;
        public bool RequiredTransition;
    }
    
}