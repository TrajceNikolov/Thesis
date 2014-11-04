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
        public static float[, , ,] exhaust_factors = new float[4, 4, 15, 24];
        public static float[, ,] start_factors = new float[3, 69, 24]; //69 = number of different durations for the start emissions.
        public static float[,] soak_factors = new float[60, 24]; //60 = number of soaktime durations
        public static void Calculate_Factors(string Exhaust_file, string Start_file, string Soak_file)
        {            

            using (var reader = new CsvReader(Exhaust_file))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int road_type;
                    int pol_type;
                    int speed;
                    float factor;
                    reader.Get(out road_type, 0);
                    reader.Get(out pol_type, 1);
                    reader.Get(out speed, 2);

                    for (int hour = 3; hour <= 26; hour++) // 26 because we have 3 columns (in file) before factors start
                    {
                        reader.Get(out factor, hour);
                        exhaust_factors[road_type, pol_type, speed, hour - 3] = factor;
                    }
                }
            }

            using (var reader = new CsvReader(Start_file))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int pollutant_type;
                    int duration_of_soak;
                    float factor;
                    reader.Get(out pollutant_type, 0);
                    reader.Get(out duration_of_soak, 1);

                    for (int hour = 2; hour <= columns - 1; hour++) // 25 because we have two columns before factors start.
                    {
                        reader.Get(out factor, hour);
                        start_factors[pollutant_type, duration_of_soak - 1, hour - 2] = factor;
                    }
                }
            }

            using (var reader = new CsvReader(Soak_file))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int soak_duration;
                    float factor;
                    reader.Get(out soak_duration, 0);

                    for (int hour = 1; hour <= 24; hour++) // 24 because we only have one column before factors start in file.
                    {
                        reader.Get(out factor, hour);
                        soak_factors[soak_duration - 1, hour - 1] = factor;
                    }
                }
            }            
        }

        public static float get_exhaust_factor(int road_type, int pollutant, int speed, int hour)
        {
            return exhaust_factors[road_type, pollutant, speed, hour];                        
        }

        public static float get_start_factor(int pollutant, int duration, int hour)
        {
            return start_factors[pollutant, duration, hour];
        }

        public static float get_soak_factor(int duration, int hour)
        {
            return soak_factors[duration, hour];
        }
    }
}
