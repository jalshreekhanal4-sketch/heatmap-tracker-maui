using SQLite;

namespace HeatMapTracker.Models;

/// <summary>
/// A single GPS fix recorded by the application and persisted to SQLite.
/// </summary>
[Table("location_points")]
public class LocationPoint
{
    /// <summary>Surrogate key assigned by SQLite.</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Latitude in decimal degrees (WGS-84).</summary>
    [Indexed]
    public double Latitude { get; set; }

    /// <summary>Longitude in decimal degrees (WGS-84).</summary>
    [Indexed]
    public double Longitude { get; set; }

    /// <summary>Reported horizontal accuracy in metres, when the platform supplies one.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>UTC instant at which the fix was recorded.</summary>
    public DateTime RecordedUtc { get; set; }

    public override string ToString() =>
        $"{Latitude:F5}, {Longitude:F5} @ {RecordedUtc:u}";
}
