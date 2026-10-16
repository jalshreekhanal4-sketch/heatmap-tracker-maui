using HeatMapTracker.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;   // Disambiguate from Microsoft.Maui.ApplicationModel.Map.

namespace HeatMapTracker.Services;

/// <summary>
/// Extends the stock .NET MAUI <see cref="Map"/> control, which has no native heat map,
/// by aggregating recorded fixes into a fixed-size grid and drawing one translucent
/// <see cref="Circle"/> per occupied cell. Cell colour and opacity are driven by how many
/// fixes fell inside the cell, which produces the familiar cold-to-hot appearance without
/// requiring a custom platform renderer.
/// </summary>
public static class HeatMapRenderer
{
    /// <summary>Approximate metres per degree of latitude. Constant enough for grid snapping.</summary>
    private const double MetersPerDegreeLatitude = 111_320.0;

    /// <summary>Edge length of one aggregation cell, in metres.</summary>
    public const double CellSizeMeters = 50.0;

    /// <summary>Radius of each drawn circle, in metres. Slightly larger than a cell so neighbours blend.</summary>
    public const double CircleRadiusMeters = 40.0;

    /// <summary>Opacity floor so that a single visit is still visible.</summary>
    private const double MinimumAlpha = 0.25;

    /// <summary>Opacity ceiling so that the busiest cells do not hide the map beneath.</summary>
    private const double MaximumAlpha = 0.70;

    /// <summary>
    /// Groups recorded points into grid cells. Longitude spacing is scaled by the cosine of
    /// the latitude so that cells stay roughly square as the map moves away from the equator.
    /// </summary>
    /// <param name="points">Recorded fixes, in any order.</param>
    /// <returns>One <see cref="HeatCell"/> per occupied cell, with its visit count.</returns>
    public static IReadOnlyList<HeatCell> BuildCells(IEnumerable<LocationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double latitudeStep = CellSizeMeters / MetersPerDegreeLatitude;
        var buckets = new Dictionary<(long Row, long Column), (double LatSum, double LonSum, int Count)>();

        foreach (LocationPoint point in points)
        {
            // Longitude degrees shrink as |latitude| grows, so widen the longitude step to compensate.
            double latitudeRadians = point.Latitude * Math.PI / 180.0;
            double cosine = Math.Max(Math.Cos(latitudeRadians), 0.000001); // Guard against the poles.
            double longitudeStep = latitudeStep / cosine;

            long row = (long)Math.Floor(point.Latitude / latitudeStep);
            long column = (long)Math.Floor(point.Longitude / longitudeStep);
            var key = (row, column);

            if (buckets.TryGetValue(key, out var existing))
            {
                buckets[key] = (existing.LatSum + point.Latitude,
                                existing.LonSum + point.Longitude,
                                existing.Count + 1);
            }
            else
            {
                buckets[key] = (point.Latitude, point.Longitude, 1);
            }
        }

        // Use the mean position of the points in a cell rather than the cell corner,
        // which keeps circles centred on the path the user actually walked.
        return buckets.Values
                      .Select(b => new HeatCell(b.LatSum / b.Count, b.LonSum / b.Count, b.Count))
                      .ToList();
    }

    /// <summary>
    /// Replaces the map's elements with circles representing the supplied cells.
    /// </summary>
    /// <param name="map">The map whose <see cref="Map.MapElements"/> collection will be rebuilt.</param>
    /// <param name="cells">Aggregated cells produced by <see cref="BuildCells"/>.</param>
    public static void Render(Map map, IReadOnlyList<HeatCell> cells)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);

        map.MapElements.Clear();

        if (cells.Count == 0)
        {
            return;
        }

        // Normalise against the busiest cell so the gradient always uses its full range.
        int maxCount = cells.Max(c => c.Count);

        foreach (HeatCell cell in cells)
        {
            double intensity = maxCount <= 1 ? 1.0 : (double)(cell.Count - 1) / (maxCount - 1);
            Color fill = GetHeatColor(intensity);

            var circle = new Circle
            {
                Center = new Location(cell.CenterLatitude, cell.CenterLongitude),
                Radius = Distance.FromMeters(CircleRadiusMeters),
                FillColor = fill,
                StrokeColor = fill.WithAlpha(0.9f),
                StrokeWidth = 1,
            };

            map.MapElements.Add(circle);
        }
    }

    /// <summary>
    /// Maps a normalised intensity onto a cold-to-hot gradient running
    /// blue, cyan, green, yellow, red. Alpha rises with intensity so that
    /// frequently visited areas read as solid and rare ones as a faint wash.
    /// </summary>
    /// <param name="intensity">Normalised density in the range 0.0 to 1.0.</param>
    public static Color GetHeatColor(double intensity)
    {
        double t = Math.Clamp(intensity, 0.0, 1.0);

        // Four equal segments between the five gradient stops.
        (double R, double G, double B)[] stops =
        [
            (0.00, 0.30, 1.00), // blue
            (0.00, 0.90, 0.90), // cyan
            (0.20, 0.85, 0.20), // green
            (1.00, 0.85, 0.00), // yellow
            (1.00, 0.10, 0.10), // red
        ];

        double scaled = t * (stops.Length - 1);
        int index = Math.Min((int)scaled, stops.Length - 2);
        double local = scaled - index;

        double r = Lerp(stops[index].R, stops[index + 1].R, local);
        double g = Lerp(stops[index].G, stops[index + 1].G, local);
        double b = Lerp(stops[index].B, stops[index + 1].B, local);
        double alpha = Lerp(MinimumAlpha, MaximumAlpha, t);

        return Color.FromRgba((float)r, (float)g, (float)b, (float)alpha);
    }

    /// <summary>Linear interpolation between <paramref name="from"/> and <paramref name="to"/>.</summary>
    private static double Lerp(double from, double to, double amount) =>
        from + ((to - from) * amount);
}
