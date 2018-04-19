using System;
using System.Globalization;

namespace StudyAnalysis
{
    class Helper
    {
        public static double ConvertToDouble(string numberString)
        {
            if (double.TryParse(numberString.Replace(',', '.'), out double result))
            {
                return result;
            }

            return double.NaN;
        }

        public static int ConvertToInteger(string numberString)
        {
            return Int32.Parse(numberString);
        }

        public static long ConvertToLong(string numberString)
        {
            return Int64.Parse(numberString);
        }
    }
}