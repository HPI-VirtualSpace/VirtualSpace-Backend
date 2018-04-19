using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class MaximumShiftingDictatorMechanism : VotingMechanism
    {
        private const double DefaultVote = 0;
        private const int TermLength = 4;

        private int _dictatorState;
        private readonly int _numUsers;

        public MaximumShiftingDictatorMechanism(int numUsers)
        {
            _dictatorState = 0;
            _numUsers = numUsers;
        }

        public int NextDictator()
        {
            int thisDictator = ((_dictatorState + 1) / TermLength) % _numUsers;
            _dictatorState++;
            return thisDictator;
        }

        public override void PrepareVoting()
        {
            int dictatorNum = NextDictator();
            Logger.Debug("Dictator: " + dictatorNum);
            foreach (var vote in AssignedVotes)
            {
                var dictatorUserVote = vote.Votes[dictatorNum];
                vote.Value = dictatorUserVote?.Value ?? DefaultVote;
            }
        }

        public override double GetUserValue(TransitionVote userVote, AssignedVote assignedVote)
        {
            throw new System.NotImplementedException();
        }
    }
}