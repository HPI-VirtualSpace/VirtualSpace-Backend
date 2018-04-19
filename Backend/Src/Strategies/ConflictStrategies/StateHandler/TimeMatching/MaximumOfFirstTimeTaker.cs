using System;
using System.Collections.Generic;

namespace VirtualSpace.Backend
{
    class MaximumOfFirstTimeTaker : TimeMatcher
    {
        public override void UpdateNumUser(int numUsers)
        {
            throw new NotImplementedException();
        }

        public override List<AssignedVote> AggregateTimes(List<AssignedVote> transitionVotingInfo)
        {
            foreach (AssignedVote vote in transitionVotingInfo)
            {
                //Logger.Debug($"{vote.TransitionName}, value {vote.Value}");
                foreach (var userVote in vote.Votes)
                {
                    if (userVote == null) continue;

                    vote.ExecutionLengthMs = Math.Max(vote.ExecutionLengthMs, userVote.ExecutionLengthMs[0]);
                    vote.PlanningTimestampMs = Math.Max(vote.PlanningTimestampMs, userVote.PlanningTimestampMs[0]);
                }
            }

            return transitionVotingInfo;
        }
    }
}