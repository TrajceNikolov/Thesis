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

namespace Trajce_Thesis
{
    class Start_Soak_Calculations : IPostHouseholdIteration
    {

        [SubModelInformation(Required = true, Description = "Exhaust File")]
        public FileLocation ExhaustFile;

        [RunParameter("Soak Emissions File", "Soak.txt", "Please enter the name of the Soak Emissisions File")]
        public FileLocation SoakFile;

        [RunParameter("Start Emissions File", "Start.txt", "Please enter the name of your Start Emissions File")]
        public FileLocation StartFile;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {           
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            CalculateIntraTripChainEmissions(household);
            var allocator = household["ResourceAllocator"] as HouseholdResourceAllocator;
            var allocations = allocator.Resolution;
            int previousAvailable = household.Vehicles.Length;
            foreach(var window in allocator.VehicleAvailability)
            {
                // if a car was returned
                if(previousAvailable < window.AvailableCars)
                {

                }
                // if a car was taken out
                else if(previousAvailable > window.AvailableCars)
                {

                }
                previousAvailable = window.AvailableCars;
            }
        }

        private void CalculateIntraTripChainEmissions(ITashaHousehold household)
        {
            float[] results = null;
            foreach(var person in household.Persons)
            {
                for(int i = 0; i < person.TripChains.Count; i++)
                {
                    if(person.TripChains[i].TripChainRequiresPV)
                    {
                        //Start_Calculation_IntraTripChain(person.TripChains[i]);
                        results = StartCalculationIntraTripChain(
                            person.TripChains.Where(tc => tc.TripChainRequiresPV)
                            .SelectMany(tc => tc.Trips.Where(t => !t.Mode.NonPersonalVehicle)).OrderBy(t => t.ActivityStartTime).ToList(),
                            ref results);
                    }
                }
            }
        }

        public float[] StartCalculationIntraTripChain(List<ITrip> vehicleTrips, ref float[] results)
        {
            if(vehicleTrips.Count <= 0)
            {
                throw new ArgumentException("vehicleTrips does not contain any elements!");
            }
            results = results == null ? new float[3] : results;
            var previous_trip = vehicleTrips[0];
            for (int i = 1; i < vehicleTrips.Count; i++)
            {
                var currentTrip = vehicleTrips[i];
                int soakDurationMinutes = (int)((currentTrip.TripStartTime - previous_trip.ActivityStartTime).ToMinutes());
                int durationCategory = GetDurationCategory(soakDurationMinutes);
                int hours = currentTrip.TripStartTime.Hours % 24;
                results[0] += Factors.GetStartFactor(0, durationCategory, (int)hours);
                results[1] += Factors.GetStartFactor(1, durationCategory, (int)hours);
                results[2] += Factors.GetStartFactor(2, durationCategory, (int)hours);
                previous_trip = currentTrip;
            }
            return results;
        }

        private static int GetDurationCategory(int soakDurationMinutes)
        {
            int duration_category;
            if(soakDurationMinutes < 30)
            {
                duration_category = soakDurationMinutes;
            }
            else if(soakDurationMinutes < 60)
            {
                duration_category = soakDurationMinutes / 2 + 15;
            }
            else if(soakDurationMinutes < 720)
            {
                duration_category = soakDurationMinutes / 30 + 43;
            }
            else
            {
                duration_category = 67;
            }
            return duration_category;
        }

        public float SoakCalculationIntraTripChain(List<ITrip> vehicleTrips) // Only needs one pollutant because HC is only one emitted at soak.
        {
            float soakEmissions = 0;
            for (int i = 1; i < vehicleTrips.Count; i++)
            {
                var previousTrip = vehicleTrips[i - 1];
                var currentTrip = vehicleTrips[i];
                int soakDuration = Math.Min(60, (int)((currentTrip.TripStartTime - previousTrip.ActivityStartTime).ToMinutes()));
                soakEmissions += Factors.GetSoakFactor(soakDuration, (int)(currentTrip.TripStartTime.Hours % 24));
            }
            return soakEmissions;
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {            
        }

        public void IterationFinished(int iteration, int totalIterations)
        {         
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            Factors.CalculateFactors(ExhaustFile, StartFile, SoakFile);
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
