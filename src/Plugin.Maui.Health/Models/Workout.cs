using Plugin.Maui.Health.Enums;

namespace Plugin.Maui.Health.Models;

public sealed record Workout
{
	public WorkoutType WorkoutType { get; }
	public string Source { get; }
	public DateTime? From { get; }
	public DateTime? Until { get; }
	public double DurationInSeconds { get; }
	public double? EnergyBurnedInCalorie { get; }
	public double? TotalDistanceInMeter { get; }

	/// <summary>
	/// Gets the ordered list of GPS coordinates that make up the workout route.
	/// Returns <see langword="null"/> when GPS data was not recorded or is unavailable for the platform.
	/// </summary>
	public IReadOnlyList<WorkoutCoordinate>? Route { get; }

	public Workout(WorkoutType workoutType, DateTime? from, DateTime? until, double durationInSeconds,
		double? energyBurnedInCalorie, double? totalDistanceInMeter, string source,
		IReadOnlyList<WorkoutCoordinate>? route = null)
	{
		WorkoutType = workoutType;
		Source = source;
		From = from;
		Until = until;
		DurationInSeconds = durationInSeconds;
		EnergyBurnedInCalorie = energyBurnedInCalorie;
		TotalDistanceInMeter = totalDistanceInMeter;
		Route = route;
	}
}
