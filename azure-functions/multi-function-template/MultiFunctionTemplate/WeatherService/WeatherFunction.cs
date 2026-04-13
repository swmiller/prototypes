using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace WeatherService;

public class WeatherFunction
{
    private const string RoutePrefix = "weather";
    private readonly ILogger<WeatherFunction> _logger;
    private static readonly Random _random = new();

    public WeatherFunction(ILogger<WeatherFunction> logger)
    {
        _logger = logger;
    }

    [Function("GetCurrentWeather")]
    public IActionResult GetCurrentWeather(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"{RoutePrefix}/current/{{city}}")] HttpRequest req, 
        string city)
    {
        _logger.LogInformation($"Getting current weather for {city}");

        var weather = new
        {
            City = city,
            Temperature = _random.Next(-10, 40),
            FeelsLike = _random.Next(-10, 40),
            Condition = GetRandomCondition(),
            Humidity = _random.Next(30, 90),
            WindSpeed = _random.Next(0, 50),
            Timestamp = DateTime.UtcNow
        };

        return new OkObjectResult(weather);
    }

    [Function("GetWeatherForecast")]
    public IActionResult GetWeatherForecast(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"{RoutePrefix}/forecast/{{city}}")] HttpRequest req,
        string city)
    {
        _logger.LogInformation($"Getting 5-day forecast for {city}");

        var forecast = Enumerable.Range(1, 5).Select(day => new
        {
            Date = DateTime.UtcNow.AddDays(day).ToString("yyyy-MM-dd"),
            HighTemp = _random.Next(15, 35),
            LowTemp = _random.Next(-5, 15),
            Condition = GetRandomCondition(),
            ChanceOfRain = _random.Next(0, 100)
        });

        return new OkObjectResult(new
        {
            City = city,
            Forecast = forecast
        });
    }

    [Function("GetWeatherByCoordinates")]
    public IActionResult GetWeatherByCoordinates(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"{RoutePrefix}/coordinates")] HttpRequest req)
    {
        var lat = req.Query["lat"].ToString();
        var lon = req.Query["lon"].ToString();

        _logger.LogInformation($"Getting weather for coordinates: {lat}, {lon}");

        if (string.IsNullOrEmpty(lat) || string.IsNullOrEmpty(lon))
        {
            return new BadRequestObjectResult("Please provide both 'lat' and 'lon' query parameters");
        }

        var weather = new
        {
            Latitude = lat,
            Longitude = lon,
            Temperature = _random.Next(-10, 40),
            Condition = GetRandomCondition(),
            Humidity = _random.Next(30, 90),
            WindSpeed = _random.Next(0, 50),
            Timestamp = DateTime.UtcNow
        };

        return new OkObjectResult(weather);
    }

    [Function("GetAllCities")]
    public IActionResult GetAllCities(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = $"{RoutePrefix}/cities")] HttpRequest req)
    {
        _logger.LogInformation("Getting list of available cities");

        var cities = new[]
        {
            "Seattle", "New York", "London", "Tokyo", "Sydney",
            "Paris", "Berlin", "Toronto", "Singapore", "Dubai"
        };

        return new OkObjectResult(cities);
    }

    private static string GetRandomCondition()
    {
        var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Rainy", "Stormy", "Snowy", "Foggy", "Windy" };
        return conditions[_random.Next(conditions.Length)];
    }
}