using System;

namespace MoteurDeRechercheDeVol.Models
{
    public class SearchHistory
    {
        public int Id { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public int NumberOfPassengers { get; set; }
        public string ClassType { get; set; }
        public string TripType { get; set; }
        public DateTime SearchDate { get; set; }
    }
}
