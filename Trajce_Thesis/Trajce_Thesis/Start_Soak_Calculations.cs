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

        [SubModelInformation(Required = true, Description = "Soak File")]
        public FileLocation SoakFile;

        [SubModelInformation(Required = true, Description = "Start File")]
        public FileLocation StartFile;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {           
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            float[] startResults = null;
            float soakResults = 0;
            var allocator = household["ResourceAllocator"] as HouseholdResourceAllocator;
            CalculateIntraTripChainEmissions(household, ref startResults, ref soakResults);                             
            CalculateInterTripChainEmissions(ref startResults, ref soakResults, allocator);
        }

        private void CalculateInterTripChainEmissions(ref float[] startResults, ref float soakResults, HouseholdResourceAllocator allocator)
        {
            // var allocations = allocator.Resolution;       
            for(int window = 1; window < allocator.VehicleAvailability.Count; window++)
            {
                var previousWindow = allocator.VehicleAvailability[window - 1];
                var currentWindow = allocator.VehicleAvailability[window];               
                // if a car was returned
                if(previousWindow.AvailableCars < currentWindow.AvailableCars)
                {

                }
                // If a car was taken out
                else if(previousWindow.AvailableCars > currentWindow.AvailableCars)
                {
                    int numberOfCars = previousWindow.AvailableCars - currentWindow.AvailableCars;
                    for(int car = 1; car <= numberOfCars; car++)
                    {
                        int idleDurationMinutes = (int)(previousWindow.TimeSpan.Start - previousWindow.TimeSpan.End).ToMinutes();
                        int startHour = (int)(currentWindow.TimeSpan.Start.Hours % 24);

                        StartCalculation(idleDurationMinutes, startHour, ref startResults);
                                                
                        Time soakDurationStart = previousWindow.TimeSpan.Start;
                        Time soakDurationEnd = previousWindow.TimeSpan.End;

                        SoakCalculations(soakDurationStart, soakDurationEnd, ref soakResults)                                                
                    }
                }
            }
        }
        
        private void SoakCalculations(Time soakDurationStart, Time soakDurationEnd, ref float soakResults)
        {
            int soakDuration1;
            int soakDuration2;
            int soakHour1 = soakDurationStart.Hours % 24;
            int soakHour2 = soakDurationEnd.Hours % 24;

            // Same hour for soak duration
            if (soakHour1 == soakHour2)
            {
                soakDuration1 = Math.Min(60, (int)(soakDurationStart - soakDurationEnd).ToMinutes());
                soakDuration2 = 0;

                soakResults += Factors.GetSoakFactor(soakDuration1, soakHour1);
            }

            else
            {
                soakDuration1 = (int)((new Time { Hours = soakHour1 + 1, Minutes = 0, Seconds = 0 }) - soakDurationStart).ToMinutes(); // rest of first hour
                soakHour2 = soakHour1 + 1;
                soakDuration2 = Math.Min((int)(soakDurationEnd - new Time { Hours = soakHour2 }).ToMinutes(), 60 - soakDuration1); // the second duration represents whatever is left over in the second hour, or just 60 - first duration

                soakResults += (Factors.GetSoakFactor(soakDuration1, soakHour1) + Factors.GetSoakFactor(soakDuration2, soakHour2));
            }            
        }

        private void StartCalculation(int idleDurationMinutes, int startHour, ref float[] results)
        {
            results = results == null ? new float[3] : results;
            int durationCategory = GetDurationCategory(idleDurationMinutes);
            results[0] += Factors.GetStartFactor(0, durationCategory, startHour);
            results[1] += Factors.GetStartFactor(1, durationCategory, startHour);
            results[2] += Factors.GetStartFactor(2, durationCategory, startHour);
        }

        private void CalculateIntraTripChainEmissions(ITashaHousehold household, ref float[] startResults, ref float soakResults)
        {            
            foreach(var person in household.Persons)
            {
                for(int i = 0; i < person.TripChains.Count; i++)
                {
                    if(person.TripChains[i].TripChainRequiresPV)
                    {                       
                        StartCalculationIntraTripChain(
                            person.TripChains.Where(tc => tc.TripChainRequiresPV)
                            .SelectMany(tc => tc.Trips.Where(t => !t.Mode.NonPersonalVehicle)).OrderBy(t => t.ActivityStartTime).ToList(),
                            ref startResults);

                        SoakCalculationIntraTripChain(
                            person.TripChains.Where(tc => tc.TripChainRequiresPV)
                            .SelectMany(tc => tc.Trips.Where(t => !t.Mode.NonPersonalVehicle)).OrderBy(t => t.ActivityStartTime).ToList(),
                            ref soakResults);
                    }
                }
            }
        }        

        public void StartCalculationIntraTripChain(List<ITrip> vehicleTrips, ref float[] results)
        {
            if(vehicleTrips.Count <= 0)
            {
                throw new ArgumentException("vehicleTrips does not contain any elements!");
            }
            
            var previous_trip = vehicleTrips[0];
            for (int i = 1; i < vehicleTrips.Count; i++)
            {
                var currentTrip = vehicleTrips[i];
                int idleDurationMinutes = (int)((currentTrip.TripStartTime - previous_trip.ActivityStartTime).ToMinutes());                
                int startHour = currentTrip.TripStartTime.Hours % 24;      
     
                StartCalculation(idleDurationMinutes, startHour, ref results);

                previous_trip = currentTrip;
            }            
        }

        private static int GetDurationCategory(int soakDurationMinutes)
        {
            int durationCategory;
            if(soakDurationMinutes < 30)
            {
                durationCategory = soakDurationMinutes;
            }
            else if(soakDurationMinutes < 60)
            {
                durationCategory = soakDurationMinutes / 2 + 15;
            }
            else if(soakDurationMinutes < 720)
            {
                durationCategory = soakDurationMinutes / 30 + 43;
            }
            else
            {
                durationCategory = 67;
            }
            return durationCategory;
        }

        public void SoakCalculationIntraTripChain(List<ITrip> vehicleTrips, ref float soakResults) // Only needs one pollutant because HC is only one emitted at soak.
        {            
            for (int i = 1; i < vehicleTrips.Count; i++)
            {
                var previousTrip = vehicleTrips[i - 1];
                var currentTrip = vehicleTrips[i];

                Time soakDurationStart = previousTrip.ActivityStartTime;
                Time soakDurationEnd = currentTrip.TripStartTime;

                SoakCalculations(soakDurationStart, soakDurationEnd, ref soakResults);                                
            }            
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
