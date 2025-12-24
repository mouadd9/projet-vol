using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MoteurDeRechercheDeVol.Services;

namespace MoteurDeRechercheDeVol.Controllers
{
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
                    value = $"{c.CityName} ({c.CityCode})", // Show Name + Code in input
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
}
