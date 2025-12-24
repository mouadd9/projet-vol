# Flight Search Web Service - Complete Development Guide

## PROJECT OVERVIEW
Build a fully functional flight search web application using ASP.NET Core MVC with MySQL database. The application will integrate with flight APIs (Amadeus/Travelport/Sabre) to search, display, sort, and filter flight results.

---

## TECHNOLOGY STACK

### Backend
- **Framework**: ASP.NET Core 8.0 MVC
- **Language**: C# 12
- **Database**: MySQL 8.0+
- **ORM**: Entity Framework Core with Pomelo.EntityFrameworkCore.MySql
- **API Integration**: Amadeus Developer API (primary choice - has free tier)

### Frontend
- **View Engine**: Razor (.cshtml)
- **Styling**: Bootstrap 5.3
- **Autocomplete**: jQuery UI or Select2
- **No separate JavaScript framework** (server-side rendering only)

### Required NuGet Packages
```
Pomelo.EntityFrameworkCore.MySql
Microsoft.EntityFrameworkCore.Tools
Newtonsoft.Json (for API responses)
RestSharp (for API calls)
```

---

## DATABASE SCHEMA

### Tables to Create

#### 1. SearchHistory Table
```sql
CREATE TABLE SearchHistory (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    DepartureCity VARCHAR(100) NOT NULL,
    ArrivalCity VARCHAR(100) NOT NULL,
    DepartureDate DATE NOT NULL,
    ReturnDate DATE NULL,
    NumberOfPassengers INT NOT NULL,
    ClassType VARCHAR(50) NOT NULL,
    TripType VARCHAR(20) NOT NULL,
    SearchDate DATETIME NOT NULL,
    INDEX idx_search_date (SearchDate)
);
```

#### 2. Cities Table (for autocomplete)
```sql
CREATE TABLE Cities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CityName VARCHAR(100) NOT NULL,
    CityCode VARCHAR(10) NOT NULL,
    CountryCode VARCHAR(10) NOT NULL,
    AirportName VARCHAR(200),
    INDEX idx_city_name (CityName),
    INDEX idx_city_code (CityCode)
);
```

---

## DEVELOPMENT PHASES

## PHASE 1: PROJECT SETUP & STRUCTURE

### 1.1 Create Project Structure
```
FlightSearchApp/
├── Controllers/
│   ├── HomeController.cs
│   ├── FlightController.cs
│   └── ApiController.cs
├── Models/
│   ├── FlightSearchRequest.cs
│   ├── FlightSearchResponse.cs
│   ├── FlightOffer.cs
│   ├── SearchHistory.cs
│   └── City.cs
├── ViewModels/
│   ├── FlightSearchViewModel.cs
│   └── FlightResultsViewModel.cs
├── Services/
│   ├── IFlightApiService.cs
│   ├── AmadeusApiService.cs
│   └── CityService.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   └── _ValidationScriptsPartial.cshtml
│   ├── Home/
│   │   └── Index.cshtml
│   └── Flight/
│       ├── Search.cshtml
│       └── Results.cshtml
└── wwwroot/
    ├── css/
    ├── js/
    └── images/
```

### 1.2 Configure MySQL Connection
In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FlightSearchDB;User=root;Password=your_password;"
  },
  "AmadeusApi": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "BaseUrl": "https://test.api.amadeus.com"
  }
}
```

In `Program.cs`:
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

builder.Services.AddScoped<IFlightApiService, AmadeusApiService>();
builder.Services.AddScoped<CityService>();
builder.Services.AddHttpClient();
```

---

## PHASE 2: DATA MODELS & DATABASE CONTEXT

### 2.1 Create Model Classes

**Models/FlightSearchRequest.cs**
```csharp
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
```

**Models/FlightOffer.cs**
```csharp
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
```

**Models/SearchHistory.cs** (Entity Framework model)
```csharp
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
```

**Models/City.cs**
```csharp
public class City
{
    public int Id { get; set; }
    public string CityName { get; set; }
    public string CityCode { get; set; }
    public string CountryCode { get; set; }
    public string AirportName { get; set; }
}
```

### 2.2 Create DbContext

**Data/ApplicationDbContext.cs**
```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SearchHistory> SearchHistories { get; set; }
    public DbSet<City> Cities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<SearchHistory>()
            .HasIndex(s => s.SearchDate);
            
        modelBuilder.Entity<City>()
            .HasIndex(c => c.CityName);
    }
}
```

### 2.3 Run Migrations
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## PHASE 3: AMADEUS API INTEGRATION

### 3.1 Create API Service Interface

**Services/IFlightApiService.cs**
```csharp
public interface IFlightApiService
{
    Task<string> GetAccessTokenAsync();
    Task<List<FlightOffer>> SearchFlightsAsync(FlightSearchRequest request);
    Task<List<City>> SearchCitiesAsync(string searchTerm);
}
```

### 3.2 Implement Amadeus API Service

**Services/AmadeusApiService.cs**
```csharp
public class AmadeusApiService : IFlightApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private string _accessToken;
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
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        var response = await client.PostAsync($"{baseUrl}/v1/security/oauth2/token", content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var tokenData = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

        _accessToken = tokenData.access_token;
        _tokenExpiration = DateTime.UtcNow.AddSeconds((int)tokenData.expires_in - 60);

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
            { "travelClass", request.ClassType },
            { "currencyCode", "USD" },
            { "max", "50" }
        };

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
        
        foreach (var offer in apiResponse.data)
        {
            var flightOffer = ParseFlightOffer(offer);
            flightOffers.Add(flightOffer);
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

        return cities;
    }

    private FlightOffer ParseFlightOffer(dynamic offer)
    {
        // Implementation to parse Amadeus API response into FlightOffer object
        // This is a complex mapping - needs careful implementation
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
        var duration = System.Xml.XmlConvert.ToTimeSpan(isoDuration);
        return duration;
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
```

---

## PHASE 4: CONTROLLERS

### 4.1 Home Controller

**Controllers/HomeController.cs**
```csharp
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Search", "Flight");
    }
}
```

### 4.2 Flight Controller

**Controllers/FlightController.cs**
```csharp
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

        // Save search to history
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

            // Store in TempData for filtering/sorting
            TempData["SearchCriteria"] = JsonConvert.SerializeObject(model);
            TempData["AllFlights"] = JsonConvert.SerializeObject(flights);

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
        var searchCriteriaJson = TempData["SearchCriteria"]?.ToString();
        var allFlightsJson = TempData["AllFlights"]?.ToString();

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

        // Re-store in TempData
        TempData["SearchCriteria"] = searchCriteriaJson;
        TempData["AllFlights"] = allFlightsJson;

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
```

### 4.3 API Controller (for autocomplete)

**Controllers/ApiController.cs**
```csharp
[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly IFlightApiService _flightApiService;

    public ApiController(IFlightApiService flightApiService)
    {
        _flightApiService = flightApiService;
    }

    [HttpGet("cities")]
    public async Task<IActionResult> SearchCities([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            return Ok(new List<object>());
        }

        try
        {
            var cities = await _flightApiService.SearchCitiesAsync(term);
            
            var results = cities.Select(c => new
            {
                label = $"{c.CityName} ({c.CityCode}) - {c.AirportName}",
                value = c.CityName,
                code = c.CityCode
            });

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
```

---

## PHASE 5: VIEW MODELS

**ViewModels/FlightSearchViewModel.cs**
```csharp
public class FlightSearchViewModel
{
    [Required(ErrorMessage = "Please select trip type")]
    public string TripType { get; set; }

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
    public DateTime DepartureDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ReturnDate { get; set; }

    [Required]
    [Range(1, 9, ErrorMessage = "Number of passengers must be between 1 and 9")]
    public int NumberOfPassengers { get; set; }

    [Required(ErrorMessage = "Please select class type")]
    public string ClassType { get; set; }
}
```

**ViewModels/FlightResultsViewModel.cs**
```csharp
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
```

---

## PHASE 6: VIEWS

### 6.1 Layout

**Views/Shared/_Layout.cshtml**
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - Flight Search</title>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="https://code.jquery.com/ui/1.13.2/themes/base/jquery-ui.css" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark bg-primary">
        <div class="container">
            <a class="navbar-brand" href="/">
                <i class="fas fa-plane"></i> FlightSearch
            </a>
        </div>
    </nav>

    <main role="main" class="pb-3">
        @RenderBody()
    </main>

    <footer class="border-top footer text-muted">
        <div class="container text-center">
            &copy; 2025 - Flight Search Application
        </div>
    </footer>

    <script src="https://code.jquery.com/jquery-3.7.0.min.js"></script>
    <script src="https://code.jquery.com/ui/1.13.2/jquery-ui.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### 6.2 Search View

**Views/Flight/Search.cshtml**
```html
@model FlightSearchViewModel

@{
    ViewData["Title"] = "Search Flights";
}

<div class="container mt-5">
    <div class="row justify-content-center">
        <div class="col-lg-10">
            <div class="card shadow">
                <div class="card-header bg-primary text-white">
                    <h3 class="mb-0"><i class="fas fa-search"></i> Search Flights</h3>
                </div>
                <div class="card-body p-4">
                    <form asp-action="Search" method="post" id="searchForm">
                        <div asp-validation-summary="ModelOnly" class="text-danger"></div>

                        <!-- Trip Type -->
                        <div class="mb-4">
                            <label class="form-label fw-bold">Trip Type</label>
                            <div class="btn-group w-100" role="group">
                                <input type="radio" class="btn-check" name="TripType" id="roundTrip" 
                                       value="round-trip" asp-for="TripType" checked>
                                <label class="btn btn-outline-primary" for="roundTrip">
                                    <i class="fas fa-exchange-alt"></i> Round Trip
                                </label>

                                <input type="radio" class="btn-check" name="TripType" id="oneWay" 
                                       value="one-way" asp-for="TripType">
                                <label class="btn btn-outline-primary" for="oneWay">
                                    <i class="fas fa-arrow-right"></i> One Way
                                </label>
                            </div>
                        </div>

                        <!-- Cities Row -->
                        <div class="row mb-3">
                            <div class="col-md-6">
                                <label asp-for="DepartureCity" class="form-label fw-bold">From</label>
                                <div class="input-group">
                                    <span class="input-group-text"><i class="fas fa-plane-departure"></i></span>
                                    <input asp-for="DepartureCity" class="form-control" 
                                           placeholder="Departure city" id="departureCity" />
                                    <input asp-for="DepartureCityCode" type="hidden" id="departureCityCode" />
                                </div>
                                <span asp-validation-for="DepartureCity" class="text-danger"></span>
                            </div>

                            <div class="col-md-6">
                                <label asp-for="ArrivalCity" class="form-label fw-bold">To</label>
                                <div class="input-group">
                                    <span class="input-group-text"><i class="fas fa-plane-arrival"></i></span>
                                    <input asp-for="ArrivalCity" class="form-control" 
                                           placeholder="Arrival city" id="arrivalCity" />
                                    <input asp-for="ArrivalCityCode" type="hidden" id="arrivalCityCode" />
                                </div>
                                <span asp-validation-for="ArrivalCity" class="text-danger"></span>
                            </div>
                        </div>

                        <!-- Dates Row -->
                        <div class="row mb-3">
                            <div class="col-md-6">
                                <label asp-for="DepartureDate" class="form-label fw-bold">Departure Date</label>
                                <input asp-for="DepartureDate" type="date" class="form-control" 
                                       min="@DateTime.Today.ToString("yyyy-MM-dd")" />
                                <span asp-validation-for="DepartureDate" class="text-danger"></span>
                            </div>

                            <div class="col-md-6" id="returnDateDiv">
                                <label asp-for="ReturnDate" class="form-label fw-bold">Return Date</label>
                                <input asp-for="ReturnDate" type="date" class="form-control" 
                                       min="@DateTime.Today.ToString("yyyy-MM-dd")" />
                                <span asp-validation-for="ReturnDate" class="text-danger"></span>
                            </div>
                        </div>

                        <!-- Passengers and Class -->
                        <div class="row mb-4">
                            <div class="col-md-6">
                                <label asp-for="NumberOfPassengers" class="form-label fw-bold">Passengers</label>
                                <select asp-for="NumberOfPassengers" class="form-select">
                                    @for (int i = 1; i <= 9; i++)
                                    {
                                        <option value="@i">@i @(i == 1 ? "Passenger" : "Passengers")</option>
                                    }
                                </select>
                            </div>

                            <div class="col-md-6">
                                <label asp-for="ClassType" class="form-label fw-bold">Class</label>
                                <select asp-for="ClassType" class="form-select">
                                    <option value="ECONOMY">Economy</option>
                                    <option value="PREMIUM_ECONOMY">Premium Economy</option>
                                    <option value="BUSINESS">Business</option>
                                    <option value="FIRST">First Class</option>
                                </select>
                            </div>
                        </div>

                        <!-- Submit Button -->
                        <div class="d-grid">
                            <button type="submit" class="btn btn-primary btn-lg">
                                <i class="fas fa-search"></i> Search Flights
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <script>
        $(document).ready(function() {
            // Autocomplete for departure city
            $("#departureCity").autocomplete({
                source: function(request, response) {
                    $.ajax({
                        url: "/api/cities",
                        data: { term: request.term },
                        success: function(data) {
                            response(data);
                        }
                    });
                },
                minLength: 2,
                select: function(event, ui) {
                    $("#departureCityCode").val(ui.item.code);
                }
            });

            // Autocomplete for arrival city
            $("#arrivalCity").autocomplete({
                source: function(request, response) {
                    $.ajax({
                        url: "/api/cities",
                        data: { term: request.term },
                        success: function(data) {
                            response(data);
                        }
                    });
                },
                minLength: 2,
                select: function(event, ui) {
                    $("#arrivalCityCode").val(ui.item.code);
                }
            });

            // Toggle return date based on trip type
            $('input[name="TripType"]').change(function() {
                if ($(this).val() === 'one-way') {
                    $('#returnDateDiv').hide();
                    $('input[name="ReturnDate"]').removeAttr('required');
                } else {
                    $('#returnDateDiv').show();
                    $('input[name="ReturnDate"]').attr('required', 'required');
                }
            });

            // Initialize visibility
            if ($('input[name="TripType"]:checked').val() === 'one-way') {
                $('#returnDateDiv').hide();
            }
        });
    </script>
}
```

### 6.3 Results View

**Views/Flight/Results.cshtml**
```html
@model FlightResultsViewModel

@{
    ViewData["Title"] = "Flight Results";
}

<div class="container mt-4">
    <!-- Search Summary & Modify Button -->
    <div class="card mb-4">
        <div class="card-body">
            <div class="row align-items-center">
                <div class="col-md-8">
                    <h5 class="mb-2">
                        <i class="fas fa-plane-departure"></i> @Model.SearchCriteria.DepartureCity 
                        <i class="fas fa-arrow-right mx-2"></i> 
                        <i class="fas fa-plane-arrival"></i> @Model.SearchCriteria.ArrivalCity
                    </h5>
                    <p class="mb-0 text-muted">
                        @Model.SearchCriteria.DepartureDate.ToString("MMM dd, yyyy")
                        @if (Model.SearchCriteria.ReturnDate.HasValue)
                        {
                            <span> - @Model.SearchCriteria.ReturnDate.Value.ToString("MMM dd, yyyy")</span>
                        }
                        | @Model.SearchCriteria.NumberOfPassengers Passenger(s) | @Model.SearchCriteria.ClassType
                    </p>
                </div>
                <div class="col-md-4 text-end">
                    <a asp-action="ModifySearch" 
                       asp-route-searchData="@Newtonsoft.Json.JsonConvert.SerializeObject(Model.SearchCriteria)" 
                       class="btn btn-outline-primary">
                        <i class="fas fa-edit"></i> Modify Search
                    </a>
                </div>
            </div>
        </div>
    </div>

    <div class="row">
        <!-- Filters Sidebar -->
        <div class="col-lg-3">
            <div class="card shadow-sm">
                <div class="card-header bg-light">
                    <h6 class="mb-0"><i class="fas fa-filter"></i> Filters & Sort</h6>
                </div>
                <div class="card-body">
                    <form asp-action="FilterAndSort" method="post">
                        <!-- Sort Options -->
                        <div class="mb-3">
                            <label class="form-label fw-bold">Sort By</label>
                            <select name="sortBy" class="form-select form-select-sm" onchange="this.form.submit()">
                                <option value="">Best Match</option>
                                <option value="price-asc" selected="@(Model.AppliedFilters?.SortBy == "price-asc")">
                                    Price: Low to High
                                </option>
                                <option value="price-desc" selected="@(Model.AppliedFilters?.SortBy == "price-desc")">
                                    Price: High to Low
                                </option>
                                <option value="duration-asc" selected="@(Model.AppliedFilters?.SortBy == "duration-asc")">
                                    Duration: Shortest
                                </option>
                                <option value="duration-desc" selected="@(Model.AppliedFilters?.SortBy == "duration-desc")">
                                    Duration: Longest
                                </option>
                                <option value="stops-asc" selected="@(Model.AppliedFilters?.SortBy == "stops-asc")">
                                    Fewest Stops
                                </option>
                            </select>
                        </div>

                        <hr />

                        <!-- Stops Filter -->
                        <div class="mb-3">
                            <label class="form-label fw-bold">Stops</label>
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" name="directOnly" 
                                       id="directOnly" value="true" 
                                       checked="@(Model.AppliedFilters?.DirectOnly == true)"
                                       onchange="this.form.submit()">
                                <label class="form-check-label" for="directOnly">
                                    Direct flights only
                                </label>
                            </div>
                        </div>

                        <hr />

                        <!-- Departure Time Filter -->
                        <div class="mb-3">
                            <label class="form-label fw-bold">Departure Time</label>
                            <select name="departureTime" class="form-select form-select-sm" 
                                    onchange="this.form.submit()">
                                <option value="">Any time</option>
                                <option value="morning" selected="@(Model.AppliedFilters?.DepartureTime == "morning")">
                                    Morning (6AM - 12PM)
                                </option>
                                <option value="afternoon" selected="@(Model.AppliedFilters?.DepartureTime == "afternoon")">
                                    Afternoon (12PM - 6PM)
                                </option>
                                <option value="evening" selected="@(Model.AppliedFilters?.DepartureTime == "evening")">
                                    Evening (6PM - 6AM)
                                </option>
                            </select>
                        </div>

                        <!-- Arrival Time Filter -->
                        <div class="mb-3">
                            <label class="form-label fw-bold">Arrival Time</label>
                            <select name="arrivalTime" class="form-select form-select-sm" 
                                    onchange="this.form.submit()">
                                <option value="">Any time</option>
                                <option value="morning" selected="@(Model.AppliedFilters?.ArrivalTime == "morning")">
                                    Morning (6AM - 12PM)
                                </option>
                                <option value="afternoon" selected="@(Model.AppliedFilters?.ArrivalTime == "afternoon")">
                                    Afternoon (12PM - 6PM)
                                </option>
                                <option value="evening" selected="@(Model.AppliedFilters?.ArrivalTime == "evening")">
                                    Evening (6PM - 6AM)
                                </option>
                            </select>
                        </div>
                    </form>
                </div>
            </div>
        </div>

        <!-- Results List -->
        <div class="col-lg-9">
            <div class="mb-3">
                <h5>@Model.TotalResults flights found</h5>
            </div>

            @if (Model.FlightOffers == null || !Model.FlightOffers.Any())
            {
                <div class="alert alert-info">
                    <i class="fas fa-info-circle"></i> No flights found matching your criteria. 
                    Try adjusting your filters.
                </div>
            }
            else
            {
                @foreach (var flight in Model.FlightOffers)
                {
                    <div class="card mb-3 shadow-sm flight-card">
                        <div class="card-body">
                            <div class="row align-items-center">
                                <!-- Outbound Flight Info -->
                                <div class="col-md-8">
                                    <div class="mb-2">
                                        <span class="badge bg-primary">Outbound</span>
                                        @if (flight.NumberOfStops == 0)
                                        {
                                            <span class="badge bg-success">Direct</span>
                                        }
                                        else
                                        {
                                            <span class="badge bg-warning text-dark">
                                                @flight.NumberOfStops @(flight.NumberOfStops == 1 ? "Stop" : "Stops")
                                            </span>
                                        }
                                    </div>

                                    @foreach (var segment in flight.OutboundSegments)
                                    {
                                        <div class="d-flex align-items-center mb-2">
                                            <div class="flex-grow-1">
                                                <div class="row">
                                                    <div class="col-3">
                                                        <div class="fw-bold">@segment.DepartureTime.ToString("HH:mm")</div>
                                                        <div class="text-muted small">@segment.DepartureAirport</div>
                                                    </div>
                                                    <div class="col-6 text-center">
                                                        <div class="text-muted small">
                                                            @segment.Duration.ToString(@"h\h\ m\m")
                                                        </div>
                                                        <div>
                                                            <i class="fas fa-long-arrow-alt-right"></i>
                                                        </div>
                                                        <div class="text-muted small">@segment.AirlineName</div>
                                                    </div>
                                                    <div class="col-3 text-end">
                                                        <div class="fw-bold">@segment.ArrivalTime.ToString("HH:mm")</div>
                                                        <div class="text-muted small">@segment.ArrivalAirport</div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    }

                                    <!-- Return Flight Info -->
                                    @if (flight.ReturnSegments != null && flight.ReturnSegments.Any())
                                    {
                                        <hr />
                                        <div class="mb-2">
                                            <span class="badge bg-info">Return</span>
                                        </div>

                                        @foreach (var segment in flight.ReturnSegments)
                                        {
                                            <div class="d-flex align-items-center mb-2">
                                                <div class="flex-grow-1">
                                                    <div class="row">
                                                        <div class="col-3">
                                                            <div class="fw-bold">@segment.DepartureTime.ToString("HH:mm")</div>
                                                            <div class="text-muted small">@segment.DepartureAirport</div>
                                                        </div>
                                                        <div class="col-6 text-center">
                                                            <div class="text-muted small">
                                                                @segment.Duration.ToString(@"h\h\ m\m")
                                                            </div>
                                                            <div>
                                                                <i class="fas fa-long-arrow-alt-right"></i>
                                                            </div>
                                                            <div class="text-muted small">@segment.AirlineName</div>
                                                        </div>
                                                        <div class="col-3 text-end">
                                                            <div class="fw-bold">@segment.ArrivalTime.ToString("HH:mm")</div>
                                                            <div class="text-muted small">@segment.ArrivalAirport</div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        }
                                    }
                                </div>

                                <!-- Price & Book -->
                                <div class="col-md-4 text-center border-start">
                                    <div class="h3 text-primary mb-2">
                                        @flight.Currency @flight.Price.ToString("N2")
                                    </div>
                                    <div class="text-muted small mb-3">
                                        Total for @Model.SearchCriteria.NumberOfPassengers passenger(s)
                                    </div>
                                    <button class="btn btn-primary w-100">
                                        <i class="fas fa-ticket-alt"></i> Select Flight
                                    </button>
                                    <div class="mt-2">
                                        <small class="text-muted">
                                            Total Duration: @flight.TotalDuration.ToString(@"h\h\ m\m")
                                        </small>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                }
            }
        </div>
    </div>
</div>

@section Scripts {
    <script>
        // Keep TempData alive for page refresh
        $.post('@Url.Action("KeepTempData", "Flight")');
    </script>
}
```

---

## PHASE 7: CUSTOM CSS

**wwwroot/css/site.css**
```css
:root {
    --primary-color: #0066cc;
    --secondary-color: #28a745;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background-color: #f8f9fa;
}

.flight-card {
    transition: transform 0.2s, box-shadow 0.2s;
}

.flight-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0,0,0,0.15) !important;
}

.ui-autocomplete {
    max-height: 300px;
    overflow-y: auto;
    overflow-x: hidden;
    z-index: 1050;
}

.navbar-brand {
    font-size: 1.5rem;
    font-weight: bold;
}

.card-header {
    font-weight: 600;
}

.badge {
    font-size: 0.75rem;
    padding: 0.35em 0.65em;
}

/* Loading spinner */
.spinner-border-sm {
    width: 1rem;
    height: 1rem;
}

/* Form enhancements */
.form-control:focus,
.form-select:focus {
    border-color: var(--primary-color);
    box-shadow: 0 0 0 0.2rem rgba(0, 102, 204, 0.25);
}

.btn-primary {
    background-color: var(--primary-color);
    border-color: var(--primary-color);
}

.btn-primary:hover {
    background-color: #0052a3;
    border-color: #0052a3;
}

/* Flight segment styling */
.flight-segment {
    padding: 10px;
    border-left: 3px solid var(--primary-color);
    margin-bottom: 10px;
}

/* Responsive adjustments */
@media (max-width: 768px) {
    .flight-card .col-md-4 {
        border-top: 1px solid #dee2e6;
        border-left: none !important;
        margin-top: 15px;
        padding-top: 15px;
    }
}
```

---

## PHASE 8: AMADEUS API SETUP GUIDE

### Get Amadeus API Credentials

1. Go to https://developers.amadeus.com/
2. Click "Register" and create a free account
3. After login, go to "My Self-Service Workspace"
4. Create a new app
5. Copy your **API Key** (Client ID) and **API Secret** (Client Secret)
6. Use the **Test environment** for development (free tier)

### Add to appsettings.json
```json
"AmadeusApi": {
  "ClientId": "YOUR_CLIENT_ID_HERE",
  "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
  "BaseUrl": "https://test.api.amadeus.com"
}
```

---

## PHASE 9: TESTING & VALIDATION

### Test Cases to Verify

1. **Search Functionality**
   - [ ] Round trip search works
   - [ ] One-way search works
   - [ ] City autocomplete displays results
   - [ ] Validation prevents invalid dates
   - [ ] API integration returns results

2. **Filtering**
   - [ ] Direct flights filter works
   - [ ] Departure time filter works
   - [ ] Arrival time filter works
   - [ ] Multiple filters can be combined

3. **Sorting**
   - [ ] Sort by price (ascending/descending)
   - [ ] Sort by duration
   - [ ] Sort by number of stops

4. **Modify Search**
   - [ ] Clicking "Modify Search" pre-fills form
   - [ ] All previous values are retained
   - [ ] Can search again with modifications

5. **Database**
   - [ ] Search history is saved
   - [ ] Cities table is populated

---

## PHASE 10: ERROR HANDLING & IMPROVEMENTS

### Add Error Handling

1. **API Rate Limiting**: Handle 429 errors gracefully
2. **No Results**: Display helpful message when no flights found
3. **API Errors**: Catch and display user-friendly error messages
4. **Validation**: Ensure return date is after departure date
5. **Loading States**: Show spinner while searching

### Optional Enhancements

- Add pagination for results (show 20 per page)
- Cache API responses for 5 minutes
- Add flight details modal
- Export results to PDF
- Email search results
- Price alerts
- Multi-city search support

---

## DEPLOYMENT CHECKLIST

### Before Production

- [ ] Change to production Amadeus API endpoint
- [ ] Secure API credentials (use Azure Key Vault or similar)
- [ ] Add logging (Serilog recommended)
- [ ] Implement caching (Redis or Memory Cache)
- [ ] Add rate limiting
- [ ] Optimize database queries
- [ ] Add comprehensive error pages
- [ ] Test on different browsers
- [ ] Mobile responsiveness check
- [ ] Security audit
- [ ] Performance testing

---

## COMMON ISSUES & SOLUTIONS

### Issue: Cities autocomplete not working
**Solution**: Check jQuery UI is loaded correctly, verify API endpoint returns data

### Issue: No search results
**Solution**: Verify Amadeus API credentials, check if test cities exist (try "PAR" and "LON")

### Issue: Date validation fails
**Solution**: Ensure dates are in correct format (yyyy-MM-dd)

### Issue: Database connection fails
**Solution**: Check MySQL is running, verify connection string, ensure database exists

---

## FILE STRUCTURE SUMMARY

```
FlightSearchApp/
├── Controllers/
│   ├── HomeController.cs          ✓ Redirect to search
│   ├── FlightController.cs        ✓ Main logic
│   └── ApiController.cs            ✓ Autocomplete endpoint
├── Models/
│   ├── FlightSearchRequest.cs     ✓ API request model
│   ├── FlightOffer.cs              ✓ API response model
│   ├── SearchHistory.cs            ✓ EF entity
│   └── City.cs                     ✓ EF entity
├── ViewModels/
│   ├── FlightSearchViewModel.cs   ✓ Search form
│   └── FlightResultsViewModel.cs  ✓ Results page
├── Services/
│   ├── IFlightApiService.cs       ✓ Interface
│   └── AmadeusApiService.cs       ✓ Implementation
├── Data/
│   └── ApplicationDbContext.cs    ✓ EF context
├── Views/
│   ├── Shared/_Layout.cshtml      ✓ Master layout
│   ├── Flight/Search.cshtml       ✓ Search form
│   └── Flight/Results.cshtml      ✓ Results display
└── wwwroot/css/site.css           ✓ Custom styles
```

---

## SUCCESS CRITERIA

Your application is complete when:

✅ Users can search for flights (round-trip or one-way)
✅ City autocomplete works using Amadeus API
✅ Search results are displayed with proper formatting
✅ Results can be sorted by price, duration, stops
✅ Results can be filtered by stop type, time of day
✅ Users can modify search without losing data
✅ Search history is saved to MySQL database
✅ Application is responsive and works on mobile
✅ Error handling is in place
✅ Code is clean and well-organized

---

## FINAL NOTES

- Start with Phase 1 and work sequentially
- Test each phase before moving to the next
- Commit code after completing each phase
- Use the Amadeus test environment initially
- Read Amadeus API documentation for edge cases
- Keep API calls efficient (don't spam the API)
- Consider adding logging from the start
