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
    class AvgPlayerDistance
    {
        private string _infoFilePath;
        private string _dataFilePath;
        private bool _skipHeader;
        
        public AvgPlayerDistance(string infoFilePath, string dataFilePath, bool skipHeader = true)
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
                                  StudyType = studyType,
                                  StudyId = Helper.ConvertToInteger(data[0])
                              }).Distinct().ToList();
            // need the study ID :o

            Logger.Info("PartId;StudyType;Game;AvgMinDistOthers;Other0;Other1;Other2;MinOther");
            
            foreach (var selection in selections)
            {
                var query = (from line in _skipHeader ? dataLines.Skip(1) : dataLines
                            let data = line.Split(';')
                            let timestamp = Helper.ConvertToLong(data[9])
                            where Helper.ConvertToInteger(data[0]) == selection.StudyId
                                  &&
                                  infoSelections
                                      .First(infoSelection => infoSelection.PartId == selection.PartId &&
                                                              infoSelection.StudyType == selection.StudyType).StartOffset < timestamp
                            select new
                            {
                                PartId = Helper.ConvertToInteger(data[1]),
                                StudyId = Helper.ConvertToInteger(data[0]),
                                Timestamp = timestamp,
                                Game = data[5],
                                StudyType = data[2],
                                Rotation = Helper.ConvertToDouble(data[8]),
                                PositionX = Helper.ConvertToDouble(data[11]),
                                PositionY = Helper.ConvertToDouble(data[12]),
                                MinOtherDist = Helper.ConvertToDouble(data[33]),
                                MinDistToPoly = Helper.ConvertToDouble(data[34])
                            }).ToList();

                var onlyUserQuery = query.Where(row => row.PartId == Helper.ConvertToInteger(selection.PartId)).ToList();

                var numRows = onlyUserQuery.Count;

                var minDistOther = 0f;
                var numDistOther = 0;

                var minDistTest = 0f;
                
                var distToOther = new Dictionary<int, float>();

                for (int rowNum = 0; rowNum < numRows; rowNum++)
                {
                    var row = onlyUserQuery[rowNum];

                    minDistOther += (float)row.MinOtherDist;
                    numDistOther++;

                    var rowPos = new Vector(row.PositionX, row.PositionY);
                    
                    var otherOffsets = Enumerable.Range(-4, 9).ToList();

                    var localMinDist = float.MaxValue;
                    foreach (var otherOffset in otherOffsets)
                    {
                        var otherRowNum = rowNum * 4 + otherOffset;
                        if (otherRowNum < 0 || otherRowNum >= query.Count) continue;

                        var otherRow = query[otherRowNum];
                        if (row.Timestamp != otherRow.Timestamp) continue;
                        if (row.PartId == otherRow.PartId) continue;
                        
                        if (row.PartId == otherRow.PartId)
                            Logger.Warn($"Same part id. pid: {row.PartId}, sid: {row.StudyId}");
                        
                        if (!distToOther.ContainsKey(otherRow.PartId)) distToOther[otherRow.PartId] = 0;

                        var innerRowPos = new Vector(otherRow.PositionX, otherRow.PositionY);

                        var dist = rowPos.Distance(innerRowPos);
                        distToOther[otherRow.PartId] += dist;

                        if (localMinDist > dist) localMinDist = dist;
                    }

                    // todo here comes the evaluation code
                    // version one
                    // look at subsequent distances
                    minDistTest += localMinDist;
                }
                
                var logString =
                    $"{selection.PartId};{selection.StudyType};{selection.Game};{minDistOther / numDistOther}";

                foreach (var pair in distToOther)
                {
                    var avgDistOther = pair.Value / numDistOther;
                    logString += $";{avgDistOther}";
                }

                logString += $";{minDistTest / numDistOther}";

                Logger.Info(logString);

            }

            Logger.Info("done");

            Console.ReadKey();

        }
    }
}
