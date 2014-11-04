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

namespace Trajce_Thesis
{
    class Start_Soak_Calculations : IPostHouseholdIteration
    {

        [RunParameter("Exhaust File", "Exhaust.txt", "Please enter the name of the Exhaust File")]
        public string Exhaust_file;

        [RunParameter("Soak Emissions File", "Soak.txt", "Please enter the name of the Soak Emissisions File")]
        public string Soak_file;

        [RunParameter("Start Emissions File", "Start.txt", "Please enter the name of your Start Emissions File")]
        public string Start_file;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {           
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {                   
            foreach(var person in household.Persons)
            {                                
                for(int i = 0; i < person.TripChains.Count; i++)
                {
                    if (person.TripChains[i].TripChainRequiresPV)
                    {
                        List<ITrip> vehicle_trips = new List<ITrip>();
                        //Start_Calculation_IntraTripChain(person.TripChains[i]);
                        for (int j = 0; j < person.TripChains[i].Trips.Count; j++)
                        {
                            if (!person.TripChains[i].Trips[j].Mode.NonPersonalVehicle)
                            {
                                vehicle_trips.Add(person.TripChains[i].Trips[j]);
                            }                            
                        }
                        Start_Calculation_IntraTripChain(vehicle_trips);
                    }
                }
            }
        }

        public float[] Start_Calculation_IntraTripChain(List<ITrip> vehicle_trips)
        {
            float[] results = new float[2];
            int soak_duration_minutes;
            int duration_category;
            for (int i = 1; i < vehicle_trips.Count; i++)
            {
                var previous_trip = vehicle_trips[i - 1];
                var current_trip = vehicle_trips[i];
                
                soak_duration_minutes = (int)((previous_trip.ActivityStartTime - current_trip.TripStartTime).ToMinutes());

                if (soak_duration_minutes < 30)
                {
                    duration_category = soak_duration_minutes;
                }

                else if(soak_duration_minutes < 60)
                {
                    duration_category = (int)(soak_duration_minutes / 2) + 15;
                }
                else if(soak_duration_minutes < 720)
                {
                    duration_category = (int)(soak_duration_minutes / 30) + 43;
                }
                else
                {
                    duration_category = 67;
                }

                results[0] += Factors.get_start_factor(0, duration_category, (int)(current_trip.TripStartTime.Hours)); // is the hour right?
                results[1] += Factors.get_start_factor(1, duration_category, (int)(current_trip.TripStartTime.Hours));
                results[2] += Factors.get_start_factor(2, duration_category, (int)(current_trip.TripStartTime.Hours));
            }
            return results;
        }   
     
        public float Soak_Calculation_IntraTripChain(List<ITrip> vehicle_trips) // Only needs one pollutant because HC is only one emitted at soak.
        {
            float soak_emissions = 0;
            int soak_duration = 0;
            for (int i = 1; i < vehicle_trips.Count; i++)
            {
                var previous_trip = vehicle_trips[i - 1];
                var current_trip = vehicle_trips[i];
                soak_duration = (int)((previous_trip.ActivityStartTime - current_trip.TripStartTime).ToMinutes());

                if(soak_duration > 60)
                {
                    soak_duration = 60;                 
                }

                soak_emissions += Factors.get_soak_factor(soak_duration, (int)(current_trip.TripStartTime.Hours));
            }
            return soak_emissions;
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {            
        }

        public void IterationFinished(int iteration, int totalIterations)
        {         
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            Factors.Calculate_Factors(this.Exhaust_file, this.Start_file, this.Soak_file);            
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
            get { throw new NotImplementedException(); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
