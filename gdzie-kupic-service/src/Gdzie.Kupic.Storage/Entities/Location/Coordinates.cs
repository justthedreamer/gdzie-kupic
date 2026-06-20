namespace Gdzie.Kupic.Storage.Entities.Location;

using NetTopologySuite.Geometries;

/// <summary>
/// Represents a geographic point as latitude/longitude.
/// Stored in PostgreSQL as geography(Point,4326) via a value converter.
/// </summary>
public sealed record Coordinates(double Latitude, double Longitude)
{
    public static Coordinates FromPoint(Point point) => new(point.Y, point.X);

    public Point ToPoint() => new(this.Longitude, this.Latitude) { SRID = 4326 };
}