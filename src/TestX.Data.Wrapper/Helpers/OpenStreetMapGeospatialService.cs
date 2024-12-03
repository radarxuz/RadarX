using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class OpenStreetMapGeospatialService
{
    private static HttpClient _httpClient;

    // Static constructor to initialize the static fields
    static OpenStreetMapGeospatialService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TestXApp/1.0 (komrondeveloper@gmail.com)");
    }

    // Automatically checks if coordinates are in Uzbekistan and near a road
    public static async Task<bool> IsValid(double latitude, double longitude)
    {
        // Validate latitude and longitude ranges
        if (!IsValidLatitudeLongitude(latitude, longitude))
        {
            Console.WriteLine($"Invalid latitude or longitude: {latitude}, {longitude}");
            return false;
        }

        string apiUrl = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}&zoom=10&addressdetails=1";
        
        try
        {
            var response = await _httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode(); // Throw if not a success code.
            
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            await Task.Delay(1000);  // Adding delay to handle rate limits

            var address = json["address"];
            if (address != null)
            {
                string country = address["country_code"]?.ToString();
                if (!string.IsNullOrEmpty(country) && country.Equals("uz", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Coordinates are in Uzbekistan
                }
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request failed: {e.Message}");
            if (e.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine("Bad Request - check the parameters and URL.");
            }
        }
        catch (NullReferenceException e)
        {
            Console.WriteLine($"Null reference encountered: {e.Message}");
        }

        return false; // Default to false if conditions not met
    }

    // Validate latitude and longitude
    private static bool IsValidLatitudeLongitude(double latitude, double longitude)
    {
        return (latitude >= -90 && latitude <= 90) && (longitude >= -180 && longitude <= 180);
    }
}