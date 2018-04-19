using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class MaximumAverageVotingMechanism : VotingMechanism
    {
        private const double DefaultVote = 0;

        public override void PrepareVoting()
        {
            foreach (var vote in AssignedVotes)
            {
                int numVotes = 0;
                foreach (var userVote in vote.Votes)
                {
                    vote.Value += userVote?.Value ?? DefaultVote;
                    numVotes++;
                }
                vote.Value /= numVotes;
            }
        }

        public override double GetUserValue(TransitionVote userVote, AssignedVote assignedVote)
        {
            throw new System.NotImplementedException();
        }
    }
}