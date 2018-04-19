using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class AllGamesAndConditionHeatmap
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

            string gameId = "B"; 

            // select
            var selections = (from line in allLines.Skip(1)
                            let data = line.Split(';')
                            let game = data[5]
                            where 
                                game.Equals(gameId)
                            select new
                            {
                                Game = game,
                                StudyType = data[2]
                            }).Distinct();
            
            // for each selection
            if (selections.Count() != 8)
            {
                Logger.Warn("Expecting 32 different conditions for participants");
            }

            var outfilePath = Config.CsvStudyInfoFileFolder + Config.PathSeperator + $"testOffsetted{gameId}.csv";
            File.Delete(outfilePath);
            File.AppendAllText(outfilePath, "StudyId;Game;StudyType;PX;PY;genPX;genPY\n");
            foreach (var selection in selections)
            {


                // should probably use time interpolation
                //var query = (from line in allLines.Skip(1)
                //    let data = line.Split(';')
                //    let timestamp = Helper.ConvertToLong(data[9])
                //    let timestampMinOffset = infoSelections
                //        .First(infoSelection => infoSelection.StudyType == selection.StudyType).StartOffset
                //    where
                //    data[5] == selection.Game && data[2] == selection.StudyType && timestampMinOffset < timestamp
                //    select new
                //    {
                //        PartId = Helper.ConvertToInteger(data[1]),
                //        //Timestamp = timestamp,
                //        Game = data[5],
                //        StudyType = data[2],
                //        Rotation = Helper.ConvertToDouble(data[8]),
                //        //PositionX = Helper.ConvertToDouble(data[11]),
                //        //PositionY = Helper.ConvertToDouble(data[12])
                //    }).Distinct();

                //foreach (var rotationInfo in query)
                //{
                //    Logger.Debug($"{rotationInfo}");
                //}

                // BUILD POINT HEATMAPS
                var query = from line in allLines.Skip(1)
                            let data = line.Split(';')
                            let timestamp = Helper.ConvertToLong(data[9])
                            let timestampMinOffset = infoSelections
                                .First(infoSelection => infoSelection.StudyType == selection.StudyType).StartOffset
                            where
                            data[5] == selection.Game && data[2] == selection.StudyType && timestampMinOffset < timestamp
                            select new
                            {
                                PartId = Helper.ConvertToInteger(data[1]),
                                Timestamp = timestamp,
                                Game = data[5],
                                StudyType = data[2],
                                StudyId = Helper.ConvertToInteger(data[0]),
                                Rotation = Helper.ConvertToDouble(data[8]),
                                PositionX = Helper.ConvertToDouble(data[11]),
                                PositionY = Helper.ConvertToDouble(data[12])
                            };
                var radius = .3f;
                var circleSubsteps = 16;
                var numCircles = 5;
                var subradiusLength = .3f / 5;

                StringBuilder sb = new StringBuilder();
                foreach (var row in query)
                {
                    //if (row.Timestamp < minOffsetTime) continue;

                    var position = new Vector(row.PositionX, row.PositionY);
                    var degrees = -row.Rotation;
                    if (row.Game == "W" && row.StudyId > 2)
                    {
                        degrees += 90;
                    } else if (row.Game == "S" && row.StudyId < 3)
                    {
                        degrees -= 90;
                    } else if (row.Game == "B" && row.StudyId < 2)
                    {
                        //degrees -= 90;
                    }
                    position = position.RotateCounter(degrees / 180 * Math.PI);

                    if (row.StudyType == "Ctrl")
                    {
                        position.X += 4;
                    }

                    for (int circleNum = 0; circleNum < numCircles; circleNum++)
                    {
                        var positionPolygon = Polygon.AsCircle(subradiusLength * circleNum, position, circleSubsteps);
                        foreach (var point in positionPolygon.Points)
                        {
                            sb.AppendLine($"{row.StudyId};{row.Game};{row.StudyType};{point.X};{point.Z};{position.X};{position.Z}".Replace(",", "."));
                        }
                    }
                }
                File.AppendAllText(outfilePath, sb.ToString());

                Logger.Debug($"{selection.Game} and {selection.StudyType} completed point generation");
            }

            Logger.Debug("Done");


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
