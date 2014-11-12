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
    public class Start_Soak_Calculations : IPostHouseholdIteration
    {

        [SubModelInformation(Required = true, Description = "Exhaust File")]
        public FileLocation ExhaustFile;

        [SubModelInformation(Required = true, Description = "Soak File")]
        public FileLocation SoakFile;

        [SubModelInformation(Required = true, Description = "Start File")]
        public FileLocation StartFile;

        [SubModelInformation(Required=true, Description= "The output file containing soak emissions")]
        public FileLocation SoakOutputFile;

        [SubModelInformation(Required = true, Description = "The output file containing soak emissions")]
        public FileLocation StartOutputFile;

        float[] startResults = null;
        float soakResults = 0;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {           
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {          
            var allocator = household["ResourceAllocator"] as HouseholdResourceAllocator;
            if (allocator.VehicleAvailability != null)
            {
                CalculateIntraTripChainEmissions(household, ref this.startResults, ref this.soakResults);
                CalculateInterTripChainEmissions(household, ref this.startResults, ref this.soakResults, allocator);
            }
        }

        private void CalculateInterTripChainEmissions(ITashaHousehold household, ref float[] startResults, ref float soakResults, HouseholdResourceAllocator allocator)
        {
            var availability = allocator.VehicleAvailability;
            var lastTimeUsed = household.Vehicles.Select( v => Time.StartOfDay ).ToArray();
            // var allocations = allocator.Resolution;       
            for(int window = 1; window < availability.Count; window++)
            {
                var previousWindow = availability[window - 1];
                var currentWindow = availability[window];
                int deltaCars = previousWindow.AvailableCars - currentWindow.AvailableCars;
                // if a car was returned
                if(previousWindow.AvailableCars < currentWindow.AvailableCars)
                {
                    deltaCars = -deltaCars;
                    for(int i = 0; i < deltaCars; i++)
                    {
                        lastTimeUsed[currentWindow.AvailableCars - i - 1] = currentWindow.TimeSpan.Start;
                    }
                }
                // If a car was taken out
                else if(previousWindow.AvailableCars > currentWindow.AvailableCars)
                {
                    for(int car = 0; car < deltaCars; car++)
                    {
                        int idleDurationMinutes = (int)(currentWindow.TimeSpan.Start - lastTimeUsed[lastTimeUsed.Length - currentWindow.AvailableCars - 1 - car]).ToMinutes();
                        int startHour = (int)(currentWindow.TimeSpan.Start.Hours % 24);
                        StartCalculation(idleDurationMinutes, startHour, ref startResults);
                                                
                        Time soakDurationStart = previousWindow.TimeSpan.Start;
                        Time soakDurationEnd = previousWindow.TimeSpan.End;

                        SoakCalculations(soakDurationStart, soakDurationEnd, ref soakResults);                                                
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
                soakDuration1 = Math.Min(60, (int)(soakDurationEnd - soakDurationStart).ToMinutes());
                soakDuration2 = 0;

                soakResults += Factors.GetSoakFactor(soakDuration1 - 1, soakHour1)*soakDuration1;
            }

            else
            {
                soakDuration1 = (int)Math.Min(60, ((new Time { Hours = soakHour1 + 1, Minutes = 0, Seconds = 0 }) - soakDurationStart).ToMinutes()); // rest of first hour
                if (soakDuration1 < 60)
                {
                    soakHour2 = soakHour1 + 1;
                    soakDuration2 = Math.Min((int)(soakDurationEnd - new Time { Hours = soakHour2 }).ToMinutes(), 59 - soakDuration1); // the second duration represents whatever is left over in the second hour, or just 60 - first duration                    
                    soakResults += (Factors.GetSoakFactor(soakDuration1 - 1, soakHour1) * soakDuration1 + Factors.GetSoakFactor(soakDuration2, soakHour2) * soakDuration2);
                }
                else 
                { 
                    soakDuration2 = 0;
                    soakResults += Factors.GetSoakFactor(soakDuration1 - 1, soakHour1) * soakDuration1;
                }                

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
                        List<ITrip> vehicleTrips = new List<ITrip>();
                        for(int j = 0; i < person.TripChains[i].Trips.Count; j++)
                        {
                            if(!person.TripChains[i].Trips[j].Mode.NonPersonalVehicle)
                            {
                                vehicleTrips.Add(person.TripChains[i].Trips[j]);
                            }
                        }
                        vehicleTrips.OrderBy(t => t.ActivityStartTime);
                        StartCalculationIntraTripChain(vehicleTrips, ref startResults);
                        SoakCalculationIntraTripChain(vehicleTrips, ref soakResults);

                        //StartCalculationIntraTripChain(
                        //    person.TripChains[i].Trips.SelectMany(t => t.Mode!t.Mode.NonPersonalVehicle)
                        //    person.TripChains.Where(tc => tc.TripChainRequiresPV)
                        //    .SelectMany(tc => tc.Trips.Where(t => !t.Mode.NonPersonalVehicle)).OrderBy(t => t.ActivityStartTime).ToList(),
                        //    ref startResults);

                        //SoakCalculationIntraTripChain(
                        //    person.TripChains.Where(tc => tc.TripChainRequiresPV)
                        //    .SelectMany(tc => tc.Trips.Where(t => !t.Mode.NonPersonalVehicle)).OrderBy(t => t.ActivityStartTime).ToList(),
                        //    ref soakResults);
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
            using (StreamWriter writer = new StreamWriter(SoakOutputFile))
            {
                writer.WriteLine("Total HC Emissions: {0}", this.soakResults);
            }

            using (StreamWriter writer = new StreamWriter(StartOutputFile))
            {
                writer.WriteLine("HC = {0} \n, CO = {1}, \n, NOX = {2}", this.startResults[0], this.startResults[1], this.startResults[2]);
            }
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
