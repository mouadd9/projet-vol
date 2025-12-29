using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

                var totalResults = flights.Count;
                var pageSize = 10;
                var page = 1;
                var totalPages = (int)Math.Ceiling((double)totalResults / pageSize);
                
                var paginatedFlights = flights
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var resultsViewModel = new FlightResultsViewModel
                {
                    SearchCriteria = model,
                    FlightOffers = paginatedFlights,
                    TotalResults = totalResults,
                    CurrentPage = page,
                    PageSize = pageSize
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
        public IActionResult FilterAndSort(string sortBy, bool? directOnly, string departureTime, string arrivalTime, 
            string airlines, string departureAirport, string arrivalAirport, int page = 1)
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

            // Advanced filters
            if (!string.IsNullOrEmpty(airlines))
            {
                var airlineCode = airlines.Trim().ToUpper();
                filteredFlights = filteredFlights.Where(f => 
                    f.OutboundSegments.Any(s => (s.AirlineName?.ToUpper() ?? "") == airlineCode) ||
                    (f.ReturnSegments != null && f.ReturnSegments.Any(s => (s.AirlineName?.ToUpper() ?? "") == airlineCode))
                );
            }

            if (!string.IsNullOrEmpty(departureAirport))
            {
                var airportCode = departureAirport.Trim().ToUpper();
                filteredFlights = filteredFlights.Where(f => 
                    f.OutboundSegments.Any(s => 
                        s.DepartureAirport?.ToUpper() == airportCode
                    )
                );
            }

            if (!string.IsNullOrEmpty(arrivalAirport))
            {
                var airportCode = arrivalAirport.Trim().ToUpper();
                filteredFlights = filteredFlights.Where(f => 
                    f.OutboundSegments.Any(s => 
                        s.ArrivalAirport?.ToUpper() == airportCode
                    ) ||
                    (f.ReturnSegments != null && f.ReturnSegments.Any(s => 
                        s.ArrivalAirport?.ToUpper() == airportCode
                    ))
                );
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

            var totalResults = filteredFlights.Count();
            var pageSize = 10;
            var totalPages = (int)Math.Ceiling((double)totalResults / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            
            var paginatedFlights = filteredFlights
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var resultsViewModel = new FlightResultsViewModel
            {
                SearchCriteria = searchCriteria,
                FlightOffers = paginatedFlights,
                TotalResults = totalResults,
                CurrentPage = page,
                PageSize = pageSize,
                AppliedFilters = new FilterOptions
                {
                    SortBy = sortBy,
                    DirectOnly = directOnly,
                    DepartureTime = departureTime,
                    ArrivalTime = arrivalTime,
                    Airlines = airlines,
                    DepartureAirport = departureAirport,
                    ArrivalAirport = arrivalAirport
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

        [HttpPost]
        public IActionResult SelectFlight(string flightId, string currency, string price)
        {
            // Store selected flight in session
            HttpContext.Session.SetString("SelectedFlightId", flightId ?? "");
            HttpContext.Session.SetString("SelectedFlightPrice", price ?? "");
            HttpContext.Session.SetString("SelectedFlightCurrency", currency ?? "");
            
            return Json(new { success = true, message = "Flight selected successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            try
            {
                var history = await _context.SearchHistories
                    .OrderByDescending(h => h.SearchDate)
                    .Take(50)
                    .ToListAsync();
                
                return View(history);
            }
            catch
            {
                return View(new List<SearchHistory>());
            }
        }


        [HttpPost]
        public async Task<IActionResult> CreatePriceAlert(string departureCity, string arrivalCity, 
            DateTime departureDate, DateTime? returnDate, decimal targetPrice, string currency, string email)
        {
            try
            {
                var alert = new PriceAlert
                {
                    DepartureCity = departureCity,
                    ArrivalCity = arrivalCity,
                    DepartureDate = departureDate,
                    ReturnDate = returnDate,
                    TargetPrice = targetPrice,
                    Currency = currency,
                    Email = email,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.PriceAlerts.Add(alert);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Price alert created successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetFlightDetails(string flightId)
        {
            try
            {
                var allFlightsJson = HttpContext.Session.GetString("AllFlights");
                if (string.IsNullOrEmpty(allFlightsJson))
                {
                    return Json(new { success = false, message = "Flight data not found" });
                }

                var allFlights = JsonConvert.DeserializeObject<List<FlightOffer>>(allFlightsJson);
                var flight = allFlights?.FirstOrDefault(f => f.Id == flightId);

                if (flight == null)
                {
                    return Json(new { success = false, message = "Flight not found" });
                }

                return Json(new { success = true, flight = flight });
            }
            catch
            {
                return Json(new { success = false, message = "Error retrieving flight details" });
            }
        }

        [HttpGet]
        public IActionResult GetFilterOptions()
        {
            try
            {
                var allFlightsJson = HttpContext.Session.GetString("AllFlights");
                if (string.IsNullOrEmpty(allFlightsJson))
                {
                    return Json(new { success = false, message = "No flight data available" });
                }

                var allFlights = JsonConvert.DeserializeObject<List<FlightOffer>>(allFlightsJson);
                
                // Get unique airlines
                var airlines = allFlights
                    .SelectMany(f => f.OutboundSegments.Select(s => s.AirlineName))
                    .Concat(allFlights.Where(f => f.ReturnSegments != null)
                        .SelectMany(f => f.ReturnSegments.Select(s => s.AirlineName)))
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList();

                // Get unique airports
                var airports = allFlights
                    .SelectMany(f => f.OutboundSegments.Select(s => new { s.DepartureAirport, s.ArrivalAirport }))
                    .Concat(allFlights.Where(f => f.ReturnSegments != null)
                        .SelectMany(f => f.ReturnSegments.Select(s => new { s.DepartureAirport, s.ArrivalAirport })))
                    .SelectMany(a => new[] { a.DepartureAirport, a.ArrivalAirport })
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList();

                return Json(new { success = true, airlines = airlines, airports = airports });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
