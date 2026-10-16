using HeatMapTracker.Models;
using HeatMapTracker.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace HeatMapTracker.Views;

/// <summary>
/// Single page of the application. Owns the map, drives the tracking service, and
/// redraws the heat overlay whenever a new fix is recorded.
/// </summary>
public partial class MainView : ContentPage
{
    /// <summary>Metres in one degree of latitude; longitude is scaled by the cosine of latitude.</summary>
    private const double MetresPerDegreeLatitude = 111_320.0;

    /// <summary>
    /// Smallest radius used when framing the map, so that a handful of readings taken a few
    /// metres apart do not zoom the viewport in past the point of being useful.
    /// </summary>
    private static readonly Distance MinimumSpanRadius = Distance.FromMeters(150);

    private readonly LocationDatabase _database;
    private readonly LocationTrackingService _tracker;

    public MainView(LocationDatabase database, LocationTrackingService tracker)
    {
        InitializeComponent();

        _database = database ?? throw new ArgumentNullException(nameof(database));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

        _tracker.PointRecorded += OnPointRecorded;
        _tracker.TrackingFailed += OnTrackingFailed;
    }

    /// <summary>Loads any previously stored points and centres the map when the page appears.</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshHeatMapAsync(recenter: true);
    }

    /// <summary>Toggles the tracking session and updates the button caption.</summary>
    private async void OnTrackingButtonClicked(object sender, EventArgs e)
    {
        if (_tracker.IsTracking)
        {
            _tracker.Stop();
            TrackingButton.Text = "Start Tracking";
            await UpdateStatusAsync("Tracking is stopped.");
            return;
        }

        await _tracker.StartAsync();

        if (_tracker.IsTracking)
        {
            TrackingButton.Text = "Stop Tracking";
            await UpdateStatusAsync("Tracking your location...");
        }
    }

    /// <summary>Deletes every stored point after confirmation and clears the overlay.</summary>
    private async void OnClearButtonClicked(object sender, EventArgs e)
    {
        bool confirmed = await DisplayAlertAsync(
            "Clear data",
            "Delete all recorded locations? This cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        await _database.ClearAllAsync();
        HeatMap.MapElements.Clear();
        await UpdateStatusAsync("All recorded locations deleted.");
    }

    /// <summary>Redraws the overlay each time the tracker stores a new fix.</summary>
    private async void OnPointRecorded(object? sender, LocationPoint point) =>
        await RefreshHeatMapAsync(recenter: false);

    /// <summary>Surfaces tracking errors to the user and resets the button.</summary>
    private async void OnTrackingFailed(object? sender, string message)
    {
        TrackingButton.Text = "Start Tracking";
        await DisplayAlertAsync("Tracking stopped", message, "OK");
    }

    /// <summary>
    /// Reads every stored point, rebuilds the aggregation grid, and renders it onto the map.
    /// </summary>
    /// <param name="recenter">When true, moves the viewport to the most recent fix.</param>
    private async Task RefreshHeatMapAsync(bool recenter)
    {
        List<LocationPoint> points = await _database.GetAllPointsAsync();
        IReadOnlyList<HeatCell> cells = HeatMapRenderer.BuildCells(points);

        HeatMapRenderer.Render(HeatMap, cells);

        if (recenter && points.Count > 0)
        {
            HeatMap.MoveToRegion(BuildRegion(points));
        }

        string state = _tracker.IsTracking ? "Tracking" : "Stopped";
        await UpdateStatusAsync(
            $"{state}. {points.Count} {Plural(points.Count, "point")} recorded " +
            $"across {cells.Count} {Plural(cells.Count, "area")}.");
    }

    /// <summary>Returns <paramref name="noun"/> pluralised for the supplied count.</summary>
    private static string Plural(int count, string noun) => count == 1 ? noun : noun + "s";

    /// <summary>
    /// Frames the viewport on the bounding box of every recorded reading. A fixed radius
    /// cannot serve both a short walk and a long drive: anything wide enough for the drive
    /// renders the walk as an unreadable speck, so the span is derived from the data.
    /// </summary>
    /// <param name="points">Every reading currently stored.</param>
    private static MapSpan BuildRegion(IReadOnlyList<LocationPoint> points)
    {
        double minimumLatitude = points.Min(p => p.Latitude);
        double maximumLatitude = points.Max(p => p.Latitude);
        double minimumLongitude = points.Min(p => p.Longitude);
        double maximumLongitude = points.Max(p => p.Longitude);

        var centre = new Location(
            (minimumLatitude + maximumLatitude) / 2.0,
            (minimumLongitude + maximumLongitude) / 2.0);

        // Longitude degrees shorten away from the equator, so the east-west extent is
        // scaled by the cosine of the latitude before the two axes are compared.
        double latitudeExtent = (maximumLatitude - minimumLatitude) * MetresPerDegreeLatitude;
        double longitudeExtent = (maximumLongitude - minimumLongitude) * MetresPerDegreeLatitude
                                 * Math.Cos(centre.Latitude * Math.PI / 180.0);

        // Half the longer axis frames the box; one cell radius of margin keeps the
        // outermost circles clear of the viewport edge.
        double radiusMetres = (Math.Max(latitudeExtent, longitudeExtent) / 2.0)
                              + HeatMapRenderer.CircleRadiusMeters;

        return MapSpan.FromCenterAndRadius(
            centre,
            Distance.FromMeters(Math.Max(radiusMetres, MinimumSpanRadius.Meters)));
    }

    /// <summary>Updates the status line on the main thread.</summary>
    private Task UpdateStatusAsync(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = message);
        return Task.CompletedTask;
    }
}
