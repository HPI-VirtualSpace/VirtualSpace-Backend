using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal class ConditionTimeMatcher : TimeMatcher
    {
        private int _numUser;
        private ConditionSolver _solver;

        public ConditionTimeMatcher(int numUser)
        {
            _numUser = numUser;
            _solver = new ConditionSolver();
        }

        public override void UpdateNumUser(int numUser)
        {
            _numUser = numUser;
        }

        public override List<AssignedVote> AggregateTimes(List<AssignedVote> assignedVotes)
        {
            foreach (var assignedVote in assignedVotes)
            {
                PrepareSolver(assignedVote, true);

                if (_solver.TrySolve(out double prepSeconds, out double execSeconds))
                {
                    assignedVote.ExecutionLengthMs = execSeconds * 1000;
                    assignedVote.PlanningTimestampMs = prepSeconds * 1000;
                }
                else
                {
                    PrepareSolver(assignedVote, false);

                    if (_solver.TrySolve(out prepSeconds, out execSeconds))
                    {
                        assignedVote.ExecutionLengthMs = execSeconds * 1000;
                        assignedVote.PlanningTimestampMs = prepSeconds * 1000;
                    }
                    else
                    {
                        Logger.Warn($"Couldn't find any good time combination for {assignedVote.TransitionName}");
                        assignedVote.ExecutionLengthMs = double.NaN;
                        assignedVote.PlanningTimestampMs = double.NaN;
                    }
                }
            }

            var goodTransitions = assignedVotes
                .Where(assignedVote => !double.IsNaN(assignedVote.PlanningTimestampMs) && !double.IsNaN(assignedVote.ExecutionLengthMs))
                .ToList();

            return goodTransitions;
        }

        private void PrepareSolver(AssignedVote assignedVote, bool useValueFunction, bool debugPrint=false)
        {
            _solver.Reset();

            float calculationTime = Time.NowSeconds;
            //_solver.AddCalculationTime(calculationTime);

            if (debugPrint)
                Logger.Debug("=== TIME CONDITIONS ===");
            for (int userNum = 0; userNum < _numUser; userNum++)
            {
                var userVoting = assignedVote.Votes[userNum];
                if (userVoting != null)
                {
                    if (debugPrint)
                        Logger.Debug("User " + userNum);
                    _solver.AddArrivalTimeForUser(userVoting.ArrivalTime, userNum);
                    if (useValueFunction)
                        _solver.AddValueFunction(userNum, UserPriorities[userNum], userVoting.ValueFunction);
                    _solver.AddConditions(userNum, userVoting.TimeConditions, debugPrint);
                }
            }
            if (debugPrint)
                Logger.Debug("=======================");
        }
    }

    internal abstract class TimeMatcher
    {
        protected List<float> UserPriorities;
        protected StateStrategy Strategy;

        public void SetStateStrategy(StateStrategy strategy)
        {
            Strategy = strategy;
        }

        public void PrepareVotes(List<TransitionVoting> votings)
        {

        }

        public abstract void UpdateNumUser(int numUsers);

        public virtual void SetUserPriorities(List<float> userPriorities)
        {
            UserPriorities = userPriorities;
        }
        
        public abstract List<AssignedVote> AggregateTimes(List<AssignedVote> assignedVotes);
        
        public virtual void Reset() { }

        public void OffsetTimes(List<TransitionVoting> currentVotings)
        {

        }

        public void UnoffsetTimes(List<TransitionVoting> currentVotings)
        {

        }
    }
}
