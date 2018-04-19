
using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.SetLevel(Logger.Level.Info);
            Logger.AddPrinter(new ConsolePrinter());

            // data roughly 70ms apart on avg
            // info
            // (0)PartId; (1)Condition; (2)StartOffset
            // data
            // (0)StudyId; (1)PartId; (2)StudyType; 
            // (3)Gender; (4)Age; (5)Game; (6)Previous Experience; 
            // (7)Realwalking; (8)Rotation; (9)Timestamp; 
            // (10)UserNum; (11)PosX; (12)PosY; 
            // (13)PolyPos0X; (14)PolyPos0Y; (15)PolyPos1X; (16)PolyPos1Y; 
            // (17)PolyPos2X; (18)PolyPos2Y; (19)PolyPos3X; (20)PolyPos3Y; 
            // (21)PolyPos4X; (22)PolyPos4Y; (23)PolyPos5X; (24)PolyPos5Y;
            // (25)PolyPos6X; (26)PolyPos6Y; (27)PolyPos7X; (28)PolyPos7Y; 
            // (29)PolyPos8X; (30)PolyPos8Y; (31)PolyPos9X; (32)PolyPos9Y; 
            // (33)MinOtherDist; (34)MinDistToPoly; (35)MaxDistToPoly; (36)Breach

            var distanceWalkedAnalysis =
                new AvgPlayerDistance(Config.CsvStudyInfoFilePath, Config.CsvFilePath);

            distanceWalkedAnalysis.Run();
        }
    }
}
