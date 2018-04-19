using VirtualSpace.Shared;

namespace StudyAnalysis
{
    class Config
    {
        public const string CsvFileFolder =
            @"C:\Users\Max\Desktop\Study Analysis 2";
        public const string CsvStudyInfoFileFolder =
            @"C:\Users\Max\Desktop\Study Analysis 2";
        public const string CsvFileName = "All - Raw Quantitive.csv";
        public const string CsvStudyInfoFileName = "StudyConditionInfo.csv";
        public const string PathSeperator = @"\";
        public const string CsvFilePath = CsvFileFolder + PathSeperator + CsvFileName;
        public const string CsvStudyInfoFilePath = CsvStudyInfoFileFolder + PathSeperator + CsvStudyInfoFileName;
    }
}