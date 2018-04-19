using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class ParticipantAndCondition
    {
        public static void Run()
        {
            Logger.SetLevel(Logger.Level.Debug);

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
                select new
                {
                    PartId = data[1],
                    Game = data[5],
                    StudyType = data[2]
                }).Distinct();

            // for each selection
            if (selections.Count() != 32)
            {
                Logger.Warn("Expecting 32 different conditions for participants");
            }


            foreach (var selection in selections)
            {
                var query = from line in allLines.Skip(1)
                    let data = line.Split(';')
                    where data[5] == selection.Game && data[1] == selection.PartId && data[2] == selection.StudyType
                          &&
                          infoSelections
                              .First(infoSelection => infoSelection.PartId == selection.PartId &&
                                                      infoSelection.StudyType == selection.StudyType).StartOffset < Helper.ConvertToLong(data[9])
                    select new
                    {
                        PartId = Helper.ConvertToInteger(data[1]),
                        Timestamp = Helper.ConvertToLong(data[9]),
                        Game = data[5],
                        StudyType = data[2],
                        Rotation = Helper.ConvertToDouble(data[8]),
                        PositionX = Helper.ConvertToDouble(data[11]),
                        PositionY = Helper.ConvertToDouble(data[12])
                    };

                //var minOffsetTime =
                //    infoSelections.First(select => select.PartId == selection.PartId && select.StudyType == selection.StudyType).StartOffset;

                // BASIC STEPS FOR FIRST 100 ENTRIES
                //i = 0;
                //foreach (var row in query)
                //{
                //    Logger.Debug($"Part = {row.PartId}, Study type = {row.StudyType}, Game = {row.Game}, Rotation = {row.Rotation}, Position = {row.PositionX}, {row.PositionY}");
                //    i++;
                //    if (i > 100) break;
                //}

                // USED AREA
                var radius = .3f;
                var circleSubsteps = 32;
                var usedPlayArea = new PolygonList();
                foreach (var row in query)
                {
                    //if (row.Timestamp < minOffsetTime) continue;

                    var position = new Vector(row.PositionX, row.PositionY);
                    position.RotateCounter(-row.Rotation / 180 * Math.PI);
                    var positionPolygon = Polygon.AsCircle(radius, position, circleSubsteps);
                    usedPlayArea = ClipperUtility.Union(usedPlayArea, positionPolygon);
                }

                var usedArea = ClipperUtility.GetArea(usedPlayArea);
                Logger.Debug($"{selection.PartId};{selection.Game};{selection.StudyType};{usedArea}");

                // DURATION, MIN/MAX TIME
                //var min = double.MaxValue;
                //var max = double.MinValue;
                //foreach (var row in query)
                //{
                //    if (row.Timestamp < minOffsetTime) continue;

                //    if (row.Timestamp < min) min = row.Timestamp;
                //    if (max < row.Timestamp) max = row.Timestamp;
                //}

                //Logger.Debug($"{selection.PartId};{selection.StudyType};{min};{max};0;{max-min}");

            }



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
