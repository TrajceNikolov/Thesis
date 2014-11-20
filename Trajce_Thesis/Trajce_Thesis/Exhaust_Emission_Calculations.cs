using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using XTMF;
using Datastructure;
using Trajce_Thesis;
using Tasha.Common;
using Tasha;
using TMG.Input;
using Tasha.XTMFModeChoice;
using TMG.Emme;



namespace Trajce_Thesis
{
    public class Exhaust_Emission_Calculations : IEmmeTool
    {

        [SubModelInformation(Required = true, Description = "Output of the Exhaust Calculations")]
        public FileLocation ExhaustOutputFile;

        [SubModelInformation(Required = true, Description = "Input: Exhaust Emission File")]
        public FileLocation ExhaustInputFile;

        [SubModelInformation(Required = true, Description = "Intermediate Network Data File")]
        public FileLocation NetworkDataFile;

        [RunParameter("AM or PM", "AM", "Which peak period are we operating in? AM or PM")]
        public string peakPeriod;

        [RunParameter("Scenario Number", 11, "Which scenario number would you like to run the exhaust calculations on")]
        public int scenarioNumber;

        private const string _ToolName = "TMG2.Assignment.TransitAssignment.V4FBTA";

        float[] exhaustEmissions = new float[3];

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            var args = string.Join(" ", scenarioNumber);

            var result = "";

            return mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
        }

        public void ExhaustCalculation()
        {
            Factors.CalculateExhaustFactors(ExhaustInputFile);

            using (var reader = new CsvReader(NetworkDataFile))
            {
                reader.LoadLine();
                int columns;
                while (reader.LoadLine(out columns))
                {
                    int link_type;
                    float link_length;
                    float auto_time;
                    reader.Get(out link_type, 4);
                    reader.Get(out link_length, 2);
                    reader.Get(out auto_time, 3);

                    for (int pollutant = 0; pollutant < 4; pollutant++)
                    {
                        exhaustEmissions[pollutant] += Factors.GetExhaustFactor(GetLinkType(link_type), pollutant,
                            CalculateSpeedCategory(link_length, auto_time), CalculateHour(this.peakPeriod));
                    }
                }
            }
        }

        public int GetLinkType(int vdf_code)
        {
            if (vdf_code < 19) { return 0; } //highway
            else if (vdf_code < 50) { return 1; } //arterials
            else if (vdf_code < 90) { return 2; } // locals
            else { return 2; } //centroid connectors are added as local streets            
        }

        public int CalculateSpeedCategory(float link_length, float auto_time)
        {
            int category;
            float actual_speed_mph = (link_length / (auto_time / 60)) * 0.62137f;

            if (actual_speed_mph <= 10)
            {
                category = (int)(Math.Round((actual_speed_mph / 2.5)) - 1);
            }
            else if (actual_speed_mph <= 65)
            {
                category = (int)((Math.Round(actual_speed_mph / 5) + 1));
            }
            else
            {
                category = 14;
            }

            return category;
        }

        public int CalculateHour(string peakPeriod)
        {
            if (peakPeriod != "AM" && peakPeriod != "PM")
            {
                throw new XTMFRuntimeException("Please select a peak period of the format AM or PM in capital letters");
            }

            if (peakPeriod == "AM")
            {
                return 8; // Chose the 8am hour for calculations as it provides the best average within AM peak.
            }
            else
            {
                return 17; // Choose the 5pm hour for calculations as it provides the best average within PM peak.
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(100, 100, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
