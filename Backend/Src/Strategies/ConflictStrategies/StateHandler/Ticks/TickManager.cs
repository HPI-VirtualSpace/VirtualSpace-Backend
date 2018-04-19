using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Dispatcher;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class TickManager
    {
        List<TickHistory> _tickHistories;
        List<float> _necessaryTicks;
        private List<UserTickData> _lastCalculatedTickData;
        public float LastNecessaryTick = float.MinValue;

        public TickManager()
        {
            _tickHistories = new List<TickHistory>();
            _necessaryTicks = new List<float>();
        }

        public void Add()
        {
            _tickHistories.Add(new TickHistory());
        }

        public void Remove(int userNum)
        {
            _tickHistories.RemoveAt(userNum);
        }

        public void AddTick(int userNum, Tick tickMessage)
        {
            _tickHistories[userNum].AddTick(tickMessage);
        }

        public void AddNecessaryTick(float necessaryTick)
        {
            _necessaryTicks.Add(necessaryTick);
        }

        internal void Reset()
        {
            _tickHistories.ForEach(tickHistory => tickHistory.Reset());
        }

        public List<float> TickRecommendation(int userNum)
        {
            return _lastCalculatedTickData[userNum].RecommendedTicks;
        }

        // seconds into future (depends on the feasability)
        private float FindTicksInSeconds = 10f;

        class UserTickData
        {
            public float LastTickSeconds;
            public float AvgDelta;
            public float StdDelta;
            public float TimeFrame;
            public int AvgNumTicksInTimeFrame => (int)(TimeFrame / RecommendedAvg + .5f);
            public List<GRBVar> RecommendedTickVars = new List<GRBVar>();
            public List<float> RecommendedTicks;
            public GRBVar RecommendedAvgVar;
            public float RecommendedAvg;
        }

        public void ClearUntil(float until)
        {
            _tickHistories.ForEach(history => history.ClearUntil(until));
        }

        // return false if not enough tick data
        public bool TryGetTickRecommendations()
        {
            var env = new GRBEnv();
            env.LogToConsole = 0;
            var model = new GRBModel(env);

            var userTickData = new List<UserTickData>();

            foreach (var history in _tickHistories)
            {
                var userNum = _tickHistories.IndexOf(history); // todo

                // todo time delta
                if (history.GetValidInbetweenCount < 5)
                {
                    Logger.Warn($"Not enough inbetweens for {userNum}, has {history.GetValidInbetweenCount}");
                    return false;
                }

                history.GetStatistics(out float average, out float std);

                var userTick = new UserTickData
                {
                    LastTickSeconds = history.LastTickSeconds, // this needs to be close enough to a min tick time
                    AvgDelta = average,
                    StdDelta = std,
                    TimeFrame = FindTicksInSeconds
                };

                userTickData.Add(userTick);

                GRBVar avgVar = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, $"avg_u{userNum}");
                userTick.RecommendedAvgVar = avgVar;
            }

            // variant 1: make them sync
            // variant 2: find a possible intersection point??? perhaps this is already handled by the prep time thing
            var stdOffsetFactor = .5f;
            var meanDeltaDistance = .1f;

            GRBLinExpr minimizeValue = 0;

            // finde kleinsten gemeinsamen nenner
            for (int userNum = 0; userNum < userTickData.Count; userNum++)
            {
                var userInfo = userTickData[userNum];
                
                model.AddConstr(userInfo.RecommendedAvgVar <= userInfo.AvgDelta + userInfo.StdDelta * stdOffsetFactor, $"ubound_avg_u{userNum}");
                model.AddConstr(userInfo.RecommendedAvgVar >= userInfo.AvgDelta - userInfo.StdDelta * stdOffsetFactor, $"lbound_avg_u{userNum}");

                GRBVar absAvgDelta = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, $"abs_avg_delta_u{userNum}");

                model.AddConstr(userInfo.RecommendedAvgVar - userInfo.AvgDelta <= absAvgDelta, $"abs_avg_delta_r-a_u{userNum}");
                model.AddConstr(userInfo.AvgDelta - userInfo.RecommendedAvgVar <= absAvgDelta, $"abs_avg_delta_a-r_u{userNum}");

                minimizeValue += absAvgDelta;

                for (int otherNum = userNum + 1; otherNum < userTickData.Count; otherNum++)
                {
                    var otherInfo = userTickData[otherNum];

                    GRBVar interval = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, $"interval_u{userNum}_u{otherNum}");

                    GRBVar smaller;
                    GRBVar larger;
                    if (userInfo.AvgDelta <= otherInfo.AvgDelta)
                    {
                        smaller = userInfo.RecommendedAvgVar;
                        larger = otherInfo.RecommendedAvgVar;
                    }
                    else
                    {
                        smaller = otherInfo.RecommendedAvgVar;
                        larger = userInfo.RecommendedAvgVar;
                    }
                    
                    model.AddConstr(interval * smaller <= larger + meanDeltaDistance,
                        $"interval_u{userNum}_u{otherNum}_1");
                    model.AddConstr(interval * smaller >= larger - meanDeltaDistance,
                        $"interval_u{userNum}_u{otherNum}_2");
                }
            }

            model.SetObjective(minimizeValue);
            model.ModelSense = GRB.MINIMIZE;
            model.Update();
            model.Optimize();

            // if didn't a solution, cancel
            if (model.Status != GRB.Status.OPTIMAL)
            {
                Logger.Warn($"Didn't find solution to time conditions. Gurobi status: {model.Status}");
                // todo
                return false;
            }

            foreach (var userInfo in userTickData)
            {
                userInfo.RecommendedAvg = (float)userInfo.RecommendedAvgVar.X;
            }

            // (1)necessaryTick from a transition
            // or!: (3)the largest transition ==> this for now because it's easiest
            // or!: (2)the most convienient point everyone can convieniently hit
            var necessaryTick = 2 * userTickData.Max(userInfo => userInfo.RecommendedAvg);
            userTickData.ForEach(userTick => userTick.TimeFrame = necessaryTick);
            
            foreach (var userInfo in userTickData)
            {
                model.Reset();

                minimizeValue = 0;

                // do three times
                // check if any of the last hit the necessary constraint 
                // or the one before
                // or the one before
                // last tick

                for (int i = 0; i < userInfo.AvgNumTicksInTimeFrame; i++)
                {
                    GRBVar tickVar = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, $"tick_t{i}");
                    userInfo.RecommendedTickVars.Add(tickVar);
                }
                
                for (int i = 0; i < userInfo.RecommendedTickVars.Count; i++)
                {
                    var tick = userInfo.RecommendedTickVars[i];

                    if (i == 0)
                    {
                        GRBVar interval = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS,
                            $"interval_to_last_tick");

                        // soft constraint!
                        model.AddConstr(
                            tick - userInfo.LastTickSeconds <=
                            interval * (userInfo.RecommendedAvg + userInfo.StdDelta * stdOffsetFactor),
                            $"interval_to_last_tick_uconstraint");
                        model.AddConstr(
                            tick - userInfo.LastTickSeconds >=
                            interval * (userInfo.RecommendedAvg - userInfo.StdDelta * stdOffsetFactor),
                            $"interval_to_last_tick_lconstraint");
                    }
                    else if (i < userInfo.AvgNumTicksInTimeFrame - 1)
                    {
                        var nextTick = userInfo.RecommendedTickVars[i + 1];

                        model.AddConstr(
                            nextTick - tick <= userInfo.RecommendedAvg + userInfo.StdDelta * stdOffsetFactor,
                            $"delta{i}_uconstraint");
                        model.AddConstr(
                            nextTick - tick >= userInfo.RecommendedAvg - userInfo.StdDelta * stdOffsetFactor,
                            $"delta{i}_lconstraint");
                    }
                }

                // delta distance minimization
                minimizeValue = MinimizeDeltaDistance(model, minimizeValue, userInfo.RecommendedTickVars[userInfo.AvgNumTicksInTimeFrame - 1],
                    necessaryTick, "lastTickToNecessary");
                if (userInfo.AvgNumTicksInTimeFrame > 1)
                    minimizeValue = MinimizeDeltaDistance(model, minimizeValue, userInfo.RecommendedTickVars[userInfo.AvgNumTicksInTimeFrame - 2],
                        necessaryTick, "preLastTickToNecessary");
                if (userInfo.AvgNumTicksInTimeFrame > 2)
                    minimizeValue = MinimizeDeltaDistance(model, minimizeValue, userInfo.RecommendedTickVars[userInfo.AvgNumTicksInTimeFrame - 3],
                        necessaryTick, "prePreLastTickToNecessary");

                model.SetObjective(minimizeValue);
                model.ModelSense = GRB.MINIMIZE;
                model.Update();
                model.Optimize();

                if (model.Status != GRB.Status.OPTIMAL)
                {
                    Logger.Warn($"Didn't find solution for user. Gurobi status: {model.Status}");
                    return false;
                }

                userInfo.RecommendedTicks = userInfo.RecommendedTickVars.Select(var => (float)var.X).ToList();
            }

            LastNecessaryTick = necessaryTick;
            _lastCalculatedTickData = userTickData;

            return true;
        }

        public static GRBLinExpr MinimizeDeltaDistance(GRBModel model, GRBLinExpr value, GRBVar v, double c, string name)
        {
            GRBVar absAvgDelta = model.AddVar(0, double.PositiveInfinity, 0, GRB.CONTINUOUS, name + "_absval");
            
            model.AddConstr(v - c <= absAvgDelta, name + "_v-c_bound");
            model.AddConstr(c - v <= absAvgDelta, name + "_c-v_bound");

            value += absAvgDelta;

            return value;
        }

    }

    class TickHistory
    {
        List<TickInbetween> _inbetweens;
        internal int GetValidInbetweenCount => DefinedDeltaInbetweens.Count();

        IEnumerable<TickInbetween> DefinedDeltaInbetweens => _inbetweens.Where(inbetween => !float.IsNaN(inbetween.Delta));

        public float LastTickSeconds => !_inbetweens.Any() ? float.NaN : _inbetweens.Last().FirstTick;

        public TickHistory()
        {
            _inbetweens = new List<TickInbetween>();
        }

        public void AddTick(Tick tickMessage)
        {
            var inbetweens = new TickInbetween();
            inbetweens.FirstTick = tickMessage.Second;
            inbetweens.TickNumInChain = 0;

            if (tickMessage.InRelationToPreviousTick && _inbetweens.Any())
            {
                var previousInbetween = _inbetweens.Last();

                previousInbetween.Next = inbetweens;
                previousInbetween.SecondTick = inbetweens.FirstTick;

                inbetweens.Previous = previousInbetween;

                inbetweens.TickNumInChain = previousInbetween.TickNumInChain + 1;
            }
        }

        public void GetStatistics(out float inbetweenAverage, out float inbetweenStd)
        {
            var inbetweenAvg = DefinedDeltaInbetweens.Average(tick => tick.Delta);
            var inbetweenSd =
                (float)Math.Sqrt(DefinedDeltaInbetweens.Sum(inbetween => (inbetween.Delta - inbetweenAvg) * (inbetween.Delta - inbetweenAvg)) / _inbetweens.Count);

            inbetweenAverage = inbetweenAvg;
            inbetweenStd = inbetweenSd;
        }

        public void ClearUntil(float firstSeconds)
        {
            _inbetweens.RemoveAll(inbetween => inbetween.FirstTick < firstSeconds);
        }

        public void Reset()
        {
            _inbetweens.Clear();
        }
    }

    class TickInbetween
    {
        public TickInbetween Previous;
        public TickInbetween Next;
        public float FirstTick = float.NaN;
        public float SecondTick = float.NaN;
        public float Delta
        {
            get
            {
                if (float.IsNaN(FirstTick) || float.IsNaN(SecondTick))
                    return float.NaN;
                return SecondTick - FirstTick;
            }
        }
        public int TickNumInChain;
    }
}
