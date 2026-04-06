namespace ThuyetMinhTuDong.Services
{
    /// <summary>
    /// Service for handling location permissions and geolocation operations.
    /// </summary>
    public class LocationService
    {
        public event EventHandler<LocationObtainedEventArgs> LocationObtained;
        public event EventHandler<PermissionDeniedEventArgs> PermissionDenied;

        /// <summary>
        /// Checks and requests location permission from the user.
        /// </summary>
        public async Task<bool> CheckAndRequestPermissionAsync()
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
                    PermissionDenied?.Invoke(this, new PermissionDeniedEventArgs
                    {
                        Message = "Vị trí đã bị từ chối. Không thể hiển thị vị trí của bạn."
                    });
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the user's current location with fallback to last known location.
        /// Uses Medium accuracy with 10-second timeout.
        /// </summary>
        public async Task<Location> GetCurrentLocationAsync()
        {
            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync() 
                    ?? await Geolocation.GetLocationAsync(new GeolocationRequest(
                        GeolocationAccuracy.Medium, 
                        TimeSpan.FromSeconds(10)));

                if (location != null)
                {
                    LocationObtained?.Invoke(this, new LocationObtainedEventArgs { Location = location });
                    return location;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>
        /// Creates a MapSpan centered at the given location with 1km radius.
        /// </summary>
        public Microsoft.Maui.Maps.MapSpan CreateMapSpan(Location location)
        {
            if (location == null)
                return null;

            return Microsoft.Maui.Maps.MapSpan.FromCenterAndRadius(
                new Microsoft.Maui.Devices.Sensors.Location(location.Latitude, location.Longitude),
                Microsoft.Maui.Maps.Distance.FromKilometers(1));
        }

        public class LocationObtainedEventArgs : EventArgs
        {
            public Location Location { get; set; }
        }

        public class PermissionDeniedEventArgs : EventArgs
        {
            public string Message { get; set; }
        }
    }
}
