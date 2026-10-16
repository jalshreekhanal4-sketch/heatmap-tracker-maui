using HeatMapTracker.Models;

namespace HeatMapTracker.Services;

/// <summary>
/// Polls the platform geolocation provider on a timer and persists each new fix.
/// Fixes closer than <see cref="MinimumDistanceMeters"/> to the previous one are
/// discarded, which keeps a stationary device from flooding the database.
/// </summary>
public class LocationTrackingService
{
    /// <summary>Interval between location requests.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>How long to wait for a single fix before giving up.</summary>
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Minimum movement, in metres, before a new fix is stored.</summary>
    public const double MinimumDistanceMeters = 15.0;

    private readonly LocationDatabase _database;
    private CancellationTokenSource? _cancellation;
    private Location? _lastStoredLocation;

    public LocationTrackingService(LocationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>Raised on the main thread after a fix has been stored.</summary>
    public event EventHandler<LocationPoint>? PointRecorded;

    /// <summary>Raised when tracking stops because of an error the user should see.</summary>
    public event EventHandler<string>? TrackingFailed;

    /// <summary><c>true</c> while the polling loop is running.</summary>
    public bool IsTracking => _cancellation is { IsCancellationRequested: false };

    /// <summary>
    /// Requests location permission and begins polling. Calling this while already
    /// tracking is a no-op.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsTracking)
        {
            return;
        }

        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (status != PermissionStatus.Granted)
        {
            TrackingFailed?.Invoke(this, "Location permission was denied, so tracking cannot start.");
            return;
        }

        _cancellation = new CancellationTokenSource();
        _ = PollLoopAsync(_cancellation.Token);
    }

    /// <summary>Stops the polling loop. Safe to call when not tracking.</summary>
    public void Stop()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>
    /// Repeatedly requests a fix until cancelled. Transient geolocation failures are
    /// swallowed so that a single bad reading does not end the session.
    /// </summary>
    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, FixTimeout);
                Location? location = await Geolocation.Default.GetLocationAsync(request, token);

                if (location is not null && ShouldStore(location))
                {
                    var point = new LocationPoint
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        AccuracyMeters = location.Accuracy,
                        RecordedUtc = DateTime.UtcNow,
                    };

                    await _database.AddPointAsync(point);
                    _lastStoredLocation = location;

                    MainThread.BeginInvokeOnMainThread(() => PointRecorded?.Invoke(this, point));
                }
            }
            catch (FeatureNotEnabledException)
            {
                TrackingFailed?.Invoke(this, "Location services are turned off on this device.");
                Stop();
                return;
            }
            catch (PermissionException)
            {
                TrackingFailed?.Invoke(this, "Location permission was revoked while tracking.");
                Stop();
                return;
            }
            catch (OperationCanceledException)
            {
                return; // Normal shutdown.
            }
            catch (Exception ex)
            {
                // A single failed fix should not end the session; log and keep going.
                System.Diagnostics.Debug.WriteLine($"Location fix failed: {ex.Message}");
            }

            try
            {
                await Task.Delay(PollInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the device has moved far enough from the previously
    /// stored fix to be worth recording.
    /// </summary>
    private bool ShouldStore(Location candidate)
    {
        if (_lastStoredLocation is null)
        {
            return true;
        }

        double kilometres = Location.CalculateDistance(
            _lastStoredLocation, candidate, DistanceUnits.Kilometers);

        return kilometres * 1000.0 >= MinimumDistanceMeters;
    }
}
