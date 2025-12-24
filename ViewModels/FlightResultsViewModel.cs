using System.Collections.Generic;
using MoteurDeRechercheDeVol.Models;

namespace MoteurDeRechercheDeVol.ViewModels
{
    public class FlightResultsViewModel
    {
        public FlightSearchViewModel SearchCriteria { get; set; }
        public List<FlightOffer> FlightOffers { get; set; }
        public int TotalResults { get; set; }
        public FilterOptions AppliedFilters { get; set; }
    }

    public class FilterOptions
    {
        public string SortBy { get; set; }
        public bool? DirectOnly { get; set; }
        public string DepartureTime { get; set; }
        public string ArrivalTime { get; set; }
    }
}
