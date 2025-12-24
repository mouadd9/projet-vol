using System;
using System.Collections.Generic;

namespace MoteurDeRechercheDeVol.Models
{
    public class FlightOffer
    {
        public string Id { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public List<FlightSegment> OutboundSegments { get; set; }
        public List<FlightSegment> ReturnSegments { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int NumberOfStops { get; set; }
        public string Airline { get; set; }
        public string AirlineCode { get; set; }
    }

    public class FlightSegment
    {
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string FlightNumber { get; set; }
        public string AirlineName { get; set; }
    }
}
