using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class DistanceWalked
    {
        private string _infoFilePath;
        private string _dataFilePath;
        private bool _skipHeader;

        class Translation
        {
            public double Time;
            public Vector Position;
            public Vector Offset;

            public Translation(double time, Vector position, Vector offset)
            {
                Time = time;
                Position = position;
                Offset = offset;
            }
        }

        class Queue<T> : IEnumerable<T>
        {
            private int _size;
            private LinkedList<T> _queue;

            public Queue(int size)
            {
                _size = size;
                _queue = new LinkedList<T>();
            }

            public void AddFront(T elem)
            {
                while (_queue.Count >= _size) RemoveLast();

                _queue.AddFirst(elem);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _queue.GetEnumerator();
            }

            public T RemoveLast()
            {
                var t = _queue.Last.Value;
                _queue.RemoveLast();
                return t;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Clear()
            {
                _queue.Clear();
            }
        }

        public DistanceWalked(string infoFilePath, string dataFilePath, bool skipHeader = true)
        {
            _infoFilePath = infoFilePath;
            _dataFilePath = dataFilePath;
            _skipHeader = skipHeader;
        }

        public void Run()
        {
            string[] infoLines = File.ReadAllLines(_infoFilePath);

            var infoSelections = (from line in _skipHeader ? infoLines.Skip(1) : infoLines
                                  let data = line.Split(';')
                                  select new
                                  {
                                      PartId = data[0],
                                      StartOffset = Helper.ConvertToLong(data[2]),
                                      StudyType = data[1]
                                  }).Distinct().ToList();

            string[] dataLines = File.ReadAllLines(_dataFilePath);

            // select
            var selections = (from line in _skipHeader ? dataLines.Skip(1) : dataLines
                              let data = line.Split(';')
                              let studyType = data[2]
                              select new
                              {
                                  PartId = data[1],
                                  Game = data[5],
                                  StudyType = studyType
                              }).Distinct().ToList();

            Logger.Info("PartId;StudyType;Game;MinTime;MaxTime;Duration;DistanceWalkedSimple;DistanceWalkedCorrected;DistanceWalkedInIntervals;MaxSpeed;AvgMinDistOthers");

            var totalTimeDiff = 0f;
            var totalTimeDiffs = 0;
            foreach (var selection in selections)
            {
                var query = (from line in _skipHeader ? dataLines.Skip(1) : dataLines
                            let data = line.Split(';')
                            let timestamp = Helper.ConvertToLong(data[9])
                            where data[5] == selection.Game && data[1] == selection.PartId && data[2] == selection.StudyType
                                  &&
                                  infoSelections
                                      .First(infoSelection => infoSelection.PartId == selection.PartId &&
                                                              infoSelection.StudyType == selection.StudyType).StartOffset < timestamp
                            select new
                            {
                                PartId = Helper.ConvertToInteger(data[1]),
                                Timestamp = timestamp,
                                Game = data[5],
                                StudyType = data[2],
                                Rotation = Helper.ConvertToDouble(data[8]),
                                PositionX = Helper.ConvertToDouble(data[11]),
                                PositionY = Helper.ConvertToDouble(data[12]),
                                MinOtherDist = Helper.ConvertToDouble(data[33]),
                                MinDistToPoly = Helper.ConvertToDouble(data[34])
                            }).ToList();

                // DURATION, MIN/MAX TIME
                var min = double.MaxValue;
                var max = double.MinValue;

                var numRows = query.Count;

                var minDistOther = 0f;
                var numMinDistOther = 0;
                var distanceWalkedSimple = 0f;
                var distanceWalkedCorrected = 0f;
                var distanceWalkedInIntervals = 0f;
                var distanceWalkedLocally = Vector.Zero;
                var distanceQueue = new Queue<Translation>(100); // clearing when enough
                var speedQueue = new Queue<Translation>(5); // always want 5 packages
                var maxSpeed = 0f;
                for (int rowNum = 0; rowNum < numRows - 1; rowNum++)
                {
                    var row = query[rowNum];
                    var nextRow = query[rowNum + 1];

                    // todo here comes the evaluation code
                    // version one
                    // look at subsequent distances
                    var rowPos = new Vector(row.PositionX, row.PositionY);
                    var nextRowPos = new Vector(nextRow.PositionX, nextRow.PositionY);

                    totalTimeDiff += nextRow.Timestamp - row.Timestamp;
                    totalTimeDiffs++;

                    

                    var diff = rowPos - nextRowPos;
                    
                    var trans = new Translation(row.Timestamp, rowPos, diff);
                    speedQueue.AddFront(trans);

                    // get diff, if larger than .5f, record distance
                    

                    //if (speedQueue.Count() == 5)
                    //{
                        var speedQueuePosDiff = speedQueue.First().Position - speedQueue.Last().Position;
                        var speedQueueDist = speedQueuePosDiff.Magnitude;
                        var speedQueueTimeDiff = (speedQueue.First().Time - speedQueue.Last().Time) / 1000;
                        var speed = speedQueueDist / speedQueueTimeDiff;
                        if (speed < 3)
                        {
                            if (maxSpeed < speed)
                            {
                                maxSpeed = (float) speed;
                            }

                            distanceWalkedSimple += (float)diff.Magnitude;
                            distanceWalkedLocally += diff;

                            distanceQueue.AddFront(trans);
                            if (distanceQueue.First().Time - distanceQueue.Last().Time > .1f)
                            {
                                var distanceWalkedInInterval =
                                    distanceQueue.First().Position - distanceQueue.Last().Position;
                                distanceWalkedInIntervals += (float)distanceWalkedInInterval.Magnitude;
                                distanceQueue.Clear();
                            }

                            minDistOther += (float)row.MinOtherDist;
                            numMinDistOther++;
                    }
                    //}

                    if (distanceWalkedLocally.Magnitude > .2f)
                    {
                        distanceWalkedCorrected += (float)distanceWalkedLocally.Magnitude;
                        distanceWalkedLocally = Vector.Zero;
                    }

                    if (row.Timestamp < min) min = row.Timestamp;
                    if (max < row.Timestamp) max = row.Timestamp;
                }

                Logger.Info($"{selection.PartId};{selection.StudyType};{selection.Game};{min};{max};{(max-min)/60000};{distanceWalkedSimple};{distanceWalkedCorrected};{distanceWalkedInIntervals};{maxSpeed};{minDistOther/numMinDistOther}");

            }

            Logger.Info("Average time diff: " + totalTimeDiff / totalTimeDiffs);
            Logger.Info("done");

            //i = 0;
            //foreach (var row in query)
            //{
            //    Logger.Debug($"Part = {row.PartId}, Study type = {row.StudyType}, Game = {row.Game}, Rotation = {row.Rotation}, Position = {row.PositionX}, {row.PositionY}");
            //    i++;
            //    if (i > 100) break;
            //}




            Console.ReadKey();

        }
    }
}
