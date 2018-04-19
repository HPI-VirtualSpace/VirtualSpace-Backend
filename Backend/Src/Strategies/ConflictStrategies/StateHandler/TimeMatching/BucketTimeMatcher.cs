using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class BucketTimeMatcher : TimeMatcher
    {
        private int _numUsers;
        private readonly double _maximumPlanningTimeOffsetMs;
        private readonly double _maximumExecTimeOffsetMs;

        public BucketTimeMatcher(int numUsers, double maximumPlanningTimeOffsetMs, double maximumExecTimeOffsetMs)
        {
            _numUsers = numUsers;
            _maximumPlanningTimeOffsetMs = maximumPlanningTimeOffsetMs;
            _maximumExecTimeOffsetMs = maximumExecTimeOffsetMs;
        }

        public override void UpdateNumUser(int numUsers)
        {
            _numUsers = numUsers;
        }

        public override List<AssignedVote> AggregateTimes(List<AssignedVote> assignedVotes)
        {
            //Logger.Debug($"=== Bucket Time Matcher ===");

            Dictionary<AssignedVote, VoteTimeBucket> votesToBuckets = new Dictionary<AssignedVote, VoteTimeBucket>();
            foreach (AssignedVote assignedVote in assignedVotes)
            {
                List<VoteTimeBucket> buckets = new List<VoteTimeBucket>();

                bool createdInitialBuckets = false;
                for (int userVoteNum = 0; userVoteNum < _numUsers; userVoteNum++)
                {
                    var userVote = assignedVote.Votes[userVoteNum];
                    if (userVote == null) continue; // todo

                    if (!createdInitialBuckets)
                    {
                        int numTimes = userVote.ExecutionLengthMs.Count;

                        for (int timeNum = 0; timeNum < numTimes; timeNum++)
                        {
                            var bucket = new VoteTimeBucket(_numUsers, _maximumPlanningTimeOffsetMs, _maximumExecTimeOffsetMs);
                            bucket.AddToBucket(userVoteNum, timeNum, userVote);
                            buckets.Add(bucket);
                        }

                        createdInitialBuckets = true;
                    }
                    else
                    {
                        Dictionary<VoteTimeBucket, List<int>> bucketToTimeNum = new Dictionary<VoteTimeBucket, List<int>>();

                        int numTimes = userVote.ExecutionLengthMs.Count;

                        for (int timeNum = 0; timeNum < numTimes; timeNum++)
                        {
                            foreach (var bucket in buckets)
                            {
                                if (bucket.WorksWithBucket(timeNum, userVote))
                                {
                                    if (!bucketToTimeNum.ContainsKey(bucket)) bucketToTimeNum[bucket] = new List<int>();

                                    bucketToTimeNum[bucket].Add(timeNum);
                                }
                            }
                        }

                        //if (bucketToTimeNum.Count == 0)
                        //{
                        //    Logger.Debug($"Couldn't find any bucket for {userVoteNum}");
                        //    Logger.Debug($"player times");
                        //    for (int timeNum = 0; timeNum < numTimes; timeNum++)
                        //    {
                        //        Logger.Debug($"prep {userVote.PlanningTimestampMs[timeNum]}, exec {userVote.ExecutionLengthMs[timeNum]}");
                        //    }
                        //    foreach (var bucket in buckets)
                        //    {
                        //        Logger.Debug($"bucket {bucket.TimesString()}");
                        //    }
                        //}

                        foreach (var pair in bucketToTimeNum)
                        {
                            var bucket = pair.Key;
                            var timeNums = pair.Value;

                            if (timeNums.Count == 1)
                            {
                                bucket.AddToBucket(userVoteNum, timeNums.First(), userVote);
                            }
                            else
                            {
                                int numNewBuckets = timeNums.Count - 1;

                                for (int i = 0; i < numNewBuckets; i++)
                                {
                                    var newBucket = bucket.Clone();
                                    newBucket.AddToBucket(userVoteNum, timeNums[i], userVote);
                                    buckets.Add(newBucket);
                                }

                                bucket.AddToBucket(userVoteNum, timeNums[timeNums.Count - 1], userVote);
                            }
                        }

                    }
                }

                var goodBuckets = buckets.Where(bucket => !bucket.IsMissingAny()).ToList();

                if (goodBuckets.Count < 1)
                {
                    Logger.Warn($"Couldn't find any good time combination for {assignedVote.TransitionName}");
                    Logger.Warn($"now: {Time.NowSeconds}");
                    foreach (var userVote in assignedVote.Votes)
                    {
                        Logger.Debug($"user {userVote.PlanningTimestampMs.ToPrintableString()}");
                        Logger.Debug($"user {userVote.ExecutionLengthMs.ToPrintableString()}");
                    }
                    assignedVote.TimeValue = 0;
                    assignedVote.ExecutionLengthMs = double.NaN;
                    assignedVote.PlanningTimestampMs = double.NaN;
                    continue;
                }

                var minDeltaValue = double.MaxValue;
                VoteTimeBucket bestBucket = null;
                foreach (var bucket in goodBuckets)
                {
                    var planningTimes =
                        bucket.UserNumToVotes.Select((vote, userNum) => vote.PlanningTimestampMs[bucket.UserNumToTimeNum[userNum]]).ToList();
                    var executionTimes =
                        bucket.UserNumToVotes.Select((vote, userNum) => vote.ExecutionLengthMs[bucket.UserNumToTimeNum[userNum]]).ToList();
                    var planningDelta = planningTimes.Max() - planningTimes.Min();
                    var executionDelta = executionTimes.Max() - executionTimes.Min();
                    var deltaValue = PlanningDeltaWeight * planningDelta + ExecDeltaWeight * executionDelta;

                    bucket.PlanningDelta = planningDelta;
                    bucket.ExecDelta = executionDelta;
                    bucket.DeltaValue = deltaValue;

                    if (deltaValue < minDeltaValue)
                    {
                        bestBucket = bucket;
                        minDeltaValue = deltaValue;
                    }
                }

                assignedVote.TimeValue = minDeltaValue;

                MaximumTimes(assignedVote, bestBucket, out double aggrPlanning, out double aggrExec);
                
                assignedVote.ExecutionLengthMs = aggrExec * 1000;
                assignedVote.PlanningTimestampMs = aggrPlanning * 1000;

                //votesToBuckets[assignedVote] = bestBucket;
            }

            var goodTransitions = assignedVotes
                .Where(assignedVote => !double.IsNaN(assignedVote.PlanningTimestampMs) && !double.IsNaN(assignedVote.ExecutionLengthMs))
                .ToList();

            if (goodTransitions.Count > 0)
            {
                //var maxTimeValue = goodTransitions.Values.Select(vote => vote.TimeValue).Max();
                var worstMax = (PlanningDeltaWeight + ExecDeltaWeight) * 5000;
                foreach (var vote in goodTransitions)
                {
                    var weight = vote.TimeValue / worstMax > 1 ? 1 : vote.TimeValue / worstMax;
                    //Logger.Debug($"{vote.TransitionName} has absolute time value {vote.TimeValue}");
                    vote.TimeValue = .25 + .75 * (1 - weight);
                    //Logger.Debug($"{vote.TransitionName} has time value {vote.TimeValue}");
                }
            }

            //Logger.Debug($"{goodTransitions.Count()}/{assignedVotes.Count} transitions have matching planning/execution times");
            //Logger.Debug("======");

            return goodTransitions;
        }

        private float PlanningDeltaWeight = 1f;
        private float ExecDeltaWeight = 1f;

        private void AvgTimes(AssignedVote assignedVote, VoteTimeBucket bestBucket, out double avgPlanning,
            out double avgExec)
        {
            //Logger.Debug($"Times for {assignedVote.TransitionName}");
            avgPlanning = 0;
            avgExec = 0;
            for (int userNum = 0; userNum < _numUsers; userNum++)
            {
                var userPlanning = assignedVote.Votes[userNum]
                    .PlanningTimestampMs[bestBucket.UserNumToTimeNum[userNum]];
                var userExec = assignedVote.Votes[userNum]
                    .ExecutionLengthMs[bestBucket.UserNumToTimeNum[userNum]];

                //Logger.Debug($"User {userNum}: planning={userPlanning}, exec={userExec}");

                avgPlanning += userPlanning;
                avgExec += userExec;
            }
            avgPlanning /= _numUsers;
            avgExec /= _numUsers;
            //Logger.Debug($"Average: planning={avgPlanning}, exec={avgExec}");
        }

        private void MaximumTimes(AssignedVote assignedVote, VoteTimeBucket bestBucket, out double maxPlanning,
            out double maxExec)
        {
            //Logger.Debug($"Times for {assignedVote.TransitionName}");
            maxPlanning = 0;
            maxExec = 0;
            for (int userNum = 0; userNum < _numUsers; userNum++)
            {
                var userPlanning = assignedVote.Votes[userNum]
                    .PlanningTimestampMs[bestBucket.UserNumToTimeNum[userNum]];
                var userExec = assignedVote.Votes[userNum]
                    .ExecutionLengthMs[bestBucket.UserNumToTimeNum[userNum]];

                //Logger.Debug($"User {userNum}: planning={userPlanning}, exec={userExec}");

                maxPlanning = Math.Max(userPlanning, maxPlanning);
                maxExec = Math.Max(userExec, maxExec);
            }

            //Logger.Debug($"Max: planning={avgPlanning}, exec={avgExec}");
        }
    }
}