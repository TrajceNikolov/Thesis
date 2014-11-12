using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using XTMF;
using Datastructure;
using Tasha.Common;
using Tasha;

namespace Trajce_Thesis
{
    public class Factors
    {
        private static float[, , ,] ExhaustFactors = new float[4, 4, 15, 24];
        private static float[, ,] StartFactors = new float[3, 69, 24]; //69 = number of different durations for the start emissions.
        private static float[,] SoakFactors = new float[60, 24]; //60 = number of soaktime durations
        public static void CalculateFactors(string exhaustFile, string startFile, string soakFile)
        {            

            using (var reader = new CsvReader(exhaustFile))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int roadType;
                    int polutionType;
                    int speed;
                    float factor;
                    reader.Get(out roadType, 0);
                    reader.Get(out polutionType, 1);
                    reader.Get(out speed, 2);

                    // 26 because we have 3 columns (in file) before factors start
                    for(int hour = 3; hour <= 26; hour++) 
                    {
                        reader.Get(out factor, hour);
                        ExhaustFactors[roadType, polutionType, speed, hour - 3] = factor;
                    }
                }
            }

            using (var reader = new CsvReader(startFile))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int pollutantType;
                    int durationOfSoak;
                    float factor;
                    reader.Get(out pollutantType, 0);
                    reader.Get(out durationOfSoak, 1);
                    // 25 because we have two explanatory columns in the file before factors start.
                    for(int hour = 2; hour < columns; hour++) 
                    {
                        reader.Get(out factor, hour);
                        StartFactors[pollutantType, durationOfSoak - 1, hour - 2] = factor;
                    }
                }
            }

            using (var reader = new CsvReader(soakFile))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int soakDuration;
                    float factor;
                    reader.Get(out soakDuration, 0);
                    // 24 because we only have one column before factors start in file.
                    for(int hour = 1; hour <= 24; hour++) 
                    {
                        reader.Get(out factor, hour);
                        SoakFactors[soakDuration - 1, hour - 1] = factor;
                    }
                }
            }            
        }

        public static float GetExhaustFactor(int roadType, int pollutant, int speed, int hour)
        {
            return ExhaustFactors[roadType, pollutant, speed, hour];                        
        }

        public static float GetStartFactor(int pollutant, int duration, int hour)
        {
            int hourInFile = (hour % 24) - 4; // First get the hours down to the 24 available and then subtract 4 because we set 0 as 4am.
            if (hourInFile < 0) { hourInFile += 24; } // for the 25-28 hours in the morning, we need to add a 24 as they are represented by 20-23 in the file
            return StartFactors[pollutant, duration, hourInFile];
        }

        public static float GetSoakFactor(int duration, int hour)
        {

            int hourInFile = (hour % 24) - 4; // First get the hours down to the 24 available and then subtract 4 because we set 0 as 4am.
            if (hourInFile < 0) { hourInFile += 24; } // for the 25-28 hours in the morning, we need to add a 24 as they are represented by 20-23 in the file
            return SoakFactors[duration, hourInFile];
        }
    }
}
