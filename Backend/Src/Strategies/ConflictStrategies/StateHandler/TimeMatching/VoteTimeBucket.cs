using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class VoteTimeBucket
    {
        private double _minAllowedExecTime = double.MinValue;
        private double _maxAllowedExecTime = double.MaxValue;
        private double _minAllowedPlanningTime = double.MinValue;
        private double _maxAllowedPlanningTime = double.MaxValue;

        private readonly int _numUsers;
        public readonly TransitionVote[] UserNumToVotes;
        public readonly int[] UserNumToTimeNum;
        private readonly double _maximumExecOffset;
        private readonly double _maximumPlanningOffset;
        public double PlanningDelta;
        public double ExecDelta;
        public double DeltaValue;

        public VoteTimeBucket(int numUsers, double maximumPlanningOffset, double maximumExecOffset)
        {
            _numUsers = numUsers;
            UserNumToVotes = new TransitionVote[numUsers];
            UserNumToTimeNum = new int[numUsers];
            _maximumExecOffset = maximumExecOffset;
            _maximumPlanningOffset = maximumPlanningOffset;
        }

        public string TimesString()
        {
            return
                $"prep min={_minAllowedPlanningTime}, max={_maxAllowedPlanningTime}, exec min={_minAllowedExecTime}, max={_maxAllowedExecTime}";
        }

        public void AddToBucket(int userNum, int timeNum, TransitionVote userVote)
        {
            var execTime = userVote.ExecutionLengthMs[timeNum];
            var planningTime = userVote.PlanningTimestampMs[timeNum];

            _minAllowedPlanningTime = Math.Max(_minAllowedPlanningTime, planningTime - _maximumPlanningOffset);
            _maxAllowedPlanningTime = Math.Min(_maxAllowedPlanningTime, planningTime + _maximumPlanningOffset);

            _minAllowedExecTime = Math.Max(_minAllowedExecTime, execTime - _maximumExecOffset);
            _maxAllowedExecTime = Math.Min(_maxAllowedExecTime, execTime + _maximumExecOffset);

            UserNumToTimeNum[userNum] = timeNum;
            UserNumToVotes[userNum] = userVote;
        }

        private bool IsMissing(int i) => UserNumToVotes[i] == null;

        public bool IsMissingAny() => MissingVoteNums().Count > 0;

        private List<int> MissingVoteNums()
        {
            List<int> missingVotes = new List<int>();
            for (int i = 0; i < UserNumToVotes.Length; i++)
            {
                if (IsMissing(i)) missingVotes.Add(i);
            }

            return missingVotes;
        }

        public bool WorksWithBucket(int timeNum, TransitionVote userVote)
        {
            var execTime = userVote.ExecutionLengthMs[timeNum];
            var planningTime = userVote.PlanningTimestampMs[timeNum];

            return _minAllowedExecTime <= execTime && execTime <= _maxAllowedExecTime &&
                   _minAllowedPlanningTime <= planningTime && planningTime <= _maxAllowedPlanningTime;
        }

        public VoteTimeBucket Clone()
        {
            var newBucket = new VoteTimeBucket(_numUsers, _maximumPlanningOffset, _maximumExecOffset);
            for (int i = 0; i < UserNumToVotes.Length; i++)
            {
                newBucket.UserNumToVotes[i] = UserNumToVotes[i];
                newBucket.UserNumToTimeNum[i] = UserNumToTimeNum[i];
            }
            return newBucket;
        }
    }
}