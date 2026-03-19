namespace Plugin.Maui.Health.Models;

/// <summary>
/// Represents a single GPS coordinate point in a workout route.
/// </summary>
public sealed record WorkoutCoordinate
{
	/// <summary>
	/// Gets the timestamp when this location was recorded.
	/// </summary>
	public DateTime Timestamp { get; }

	/// <summary>
	/// Gets the latitude in decimal degrees (WGS84).
	/// </summary>
	public double Latitude { get; }

	/// <summary>
	/// Gets the longitude in decimal degrees (WGS84).
	/// </summary>
	public double Longitude { get; }

	/// <summary>
	/// Gets the altitude in meters above sea level, or <see langword="null"/> if not available.
	/// </summary>
	public double? Altitude { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="WorkoutCoordinate"/> record.
	/// </summary>
	/// <param name="timestamp">The timestamp when the location was recorded.</param>
	/// <param name="latitude">The latitude in decimal degrees.</param>
	/// <param name="longitude">The longitude in decimal degrees.</param>
	/// <param name="altitude">The altitude in meters, or <see langword="null"/> if not available.</param>
	public WorkoutCoordinate(DateTime timestamp, double latitude, double longitude, double? altitude = null)
	{
		Timestamp = timestamp;
		Latitude = latitude;
		Longitude = longitude;
		Altitude = altitude;
	}
}
