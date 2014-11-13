using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Emme;
using XTMF;


namespace Trajce_Thesis
{
    class EmmeTool : IEmmeTool
    {

        [RunParameter("Scenario Number", 0, "Emme Scenario Number")]
        public int ScenarioNumber;

        private const string _ToolName = "TMG2.Assignment.TransitAssignment.V4FBTA";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            var args = string.Join(" ", this.ScenarioNumber);

            var result = "";

            return mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
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
