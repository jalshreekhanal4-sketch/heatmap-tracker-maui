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
    /// <summary>Initial map span when no recorded points exist yet.</summary>
    private static readonly Distance DefaultSpanRadius = Distance.FromKilometers(2);

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
            LocationPoint newest = points[^1];
            HeatMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(newest.Latitude, newest.Longitude), DefaultSpanRadius));
        }

        string state = _tracker.IsTracking ? "Tracking" : "Stopped";
        await UpdateStatusAsync(
            $"{state}. {points.Count} {Plural(points.Count, "point")} recorded " +
            $"across {cells.Count} {Plural(cells.Count, "area")}.");
    }

    /// <summary>Returns <paramref name="noun"/> pluralised for the supplied count.</summary>
    private static string Plural(int count, string noun) => count == 1 ? noun : noun + "s";

    /// <summary>Updates the status line on the main thread.</summary>
    private Task UpdateStatusAsync(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = message);
        return Task.CompletedTask;
    }
}
