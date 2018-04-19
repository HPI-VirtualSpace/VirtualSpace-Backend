using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public abstract class VotingMechanism
    {
        protected List<TransitionVoting> UserVotings;
        protected List<AssignedVote> AssignedVotes;
        protected List<float> UserPriorities;

        public void SetVotes(List<TransitionVoting> votes, List<AssignedVote> assignedVotes)
        {
            UserVotings = votes;
            AssignedVotes = assignedVotes;
        }

        public virtual void AddUser()
        {

        }

        public virtual void SetUserPriorities(List<float> userPriorities)
        {
            UserPriorities = userPriorities;
        }

        public virtual void RemoveUser(int userNum)
        {

        }

        public virtual void PrepareVoting() { }
        public virtual void UnprepareVoting() { }

        public virtual AssignedVote GetBestVote()
        {
            double maxValue = double.MinValue;
            AssignedVote maxVote = null;
            foreach (AssignedVote vote in AssignedVotes)
            {
                Logger.Debug($"{vote.TransitionName}, value {vote.Value}");
                if (maxValue < vote.Value)
                {
                    maxValue = vote.Value;
                    maxVote = vote;
                }
            }

            return maxVote == null || maxVote.Value <= 0 ? null : maxVote;
        }

        public virtual void ConsiderSelection(AssignedVote selectedVote) { }

        public virtual void Reset() { }
        public abstract double GetUserValue(TransitionVote userVote, AssignedVote assignedVote);
    }
}
