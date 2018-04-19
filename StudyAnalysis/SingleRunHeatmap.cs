using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class SingleRunHeatmap
    {
        public static void Run()
        {
            Logger.SetLevel(Logger.Level.Debug);

            string gameName = "Space";
            string[] dataLines = File.ReadAllLines(@"C:\Users\Max\Desktop\Study\single\max2\2017-09-17 18-10-17 - VS User Study - space-single.csv");
            
            var outfilePath = Config.CsvStudyInfoFileFolder + Config.PathSeperator + $"{gameName}Single.csv";
            File.Delete(outfilePath);
            File.AppendAllText(outfilePath, "PX;PY\n");

            var selections = from line in dataLines.Skip(1)
                             let data = line.Split(';')
                             select new
                             {
                                 Timestamp = data[0],
                                 PosX = Helper.ConvertToDouble(data[2]),
                                 PosY = Helper.ConvertToDouble(data[3])
                             };

            Logger.Debug("Starting point generation");

            StringBuilder sb = new StringBuilder();
            foreach (var selection in selections)
            {
                var radius = .3f;
                var circleSubsteps = 16;
                var numCircles = 5;
                var subradiusLength = .3f / 5;

                

                var position = new Vector(selection.PosX, selection.PosY);

                for (int circleNum = 0; circleNum < numCircles; circleNum++)
                {
                    var positionPolygon = Polygon.AsCircle(subradiusLength * circleNum, position, circleSubsteps);
                    foreach (var point in positionPolygon.Points)
                    {
                        sb.AppendLine($"{point.X};{point.Z}".Replace(",", "."));
                    }
                }
            }

            File.AppendAllText(outfilePath, sb.ToString());

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
