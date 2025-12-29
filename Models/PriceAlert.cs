using System;

namespace MoteurDeRechercheDeVol.Models
{
    public class PriceAlert
    {
        public int Id { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public decimal TargetPrice { get; set; }
        public string Currency { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastCheckedDate { get; set; }
    }
}

