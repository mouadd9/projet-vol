using System;

namespace MoteurDeRechercheDeVol.Models
{
    public class FlightSearchRequest
    {
        public string TripType { get; set; } // "round-trip" or "one-way"
        public string DepartureCity { get; set; }
        public string DepartureCityCode { get; set; }
        public string ArrivalCity { get; set; }
        public string ArrivalCityCode { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public int NumberOfPassengers { get; set; } = 1;
        public string ClassType { get; set; } // "ECONOMY", "BUSINESS", "FIRST"

        // Filtering options
        public bool? DirectFlightOnly { get; set; }
        public TimeSpan? MinDepartureTime { get; set; }
        public TimeSpan? MaxDepartureTime { get; set; }
        public TimeSpan? MinArrivalTime { get; set; }
        public TimeSpan? MaxArrivalTime { get; set; }
    }
}
