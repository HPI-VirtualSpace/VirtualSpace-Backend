using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class Quality
    {
        public AssignedVote Vote;
        public double Avg;
        public double Sd;

        public Quality(AssignedVote vote, double avg, double sd)
        {
            Vote = vote;
            Avg = avg;
            Sd = sd;
        }
    }

    public class AverageFairness : VotingMechanism
    {
        private const double DefaultVote = 0.5;
        private readonly MultiHistory _valueHistory;
        private int _numUsers;
        private bool _useTimeValue;

        public AverageFairness(int numUsers, bool useTimeValue = false)
        {
            _numUsers = numUsers;
            _valueHistory = new MultiHistory(_numUsers, 5, DefaultVote);
            _useTimeValue = useTimeValue;
        }

        public override void AddUser()
        {
            _numUsers++;
            _valueHistory.AddHistory();
        }

        public override void RemoveUser(int userNum)
        {
            _numUsers--;
            _valueHistory.RemoveHistory(userNum);
        }

        public void NormalizeVotes()
        {
            // normalize for each user
            for (var i = 0; i < UserVotings.Count; i++)
            {
                var userVoting = UserVotings[i];
                var userMax = userVoting.Votes.Max(votes => votes.Value);

                if (userMax > 0)
                {
                    //Logger.Debug("User " + userVoting.UserId);
                    userVoting.Votes.ForEach(vote =>
                    {
                        var normalizedValue = (userMax == 0 ? 1 : vote.Value / userMax);
                        //Logger.Debug("New normalized value for " + vote.TransitionName + ": " + normalizedValue);
                        vote.NormalizedValue = normalizedValue;
                    });
                }
            }

            // normalize for all users
            //var _maxNormalValue = UserVotings.Max(voting => voting.Votes.Max(vote => vote.NormalizedValue));
            //if (_maxNormalValue > 0)
            //{
            //    foreach (var userVoting in UserVotings)
            //    {
            //        userVoting.Votes.ForEach(vote =>
            //        {
            //            vote.NormalizedValue /= _maxNormalValue;
            //        });
            //    }
            //}
        }

        public override void PrepareVoting()
        {
            NormalizeVotes();

            _bestVote = null;
        }

        public override void UnprepareVoting()
        {
            
        }
        
        private AssignedVote _bestVote;
        private const int NumBestAvgConsidered = 3;
        public override AssignedVote GetBestVote()
        {
            if (_bestVote != null) return _bestVote;

            var lastValues = _valueHistory.GetAndRemoveLast();

            var qualities = new List<Quality>();
            foreach (var assignedVote in AssignedVotes)
            {
                double[] expectedValues = new double[_numUsers];
                //Logger.Debug("Vote: " + assignedVote.Transition);
                for (int userNum = 0; userNum < _numUsers; userNum++)
                {
                    var userVote = assignedVote.Votes[userNum];
                    expectedValues[userNum] = GetUserValue(userVote, assignedVote);
                    
                    //Logger.Debug($"{userVote.Transition}: {userVote.Value}");
                }

                _valueHistory.AddAllLast(expectedValues);

                double expectedAvgAvg = _valueHistory.AverageAverage(UserPriorities);
                double expectedStd = _valueHistory.StdOfAverageOfAverages(UserPriorities);

                qualities.Add(new Quality(assignedVote, expectedAvgAvg, expectedStd));

                //Logger.Debug($"{assignedVote.TransitionName}: Avg: {expectedAvgAvg}, Std: {expectedStd}");

                _valueHistory.GetAndRemoveLast();
            }

            _valueHistory.AddAllLast(lastValues);

            //qualities.Sort((tupleA, tupleB) => tupleA.Avg.CompareTo(tupleB.Avg));
            //int numConsidered = Math.Min(qualities.Count, NumBestAvgConsidered);

            //var bestQuality = qualities.Last();
            //var bestQualities = qualities.GetRange(qualities.Count - numConsidered, numConsidered);
            //var bestQuality = bestQualities.OrderBy(quality => quality.Sd).FirstOrDefault();

            // todo put in single for loop
            var bestAvg = qualities.Max(quality => quality.Avg);
            var bestSd = qualities.Max(quality => quality.Sd);

            var bestValue = double.MinValue;
            Quality bestQuality = null;

            foreach (var quality in qualities)
            {
                var deltaBestAvg = 1 - Math.Abs(quality.Avg - bestAvg);
                var deltaBestSd = 1 - Math.Abs(quality.Sd - bestSd) / .5;
                var deltaOptimalAvg = quality.Avg;
                var deltaOptimalSd = 1 - quality.Sd;
                //var value = deltaBestAvg * deltaBestAvg + 1.3 * deltaBestSd * deltaBestSd + deltaBestAvg * deltaBestSd;
                var value = 0D;
                //value += deltaOptimalAvg * deltaOptimalAvg;
                value += deltaOptimalAvg * deltaOptimalAvg + 1.3 * deltaOptimalSd * deltaOptimalSd + deltaOptimalAvg * deltaOptimalSd;
                if (bestValue < value)
                {
                    bestValue = value;
                    bestQuality = quality;
                }
            }
            
            if (bestQuality == null)
            {
                Logger.Warn("AverageFairness: best vote is null");
                return null;
            }

            Logger.Debug($"AverageFairness: best vote {bestQuality.Vote.TransitionName} selection creates history: avg={bestQuality.Avg}, sd={bestQuality.Sd}");
            
            _bestVote = bestQuality?.Vote;

            return _bestVote;
        }

        public override double GetUserValue(TransitionVote userVote, AssignedVote assignedVote)
        {
            var value = userVote.NormalizedValue;
            if (_useTimeValue) value *= assignedVote.TimeValue;
            if (userVote.Transition == VSUserTransition.Undefocus) value /= 2;
            return value;
        }

        public override void ConsiderSelection(AssignedVote selectedVote)
        {
            for (int userNum = 0; userNum < selectedVote.Votes.Length; userNum++)
            {
                var userVote = selectedVote.Votes[userNum];
                var userValue = GetUserValue(userVote, selectedVote);

                _valueHistory.AddFront(userNum, userValue);
            }
        }

        public override void Reset()
        {
            _valueHistory.ResetAll();
        }
    }
}