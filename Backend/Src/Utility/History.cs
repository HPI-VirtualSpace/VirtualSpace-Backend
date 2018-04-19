using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class History
    {
        private readonly LinkedList<double> _history;
        private readonly int _maxSize;
        private bool _changedSinceLastAverage = true;
        private bool _changedSinceLastStd = true;
        private double _average;
        private double _std;

        public double Average
        {
            get
            {
                if (_changedSinceLastAverage)
                {
                    _average = _CalcAverage();
                    _changedSinceLastAverage = false;
                }

                return _average;
            }
        }

        public double Std
        {
            get
            {
                if (_changedSinceLastStd)
                {
                    _std = _CalcStd();
                    _changedSinceLastStd = false;
                }

                return _std;
            }
        }

        public History(int maxSize)
        {
            _history = new LinkedList<double>();
            _maxSize = maxSize;
        }

        public void AddFront(double value)
        {
            while (_history.Count > _maxSize) _history.RemoveLast();
            _history.AddFirst(value);
            _changedSinceLastAverage = true;
            _changedSinceLastStd = true;
        }

        public void AddLast(double value)
        {
            _history.AddLast(value);
            _changedSinceLastAverage = true;
        }

        public double RemoveLast()
        {
            if (_history.Count == 0) return double.NaN;
            double value = _history.Last.Value;
            _history.RemoveLast();
            _changedSinceLastAverage = true;
            _changedSinceLastStd = true;
            return value;
        }
        
        private double _CalcAverage()
        {
            return _history.Average();
        }
        
        private double _CalcStd()
        {
            if (double.IsNaN(Average)) return double.NaN;

            var variance = _history.Sum(v => (v - Average) * (v - Average)) / _history.Count;
            var std = Math.Sqrt(variance);

            return std;
        }
    }

    public class MultiHistory
    {
        private List<History> _histories;
        private readonly double _initialDefault;
        private int NumHistories => _histories.Count;
        private readonly int _historySize;

        public MultiHistory(int numHistories, int historySize = 5, double defaultValue = double.NaN)
        {
            _histories = new List<History>(numHistories);

            _historySize = historySize;
            _initialDefault = defaultValue;

            ResetAll();
        }

        public void AddHistory()
        {
            _histories.Add(new History(_historySize));
        }

        public void RemoveHistory(int historyNum)
        {
            _histories.RemoveAt(historyNum);
        }

        public void AddFront(int historyNum, double value)
        {
            _histories[historyNum].AddFront(value);
        }

        public void AddAllLast(double[] last)
        {
            if (last.Length != NumHistories)
                Logger.Warn($"Adding a different number of values ({last.Length}) than there are histories ({NumHistories}).");

            for (int i = 0; i < last.Length; i++)
            {
                if (!double.IsNaN(last[i]))
                    AddLast(i, last[i]);
            }
        }

        public void AddLast(int i, double value)
        {
            _histories[i].AddLast(value);
        }

        public double Average(int i)
        {
            return _histories[i].Average;
        }

        public double StdAverage(int i)
        {
            return _histories[i].Std;
        }

        public double AverageAverage(List<float> weights = null)
        {
            double runningSum = 0;
            double weightSum = 0;
            for (int i = 0; i < NumHistories; i++)
            {
                var history = _histories[i];
                var weight = weights == null ? 1 : weights[i];
                runningSum += history.Average * weight;
                weightSum += weight;
            }
            return runningSum / weightSum;
        }

        public double[] GetAndRemoveLast()
        {
            double[] last = new double[NumHistories];

            for (int i = 0; i < NumHistories; i++)
            {
                last[i] = _histories[i].RemoveLast();
            }

            return last;
        }

        public double StdOfAverageOfAverages(List<float> weights = null)
        {
            double runningSum = 0;
            double weightSum = 0;
            double avgAvg = AverageAverage();

            for (int i = 0; i < NumHistories; i++)
            {
                var weight = weights == null ? 1 : weights[i];
                runningSum +=
                    _histories.Sum(history => weight * (history.Average - avgAvg) * (history.Average - avgAvg));
                weightSum += weight;
            }

            double std = Math.Sqrt(runningSum / weightSum);

            return std;
        }

        public void ResetAll()
        {
            for (int historyNum = 0; historyNum < NumHistories; historyNum++)
            {
                Reset(historyNum);
            }
        }

        public void Reset(int historyNum)
        {
            _histories[historyNum] = new History(_historySize);
            if (!double.IsNaN(_initialDefault))
            {
                for (int i = 0; i < _historySize; i++)
                {
                    _histories[historyNum].AddFront(_initialDefault);
                }
            }
        }
    }
}