namespace SampleWebApi.Services;

public class WeatherService(HttpClient http)
{
    public async Task<string> GetCurrentWeatherAsync() =>
        await http.GetStringAsync(
            "https://api.open-meteo.com/v1/forecast?latitude=51.5&longitude=-0.12&current_weather=true");
}
