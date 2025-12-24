using System.Collections.Generic;
using System.Threading.Tasks;
using MoteurDeRechercheDeVol.Models;

namespace MoteurDeRechercheDeVol.Services
{
    public interface IFlightApiService
    {
        Task<string> GetAccessTokenAsync();
        Task<List<FlightOffer>> SearchFlightsAsync(FlightSearchRequest request);
        Task<List<City>> SearchCitiesAsync(string searchTerm);
    }
}
