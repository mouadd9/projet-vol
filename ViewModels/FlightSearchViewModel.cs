using System;
using System.ComponentModel.DataAnnotations;

namespace MoteurDeRechercheDeVol.ViewModels
{
    public class FlightSearchViewModel
    {
        [Required(ErrorMessage = "Please select trip type")]
        public string TripType { get; set; } = "round-trip";

        [Required(ErrorMessage = "Departure city is required")]
        public string DepartureCity { get; set; }

        [Required]
        public string DepartureCityCode { get; set; }

        [Required(ErrorMessage = "Arrival city is required")]
        public string ArrivalCity { get; set; }

        [Required]
        public string ArrivalCityCode { get; set; }

        [Required(ErrorMessage = "Departure date is required")]
        [DataType(DataType.Date)]
        public DateTime DepartureDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? ReturnDate { get; set; }

        [Required]
        [Range(1, 9, ErrorMessage = "Number of passengers must be between 1 and 9")]
        public int NumberOfPassengers { get; set; } = 1;

        [Required(ErrorMessage = "Please select class type")]
        public string ClassType { get; set; } = "ECONOMY";
    }
}
