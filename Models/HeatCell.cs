namespace HeatMapTracker.Models;

/// <summary>
/// One cell of the aggregation grid used to build the heat map. Recorded points are
/// snapped to a fixed-size grid so that repeated visits to the same place accumulate
/// into a single weighted cell rather than drawing hundreds of overlapping circles.
/// </summary>
/// <param name="CenterLatitude">Latitude of the cell centre in decimal degrees.</param>
/// <param name="CenterLongitude">Longitude of the cell centre in decimal degrees.</param>
/// <param name="Count">Number of recorded fixes that fell inside this cell.</param>
public readonly record struct HeatCell(
    double CenterLatitude,
    double CenterLongitude,
    int Count);
