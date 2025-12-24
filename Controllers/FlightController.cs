using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MoteurDeRechercheDeVol.Data;
using MoteurDeRechercheDeVol.Models;
using MoteurDeRechercheDeVol.Services;
using MoteurDeRechercheDeVol.ViewModels;
using Newtonsoft.Json;

namespace MoteurDeRechercheDeVol.Controllers
{
    public class FlightController : Controller
    {
        private readonly IFlightApiService _flightApiService;
        private readonly ApplicationDbContext _context;

        public FlightController(IFlightApiService flightApiService, ApplicationDbContext context)
        {
            _flightApiService = flightApiService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Search()
        {
            var model = new FlightSearchViewModel
            {
                DepartureDate = DateTime.Today.AddDays(7),
                ReturnDate = DateTime.Today.AddDays(14),
                NumberOfPassengers = 1,
                ClassType = "ECONOMY",
                TripType = "round-trip"
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Search(FlightSearchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.TripType == "round-trip" && model.ReturnDate.HasValue && model.ReturnDate < model.DepartureDate)
            {
                ModelState.AddModelError("ReturnDate", "Return date cannot be before departure date.");
                return View(model);
            }

            // Save search to history using try-catch to avoid crashing if DB is not set up
            try
            {
                var searchHistory = new SearchHistory
                {
                    DepartureCity = model.DepartureCity,
                    ArrivalCity = model.ArrivalCity,
                    DepartureDate = model.DepartureDate,
                    ReturnDate = model.ReturnDate,
                    NumberOfPassengers = model.NumberOfPassengers,
                    ClassType = model.ClassType,
                    TripType = model.TripType,
                    SearchDate = DateTime.Now
                };

                _context.SearchHistories.Add(searchHistory);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Ignore DB errors for now to allow app to run without DB
            }

            // Convert ViewModel to API request
            var searchRequest = new FlightSearchRequest
            {
                TripType = model.TripType,
                DepartureCity = model.DepartureCity,
                DepartureCityCode = model.DepartureCityCode,
                ArrivalCity = model.ArrivalCity,
                ArrivalCityCode = model.ArrivalCityCode,
                DepartureDate = model.DepartureDate,
                ReturnDate = model.ReturnDate,
                NumberOfPassengers = model.NumberOfPassengers,
                ClassType = model.ClassType
            };

            try
            {
                var flights = await _flightApiService.SearchFlightsAsync(searchRequest);

                var resultsViewModel = new FlightResultsViewModel
                {
                    SearchCriteria = model,
                    FlightOffers = flights,
                    TotalResults = flights.Count
                };

                // Store in Session for filtering/sorting (avoids HTTP 431 Cookie too large error)
                HttpContext.Session.SetString("SearchCriteria", JsonConvert.SerializeObject(model));
                HttpContext.Session.SetString("AllFlights", JsonConvert.SerializeObject(flights));

                return View("Results", resultsViewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error searching flights: {ex.Message}");
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult FilterAndSort(string sortBy, bool? directOnly, string departureTime, string arrivalTime)
        {
            var searchCriteriaJson = HttpContext.Session.GetString("SearchCriteria");
            var allFlightsJson = HttpContext.Session.GetString("AllFlights");

            if (string.IsNullOrEmpty(allFlightsJson))
            {
                return RedirectToAction("Search");
            }

            var searchCriteria = JsonConvert.DeserializeObject<FlightSearchViewModel>(searchCriteriaJson);
            var allFlights = JsonConvert.DeserializeObject<List<FlightOffer>>(allFlightsJson);

            // Apply filters
            var filteredFlights = allFlights.AsEnumerable();

            if (directOnly == true)
            {
                filteredFlights = filteredFlights.Where(f => f.NumberOfStops == 0);
            }

            if (!string.IsNullOrEmpty(departureTime))
            {
                // Parse time filter (e.g., "morning", "afternoon", "evening")
                filteredFlights = FilterByDepartureTime(filteredFlights, departureTime);
            }

            if (!string.IsNullOrEmpty(arrivalTime))
            {
                filteredFlights = FilterByArrivalTime(filteredFlights, arrivalTime);
            }

            // Apply sorting
            filteredFlights = sortBy switch
            {
                "price-asc" => filteredFlights.OrderBy(f => f.Price),
                "price-desc" => filteredFlights.OrderByDescending(f => f.Price),
                "duration-asc" => filteredFlights.OrderBy(f => f.TotalDuration),
                "duration-desc" => filteredFlights.OrderByDescending(f => f.TotalDuration),
                "stops-asc" => filteredFlights.OrderBy(f => f.NumberOfStops),
                _ => filteredFlights
            };

            var resultsViewModel = new FlightResultsViewModel
            {
                SearchCriteria = searchCriteria,
                FlightOffers = filteredFlights.ToList(),
                TotalResults = filteredFlights.Count(),
                AppliedFilters = new FilterOptions
                {
                    SortBy = sortBy,
                    DirectOnly = directOnly,
                    DepartureTime = departureTime,
                    ArrivalTime = arrivalTime
                }
            };

            return View("Results", resultsViewModel);
        }

        [HttpGet]
        public IActionResult ModifySearch(string searchData)
        {
            if (string.IsNullOrEmpty(searchData))
            {
                return RedirectToAction("Search");
            }

            var model = JsonConvert.DeserializeObject<FlightSearchViewModel>(searchData);
            return View("Search", model);
        }

        [HttpPost]
        public IActionResult KeepTempData()
        {
            // Session is automatically kept, no action needed
            return Ok();
        }

        private IEnumerable<FlightOffer> FilterByDepartureTime(IEnumerable<FlightOffer> flights, string timeFilter)
        {
            return timeFilter.ToLower() switch
            {
                "morning" => flights.Where(f => f.OutboundSegments.First().DepartureTime.Hour >= 6 &&
                                               f.OutboundSegments.First().DepartureTime.Hour < 12),
                "afternoon" => flights.Where(f => f.OutboundSegments.First().DepartureTime.Hour >= 12 &&
                                                 f.OutboundSegments.First().DepartureTime.Hour < 18),
                "evening" => flights.Where(f => f.OutboundSegments.First().DepartureTime.Hour >= 18 ||
                                               f.OutboundSegments.First().DepartureTime.Hour < 6),
                _ => flights
            };
        }

        private IEnumerable<FlightOffer> FilterByArrivalTime(IEnumerable<FlightOffer> flights, string timeFilter)
        {
            return timeFilter.ToLower() switch
            {
                "morning" => flights.Where(f => f.OutboundSegments.Last().ArrivalTime.Hour >= 6 &&
                                               f.OutboundSegments.Last().ArrivalTime.Hour < 12),
                "afternoon" => flights.Where(f => f.OutboundSegments.Last().ArrivalTime.Hour >= 12 &&
                                                 f.OutboundSegments.Last().ArrivalTime.Hour < 18),
                "evening" => flights.Where(f => f.OutboundSegments.Last().ArrivalTime.Hour >= 18 ||
                                               f.OutboundSegments.Last().ArrivalTime.Hour < 6),
                _ => flights
            };
        }
    }
}
