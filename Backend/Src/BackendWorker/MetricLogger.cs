using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class MetricLogger
    {
        private PlayerData _playerData;
        private EventMap _eventMap;

        public MetricLogger(PlayerData playerData, EventMap eventMap)
        {
            _playerData = playerData;
            _eventMap = eventMap;
            _sb = new StringBuilder();
        }

        private const string FileDescription = "VS User Study";
        private const string TableFileEnding = ".csv";
        private const string LogFileEnding = ".log";
        private const string Seperator = ";";

        private string _currentTableFileName;
        private string _currentLogFileName;
        private object _logLock = new object();

        private const int MaxPolyPoints = 10;
        private const int MaxPlayers = 4;

        public void StartNewLogging(string fileNameAddition = "")
        {
            string dateString = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            _currentTableFileName = dateString + " - " + FileDescription + " - " + fileNameAddition + TableFileEnding;
            lock(_logLock)
                _currentLogFileName = dateString + " - " + FileDescription + " - " + fileNameAddition + LogFileEnding;
            // create header
            CreateHeader();
        }

        private void PrepareAppend()
        {
            _sb.Clear();
        }

        private void FinalizeAppend()
        {
            File.AppendAllText(_currentTableFileName, _sb.ToString());
            _sb.Clear();
        }

        public bool created = false;
        private void CreateHeader()
        {
            created = false;

            PrepareAppend();

            AppendSingle($"Timestamp");
            
            AppendSingle($"UserNum");
            AppendSingle($"PosX");
            AppendSingle($"PosY");
            for (int nodeNum = 0; nodeNum < MaxPolyPoints; nodeNum++)
            {
                AppendSingle($"PolyPos{nodeNum}X");
                AppendSingle($"PolyPos{nodeNum}Y");
            }
            
            AppendSingle($"Breach");
            AppendSingle($"MinOtherDist");
            AppendSingle($"MinDistToPoly");
            AppendSingle($"MaxDistToPoly", false, true);
            
            FinalizeAppend();

            created = true;
        }

        public void EndLogging()
        {
            _currentTableFileName = "";
            lock (_logLock)
                _currentLogFileName = "";
        }

        private StringBuilder _sb;

        public void AppendSingle(string value = "", bool seperator = true, bool newLine = false)
        {
            _sb.Append(value);
            if (seperator) _sb.Append(Seperator);
            if (newLine) _sb.AppendLine();
        }

        TimeSpan _nowSinceEpoch => DateTime.UtcNow - new DateTime(1970, 1, 1);
        readonly DateTime epoch = new DateTime(1970, 1, 1);

        public void Log(string message)
        {
            lock (_logLock)
            {
                if (_currentLogFileName == "") return;

                var nowSinceEpoch = _nowSinceEpoch;
                var secondsSinceEpoch = (long) nowSinceEpoch.TotalSeconds;
                int trailingMillis = (int) (nowSinceEpoch.TotalMilliseconds % 1000);
                var timestamp = secondsSinceEpoch + "," + trailingMillis;
                File.AppendAllText(_currentLogFileName, timestamp + ":" + message + Environment.NewLine);
            }
        }

        public void LogMetrics()
        {
            if (_currentTableFileName == "") return;

            while (!created) Thread.Sleep(2);
            
            var nowSinceEpoch = _nowSinceEpoch;
            var timeString = "" + (long)nowSinceEpoch.TotalMilliseconds;

            var playerIds = _playerData.GetKeys();

            var nowTurn = Time.CurrentTurn;
            var nowEvents = _eventMap.GetEventsForTurn(nowTurn);

            Polygon[] nowPolygons = new Polygon[playerIds.Count];
            foreach (TimedEvent nowEvent in nowEvents)
            {
                TimedArea nowArea = null;
                if (nowEvent.EventType == EventType.Area)
                {
                    nowArea = nowEvent as TimedArea;
                }
                else if (nowEvent.EventType == EventType.Transition)
                {
                    Transition nowTransition = (Transition)nowEvent;
                    TransitionFrame nowFrame =
                        nowTransition.Frames.Find(frame => frame.Area != null && frame.Area.IsActiveAt(nowTurn));

                    if (nowFrame == null) continue;
                    nowArea = nowFrame.Area;
                }

                if (nowArea == null) continue;
                var playerIndex = playerIds.IndexOf(nowEvent.PlayerId);
                if (playerIndex < 0) continue;
                nowPolygons[playerIndex] = nowArea.Area;
            }

            for (int playerIndex = 0; playerIndex < playerIds.Count; playerIndex++)
            {
                if (nowPolygons[playerIndex] == null)
                    nowPolygons[playerIndex] = new Polygon();
            }
            
            Vector[] nowPositions = new Vector[playerIds.Count];
            // append empty when player is not logged in
            for (int playerNum = 0; playerNum < playerIds.Count; playerNum++)
            {
                var playerId = playerIds[playerNum];

                if (_playerData.TryGetEntry(playerId, out PlayerDataEntry player))
                {
                    var position = player.Position;
                    nowPositions[playerNum] = position;
                }
            }

            UpdateBreaches(playerIds, nowPositions, nowPolygons,
                out float[] minimalDistanceToOther, out bool[] isBreaching, out double[] minDistPerUser, out double[] maxDistPerUser);

            // appending to file
            PrepareAppend();
            
            for (int userNum = 0; userNum < playerIds.Count; userNum++)
            {
                AppendSingle(timeString);

                var playerId = playerIds[userNum];

                AppendSingle($"{playerId}");

                var position = nowPositions[userNum];

                if (position == null)
                {
                    AppendSingle();
                    AppendSingle();
                }
                else
                {
                    AppendSingle(position.X.ToString("0.000"));
                    AppendSingle(position.Z.ToString("0.000"));
                }

                var playerPolygon = nowPolygons[userNum];
                var playerPoints = playerPolygon.Points;
                for (int nodeNum = 0; nodeNum < MaxPolyPoints; nodeNum++)
                {
                    if (nodeNum < playerPoints.Count && playerPoints[nodeNum] != null)
                    {
                        var nodePosition = playerPoints[nodeNum];
                        AppendSingle(nodePosition.X.ToString("0.000"));
                        AppendSingle(nodePosition.Z.ToString("0.000"));
                    }
                    else
                    {
                        AppendSingle();
                        AppendSingle();
                    }
                }

                AppendSingle(isBreaching[userNum] ? "1" : "0");
                AppendSingle(minimalDistanceToOther[userNum] > 10 ? "" : minimalDistanceToOther[userNum].ToString("0.000"));

                AppendSingle(minDistPerUser[userNum] > 10 ? "" : minDistPerUser[userNum].ToString("0.000"));
                AppendSingle(maxDistPerUser[userNum] > 10 ? "" : maxDistPerUser[userNum].ToString("0.000"), userNum == MaxPlayers - 1 ? false : true);

                AppendSingle(seperator: false, newLine: true);
            }

            FinalizeAppend();
        }

        private float areaSafety = .3f;
        private float breachToNoBreachDelta = .07f;
        private bool[] _wasBreachingLastRound;
        private void UpdateBreaches(List<int> userIds, Vector[] nowPositions, Polygon[] nowAreas, 
            out float[] minimalDistanceToOther, out bool[] isBreaching, out double[] minDistPerUser, out double[] maxDistPerUser)
        {
            var numUsers = userIds.Count;

            minimalDistanceToOther = new float[numUsers];
            isBreaching = new bool[numUsers];
            minDistPerUser = new double[numUsers];
            maxDistPerUser = new double[numUsers];

            for (int userNum = 0; userNum < numUsers; userNum++)
            {
                float minimalDistanceToOtherPlayer = float.MaxValue;
                if (nowPositions[userNum] == null) continue;

                for (int otherNum = 0; otherNum < numUsers; otherNum++)
                {
                    if (userNum == otherNum) continue;
                    if (nowPositions[otherNum] == null) continue;

                    var distUsers = nowPositions[userNum].Distance(nowPositions[otherNum]); 
                    if (distUsers < minimalDistanceToOtherPlayer)
                    {
                        minimalDistanceToOtherPlayer = distUsers;
                    }
                }

                minimalDistanceToOther[userNum] = minimalDistanceToOtherPlayer;

                if (nowPositions[userNum] == null)
                {
                    isBreaching[userNum] = false;
                    continue;
                }

                //var userDot = Polygon.AsCircle(.05f, nowPositions[userNum], 8);

                //MinDistancePointToPolygon(nowAreas[userNum], )
                // get min distance and max distance to polygon

                GeometryHelper.DistancePointToPolygonInside(nowAreas[userNum], nowPositions[userNum], out double minDist, out double maxDist);
                minDistPerUser[userNum] = minDist;
                maxDistPerUser[userNum] = maxDist;

                var noEarlierBreachForUser = _wasBreachingLastRound != null && userNum < _wasBreachingLastRound.Length && _wasBreachingLastRound[userNum];
                if (noEarlierBreachForUser)
                {
                    //var safeArea = ClipperUtility.OffsetPolygonForSafety(nowAreas[userNum], -areaSafety);
                    
                    //isBreaching[userNum] = !ClipperUtility.ContainsWithinEpsilon(safeArea, userDot);
                    isBreaching[userNum] = minDist < areaSafety;
                }
                else
                {
                    isBreaching[userNum] = minDist < areaSafety + breachToNoBreachDelta;
                    //var safeArea = ClipperUtility.OffsetPolygonForSafety(nowAreas[userNum], -areaSafety - breachToNoBreachDelta);

                    //isBreaching[userNum] = !ClipperUtility.ContainsWithinEpsilon(safeArea, userDot);
                }

                //if (userNum == 2)
                //{
                //    Logger.Debug($"min: {minDist}, max: {maxDist}");
                //}

                //if (isBreaching[userNum])
                //{
                //    Logger.Debug($"player {userNum} is breaching");
                //    Logger.Debug($"position {nowPositions[userNum]}");

                //    Logger.Debug($"min dist {minimalDistanceToOther[userNum]}");
                //}
            }

            _wasBreachingLastRound = (bool[])isBreaching.Clone();
        }
    }
}