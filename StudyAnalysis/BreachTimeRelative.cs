using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class BreachTimeRelative
    {
        public static void Run()
        {

            string[] allInfoLines = File.ReadAllLines(Config.CsvStudyInfoFilePath);

            var infoSelections = (from line in allInfoLines.Skip(1)
                                  let data = line.Split(';')
                                  select new
                                  {
                                      PartId = data[0],
                                      StartOffset = Helper.ConvertToLong(data[2]),
                                      StudyType = data[1]
                                  }).Distinct();

            string[] allLines = File.ReadAllLines(Config.CsvFilePath);

            // select
            var selections = (from line in allLines.Skip(1)
                              let data = line.Split(';')
                              let studyType = data[2]
                              where studyType == "Exp"
                              select new
                              {
                                  PartId = data[1],
                                  Game = data[5],
                                  StudyType = studyType
                              }).Distinct();

            // for each selection
            if (selections.Count() != 16)
            {
                Logger.Warn("Expecting 16 different conditions for participants");
            }


            foreach (var selection in selections)
            {
                var query = (from line in allLines.Skip(1)
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

                var numRows = query.Count();
                var breachMs = 0L;
                var numBreaches = 0;
                var breachLastTime = false;
                for (int i = 0; i < numRows - 1; i++)
                {
                    var row = query[i];
                    var nextRow = query[i + 1];

                    //Logger.Debug($"{row}");
                    

                    if (row.MinDistToPoly <= .25f) {
                        breachMs += nextRow.Timestamp - row.Timestamp;

                        if (!breachLastTime)
                        {
                            numBreaches++;
                        }

                        breachLastTime = true;
                    } else
                    {
                        if (row.MinDistToPoly >= .3f)
                            breachLastTime = false;
                    }

                    if (row.Timestamp < min) min = row.Timestamp;
                    if (max < row.Timestamp) max = row.Timestamp;
                }

                Logger.Info($"{selection.PartId};{selection.StudyType};{selection.Game};{min};{max};{breachMs};{breachMs/(max-min)};{numBreaches}");

            }


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
