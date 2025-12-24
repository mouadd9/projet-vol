using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;
using MoteurDeRechercheDeVol.Models;
using Newtonsoft.Json;

namespace MoteurDeRechercheDeVol.Services
{
    public class AmadeusApiService : IFlightApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private string? _accessToken;
        private DateTime _tokenExpiration;

        public AmadeusApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // Check if token is still valid
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            var client = _httpClientFactory.CreateClient();
            var clientId = _configuration["AmadeusApi:ClientId"];
            var clientSecret = _configuration["AmadeusApi:ClientSecret"];
            var baseUrl = _configuration["AmadeusApi:BaseUrl"];

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId ?? string.Empty),
                new KeyValuePair<string, string>("client_secret", clientSecret ?? string.Empty)
            });

            var response = await client.PostAsync($"{baseUrl}/v1/security/oauth2/token", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            if (tokenData == null || tokenData.access_token == null)
            {
                throw new Exception("Failed to retrieve access token from Amadeus API.");
            }

            _accessToken = tokenData.access_token;
            _tokenExpiration = DateTime.UtcNow.AddSeconds((int?)tokenData.expires_in ?? 1799); // Default to ~30 mins if missing

            return _accessToken;
        }

        public async Task<List<FlightOffer>> SearchFlightsAsync(FlightSearchRequest request)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["AmadeusApi:BaseUrl"];

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Build query parameters
            var queryParams = new Dictionary<string, string>
            {
                { "originLocationCode", request.DepartureCityCode },
                { "destinationLocationCode", request.ArrivalCityCode },
                { "departureDate", request.DepartureDate.ToString("yyyy-MM-dd") },
                { "adults", request.NumberOfPassengers.ToString() },
                { "currencyCode", "USD" },
                { "max", "50" }
            };

            // Map class type to Amadeus format if needed, or send as is
            // Amadeus accepts: ECONOMY, PREMIUM_ECONOMY, BUSINESS, FIRST
            if (!string.IsNullOrEmpty(request.ClassType))
            {
                queryParams.Add("travelClass", request.ClassType);
            }

            if (request.TripType == "round-trip" && request.ReturnDate.HasValue)
            {
                queryParams.Add("returnDate", request.ReturnDate.Value.ToString("yyyy-MM-dd"));
            }

            if (request.DirectFlightOnly == true)
            {
                queryParams.Add("nonStop", "true");
            }

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var response = await client.GetAsync($"{baseUrl}/v2/shopping/flight-offers?{queryString}");
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            // Parse the response and convert to FlightOffer objects
            var flightOffers = new List<FlightOffer>();

            if (apiResponse.data != null)
            {
                foreach (var offer in apiResponse.data)
                {
                    var flightOffer = ParseFlightOffer(offer);
                    flightOffers.Add(flightOffer);
                }
            }

            return flightOffers;
        }

        public async Task<List<City>> SearchCitiesAsync(string searchTerm)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["AmadeusApi:BaseUrl"];

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(
                $"{baseUrl}/v1/reference-data/locations?subType=CITY,AIRPORT&keyword={Uri.EscapeDataString(searchTerm)}&page[limit]=10");

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            var cities = new List<City>();
            if (apiResponse.data != null)
            {
                foreach (var location in apiResponse.data)
                {
                    cities.Add(new City
                    {
                        CityName = location.address.cityName,
                        CityCode = location.iataCode,
                        CountryCode = location.address.countryCode,
                        AirportName = location.name
                    });
                }
            }

            return cities;
        }

        private FlightOffer ParseFlightOffer(dynamic offer)
        {
            // Implementation to parse Amadeus API response into FlightOffer object
            var flightOffer = new FlightOffer
            {
                Id = offer.id,
                Price = decimal.Parse(offer.price.total.ToString()),
                Currency = offer.price.currency,
                OutboundSegments = new List<FlightSegment>(),
                ReturnSegments = new List<FlightSegment>()
            };

            // Parse itineraries (outbound and return)
            int itineraryIndex = 0;
            foreach (var itinerary in offer.itineraries)
            {
                var segments = new List<FlightSegment>();

                foreach (var segment in itinerary.segments)
                {
                    segments.Add(new FlightSegment
                    {
                        DepartureAirport = segment.departure.iataCode,
                        ArrivalAirport = segment.arrival.iataCode,
                        DepartureTime = DateTime.Parse(segment.departure.at.ToString()),
                        ArrivalTime = DateTime.Parse(segment.arrival.at.ToString()),
                        Duration = ParseDuration(segment.duration.ToString()),
                        FlightNumber = segment.number,
                        AirlineName = segment.carrierCode
                    });
                }

                if (itineraryIndex == 0)
                    flightOffer.OutboundSegments = segments;
                else
                    flightOffer.ReturnSegments = segments;

                itineraryIndex++;
            }

            flightOffer.NumberOfStops = flightOffer.OutboundSegments.Count - 1;
            flightOffer.TotalDuration = CalculateTotalDuration(flightOffer.OutboundSegments);

            return flightOffer;
        }

        private TimeSpan ParseDuration(string isoDuration)
        {
            // Parse ISO 8601 duration format (PT2H30M)
            try
            {
                return XmlConvert.ToTimeSpan(isoDuration);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private TimeSpan CalculateTotalDuration(List<FlightSegment> segments)
        {
            if (segments == null || !segments.Any())
                return TimeSpan.Zero;

            var start = segments.First().DepartureTime;
            var end = segments.Last().ArrivalTime;
            return end - start;
        }
    }
}
