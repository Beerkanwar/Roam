using Microsoft.Maui.Devices.Sensors;

namespace Roam.App.Services;

public class LocationService
{
    public async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                // Permission denied, handle accordingly
                return null;
            }

            // Get cached location first, then request real location if needed
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location == null)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                location = await Geolocation.Default.GetLocationAsync(request);
            }

            return location;
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error getting location: {ex.Message}");
            return null;
        }
    }
}
