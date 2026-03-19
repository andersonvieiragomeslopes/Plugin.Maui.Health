using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Permission;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Records.Metadata;
using AndroidX.Health.Connect.Client.Request;
using AndroidX.Health.Connect.Client.Time;
using AndroidX.Health.Connect.Client.Units;
using Java.Time;
using Microsoft.Maui.ApplicationModel;
using Plugin.Maui.Health.Enums;
using Plugin.Maui.Health.Exceptions;
using Plugin.Maui.Health.Models;

namespace Plugin.Maui.Health;

partial class HealthDataProviderImplementation : IHealth
{
	HealthConnectClient? _healthConnectClient;

	readonly SemaphoreSlim semaphore = new(1, 1);

	/// <summary>
	/// Maps each <see cref="HealthParameter"/> to the corresponding Android Health Connect record type.
	/// Parameters marked with a comment are not natively supported by Health Connect and will throw
	/// <see cref="HealthException"/> at runtime.
	/// </summary>
	static readonly Dictionary<HealthParameter, Type> healthParameterToRecordType = new()
	{
		{ HealthParameter.StepCount,                    typeof(StepsRecord) },
		{ HealthParameter.HeartRate,                    typeof(HeartRateRecord) },
		{ HealthParameter.RestingHeartRate,             typeof(RestingHeartRateRecord) },
		{ HealthParameter.BodyMass,                     typeof(WeightRecord) },
		{ HealthParameter.Height,                       typeof(HeightRecord) },
		{ HealthParameter.BodyFatPercentage,            typeof(BodyFatRecord) },
		{ HealthParameter.BodyMassIndex,                typeof(BodyMassIndexRecord) },
		{ HealthParameter.LeanBodyMass,                 typeof(LeanBodyMassRecord) },
		{ HealthParameter.WaistCircumference,           typeof(WaistCircumferenceRecord) },
		{ HealthParameter.ActiveEnergyBurned,           typeof(ActiveCaloriesBurnedRecord) },
		{ HealthParameter.BasalEnergyBurned,            typeof(BasalMetabolicRateRecord) },
		{ HealthParameter.OxygenSaturation,             typeof(OxygenSaturationRecord) },
		{ HealthParameter.BloodGlucose,                 typeof(BloodGlucoseRecord) },
		{ HealthParameter.BloodPressureSystolic,        typeof(BloodPressureRecord) },
		{ HealthParameter.BloodPressureDiastolic,       typeof(BloodPressureRecord) },
		{ HealthParameter.BodyTemperature,              typeof(BodyTemperatureRecord) },
		{ HealthParameter.BasalBodyTemperature,         typeof(BasalBodyTemperatureRecord) },
		{ HealthParameter.RespiratoryRate,              typeof(RespiratoryRateRecord) },
		{ HealthParameter.VO2Max,                       typeof(Vo2MaxRecord) },
		{ HealthParameter.FlightsClimbed,               typeof(FloorsClimbedRecord) },
		{ HealthParameter.DistanceWalkingRunning,       typeof(DistanceRecord) },
		{ HealthParameter.DistanceCycling,              typeof(DistanceRecord) },
		{ HealthParameter.DistanceSwimming,             typeof(DistanceRecord) },
		{ HealthParameter.DistanceWheelchair,           typeof(DistanceRecord) },
		{ HealthParameter.ExerciseTime,                 typeof(ExerciseSessionRecord) },
		{ HealthParameter.HeartRateVariabilitySdnn,     typeof(HeartRateVariabilityRmssdRecord) },
		{ HealthParameter.SwimmingStrokeCount,          typeof(SwimmingStrokesRecord) },
		{ HealthParameter.DietaryCalcium,               typeof(NutritionRecord) },
		{ HealthParameter.DietaryCarbohydrates,         typeof(NutritionRecord) },
		{ HealthParameter.DietaryCholesterol,           typeof(NutritionRecord) },
		{ HealthParameter.DietaryEnergyConsumed,        typeof(NutritionRecord) },
		{ HealthParameter.DietaryFatMonounsaturated,    typeof(NutritionRecord) },
		{ HealthParameter.DietaryFatPolyunsaturated,    typeof(NutritionRecord) },
		{ HealthParameter.DietaryFatSaturated,          typeof(NutritionRecord) },
		{ HealthParameter.DietaryFatTotal,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryFiber,                 typeof(NutritionRecord) },
		{ HealthParameter.DietaryFolate,                typeof(NutritionRecord) },
		{ HealthParameter.DietaryIron,                  typeof(NutritionRecord) },
		{ HealthParameter.DietaryMagnesium,             typeof(NutritionRecord) },
		{ HealthParameter.DietaryPhosphorus,            typeof(NutritionRecord) },
		{ HealthParameter.DietaryPotassium,             typeof(NutritionRecord) },
		{ HealthParameter.DietaryProtein,               typeof(NutritionRecord) },
		{ HealthParameter.DietarySodium,                typeof(NutritionRecord) },
		{ HealthParameter.DietarySugar,                 typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminA,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminB6,             typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminB12,            typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminC,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminD,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminE,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryVitaminK,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryZinc,                  typeof(NutritionRecord) },
		{ HealthParameter.DietaryWater,                 typeof(HydrationRecord) },
		{ HealthParameter.DietaryCaffeine,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryBiotin,                typeof(NutritionRecord) },
		{ HealthParameter.DietaryChloride,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryChromium,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryCopper,                typeof(NutritionRecord) },
		{ HealthParameter.DietaryIodine,                typeof(NutritionRecord) },
		{ HealthParameter.DietaryManganese,             typeof(NutritionRecord) },
		{ HealthParameter.DietaryMolybdenum,            typeof(NutritionRecord) },
		{ HealthParameter.DietaryNiacin,                typeof(NutritionRecord) },
		{ HealthParameter.DietaryPantothenicAcid,       typeof(NutritionRecord) },
		{ HealthParameter.DietaryRiboflavin,            typeof(NutritionRecord) },
		{ HealthParameter.DietarySelenium,              typeof(NutritionRecord) },
		{ HealthParameter.DietaryThiamin,               typeof(NutritionRecord) },
	};

	/// <summary>
	/// Maps <see cref="WorkoutType"/> to the Health Connect integer exercise type constants defined on
	/// <see cref="ExerciseSessionRecord"/>.
	/// </summary>
	static readonly Dictionary<WorkoutType, int> workoutTypeToExerciseType = new()
	{
		{ WorkoutType.Running,                      ExerciseSessionRecord.ExerciseTypeRunning },
		{ WorkoutType.Walking,                      ExerciseSessionRecord.ExerciseTypeWalking },
		{ WorkoutType.Cycling,                      ExerciseSessionRecord.ExerciseTypeBiking },
		{ WorkoutType.Swimming,                     ExerciseSessionRecord.ExerciseTypeSwimmingOpenWater },
		{ WorkoutType.Hiking,                       ExerciseSessionRecord.ExerciseTypeHiking },
		{ WorkoutType.Yoga,                         ExerciseSessionRecord.ExerciseTypeYoga },
		{ WorkoutType.HighIntensityIntervalTraining, ExerciseSessionRecord.ExerciseTypeHighIntensityIntervalTraining },
		{ WorkoutType.TraditionalStrengthTraining,   ExerciseSessionRecord.ExerciseTypeStrengthTraining },
		{ WorkoutType.FunctionalStrengthTraining,    ExerciseSessionRecord.ExerciseTypeStrengthTraining },
		{ WorkoutType.CrossTraining,                ExerciseSessionRecord.ExerciseTypeCrossTraining },
		{ WorkoutType.Elliptical,                   ExerciseSessionRecord.ExerciseTypeElliptical },
		{ WorkoutType.Rowing,                       ExerciseSessionRecord.ExerciseTypeRowing },
		{ WorkoutType.StairClimbing,                ExerciseSessionRecord.ExerciseTypeStairClimbing },
		{ WorkoutType.Stairs,                       ExerciseSessionRecord.ExerciseTypeStairClimbing },
		{ WorkoutType.Dance,                        ExerciseSessionRecord.ExerciseTypeDancing },
		{ WorkoutType.DanceInspiredTraining,        ExerciseSessionRecord.ExerciseTypeDancing },
		{ WorkoutType.Soccer,                       ExerciseSessionRecord.ExerciseTypeFootball },
		{ WorkoutType.Basketball,                   ExerciseSessionRecord.ExerciseTypeBasketball },
		{ WorkoutType.Baseball,                     ExerciseSessionRecord.ExerciseTypeBaseball },
		{ WorkoutType.Tennis,                       ExerciseSessionRecord.ExerciseTypeTennis },
		{ WorkoutType.Volleyball,                   ExerciseSessionRecord.ExerciseTypeVolleyball },
		{ WorkoutType.Golf,                         ExerciseSessionRecord.ExerciseTypeGolf },
		{ WorkoutType.Boxing,                       ExerciseSessionRecord.ExerciseTypeBoxing },
		{ WorkoutType.MartialArts,                  ExerciseSessionRecord.ExerciseTypeMartialArts },
		{ WorkoutType.Badminton,                    ExerciseSessionRecord.ExerciseTypeBadminton },
		{ WorkoutType.Squash,                       ExerciseSessionRecord.ExerciseTypeSquash },
		{ WorkoutType.TableTennis,                  ExerciseSessionRecord.ExerciseTypeTableTennis },
		{ WorkoutType.Climbing,                     ExerciseSessionRecord.ExerciseTypeRockClimbing },
		{ WorkoutType.Snowboarding,                 ExerciseSessionRecord.ExerciseTypeSnowboarding },
		{ WorkoutType.DownhillSkiing,               ExerciseSessionRecord.ExerciseTypeSkiing },
		{ WorkoutType.CrossCountrySkiing,           ExerciseSessionRecord.ExerciseTypeSkiing },
		{ WorkoutType.Pilates,                      ExerciseSessionRecord.ExerciseTypePilates },
		{ WorkoutType.Gymnastics,                   ExerciseSessionRecord.ExerciseTypeGymnastics },
		{ WorkoutType.MindAndBody,                  ExerciseSessionRecord.ExerciseTypeMindfulness },
		{ WorkoutType.JumpRope,                     ExerciseSessionRecord.ExerciseTypeJumpRope },
		{ WorkoutType.Kickboxing,                   ExerciseSessionRecord.ExerciseTypeKickboxing },
		{ WorkoutType.Barre,                        ExerciseSessionRecord.ExerciseTypeBarre },
		{ WorkoutType.CoreTraining,                 ExerciseSessionRecord.ExerciseTypeExerciseClass },
		{ WorkoutType.WaterFitness,                 ExerciseSessionRecord.ExerciseTypeWaterPolo },
		{ WorkoutType.SurfingSports,                ExerciseSessionRecord.ExerciseTypeSurfing },
		{ WorkoutType.PaddleSports,                 ExerciseSessionRecord.ExerciseTypePaddling },
		{ WorkoutType.Sailing,                      ExerciseSessionRecord.ExerciseTypeSailing },
		{ WorkoutType.Handball,                     ExerciseSessionRecord.ExerciseTypeHandball },
		{ WorkoutType.Rugby,                        ExerciseSessionRecord.ExerciseTypeRugby },
		{ WorkoutType.Hockey,                       ExerciseSessionRecord.ExerciseTypeIceHockey },
		{ WorkoutType.AmericanFootball,             ExerciseSessionRecord.ExerciseTypeAmericanFootball },
		{ WorkoutType.AustralianFootball,           ExerciseSessionRecord.ExerciseTypeAustralianFootball },
		{ WorkoutType.Cricket,                      ExerciseSessionRecord.ExerciseTypeCricket },
		{ WorkoutType.Lacrosse,                     ExerciseSessionRecord.ExerciseTypeLacrosse },
		{ WorkoutType.Softball,                     ExerciseSessionRecord.ExerciseTypeSoftball },
		{ WorkoutType.Racquetball,                  ExerciseSessionRecord.ExerciseTypeRacquetball },
		{ WorkoutType.TrackAndField,                ExerciseSessionRecord.ExerciseTypeRunning },
		{ WorkoutType.SwimBikeRun,                  ExerciseSessionRecord.ExerciseTypeSwimmingOpenWater },
		{ WorkoutType.Other,                        ExerciseSessionRecord.ExerciseTypeOther },
	};

	/// <summary>Reverse mapping: Health Connect exercise-type int → <see cref="WorkoutType"/>.</summary>
	static readonly Dictionary<int, WorkoutType> exerciseTypeToWorkoutType =
		workoutTypeToExerciseType
			.GroupBy(kv => kv.Value)
			.ToDictionary(g => g.Key, g => g.First().Key);

	HealthConnectClient GetClient()
	{
		_healthConnectClient ??= HealthConnectClient.GetOrCreate(Platform.AppContext);
		return _healthConnectClient;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the Android Health Connect SDK is installed and available
	/// on the current device.
	/// </summary>
	public bool IsSupported =>
		HealthConnectClient.GetSdkStatus(Platform.AppContext) == HealthConnectClient.SdkAvailable;

	// ────────────────────────────────────────────────────────────────────────────
	// Permissions
	// ────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Checks whether the required Health Connect permissions have already been granted for
	/// <paramref name="healthParameter"/>.  To actually request permissions from the user the
	/// host application must launch the Health Connect permission activity using
	/// <c>HealthConnectClient.PermissionController.CreateRequestPermissionResultContract()</c>.
	/// </summary>
	public async Task<bool> CheckPermissionAsync(HealthParameter healthParameter, PermissionType permissionType)
	{
		if (!IsSupported)
			throw new HealthException("Android Health Connect is not available on this device. Ensure Health Connect is installed and the SDK is available.");

		if (!healthParameterToRecordType.TryGetValue(healthParameter, out var recordType))
			throw new HealthException($"{healthParameter} is not supported on Android Health Connect.");

		try
		{
			var client = GetClient();
			var granted = await client.PermissionController.GetGrantedPermissions();

			if (permissionType.HasFlag(PermissionType.Read))
			{
				var readPermission = HealthPermission.GetReadPermission(Java.Lang.Class.FromType(recordType));
				if (!granted.Contains(readPermission))
					throw new HealthException($"Read permission for {healthParameter} has not been granted. Request it using HealthConnectClient.PermissionController.");
			}

			if (permissionType.HasFlag(PermissionType.Write))
			{
				var writePermission = HealthPermission.GetWritePermission(Java.Lang.Class.FromType(recordType));
				if (!granted.Contains(writePermission))
					throw new HealthException($"Write permission for {healthParameter} has not been granted. Request it using HealthConnectClient.PermissionController.");
			}

			return true;
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
	}

	// ────────────────────────────────────────────────────────────────────────────
	// Helpers
	// ────────────────────────────────────────────────────────────────────────────

	static Instant ToInstant(DateTime dateTime) =>
		Instant.OfEpochMilli(new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds());

	static TimeRangeFilter BuildTimeRange(DateTime from, DateTime until) =>
		TimeRangeFilter.Between(ToInstant(from), ToInstant(until));

	static DateTime FromInstant(Instant instant) =>
		DateTimeOffset.FromUnixTimeMilliseconds(instant.ToEpochMilli()).UtcDateTime;

	/// <summary>
	/// Extracts the numeric value from a Health Connect record that is relevant for the requested
	/// <paramref name="healthParameter"/>.  Returns <see langword="null"/> for record types where
	/// a single scalar cannot be determined (e.g. blood pressure — query each component separately).
	/// </summary>
	static double? ExtractValue(Record record, HealthParameter healthParameter)
	{
		return record switch
		{
			StepsRecord r                       => r.Count,
			WeightRecord r                      => r.Weight.InKilograms,
			HeightRecord r                      => r.Height.InMeters,
			BodyFatRecord r                     => r.Percentage.Value,
			LeanBodyMassRecord r                => r.Mass.InKilograms,
			BoneMassRecord r                    => r.Mass.InKilograms,
		BodyMassIndexRecord r               => r.Bmi,
			WaistCircumferenceRecord r          => r.Circumference.InMeters,
			ActiveCaloriesBurnedRecord r        => r.Energy.InCalories,
			BasalMetabolicRateRecord r          => r.BasalMetabolicRate.InWatts,
			OxygenSaturationRecord r            => r.Percentage.Value,
			BloodGlucoseRecord r                => r.Level.InMillimolesPerLiter,
			BloodPressureRecord r when healthParameter == HealthParameter.BloodPressureSystolic
											    => r.Systolic.InMillimetersOfMercury,
			BloodPressureRecord r               => r.Diastolic.InMillimetersOfMercury,
			BodyTemperatureRecord r             => r.Temperature.InCelsius,
			BasalBodyTemperatureRecord r        => r.Temperature.InCelsius,
			RespiratoryRateRecord r             => r.Rate,
			Vo2MaxRecord r                      => r.Vo2MillilitersPerMinuteKilogram,
			FloorsClimbedRecord r               => r.Floors,
			DistanceRecord r                    => r.Distance.InMeters,
			HeartRateRecord r                   => r.Samples?.Count > 0 ? r.Samples[0].BeatsPerMinute : (double?)null,
			RestingHeartRateRecord r            => r.BeatsPerMinute,
			HeartRateVariabilityRmssdRecord r   => r.HeartRateVariabilityMillis,
			SwimmingStrokesRecord r             => r.Count,
			HydrationRecord r                   => r.Volume.InMilliliters,
			ExerciseSessionRecord r             => (r.EndTime.ToEpochMilli() - r.StartTime.ToEpochMilli()) / 1000.0,
			NutritionRecord r                   => ExtractNutritionValue(r, healthParameter),
			_                                   => null,
		};
	}

	static double? ExtractNutritionValue(NutritionRecord r, HealthParameter hp) => hp switch
	{
		HealthParameter.DietaryEnergyConsumed        => r.Energy?.InCalories,
		HealthParameter.DietaryProtein               => r.Protein?.InGrams,
		HealthParameter.DietaryCarbohydrates         => r.TotalCarbohydrate?.InGrams,
		HealthParameter.DietaryFatTotal              => r.TotalFat?.InGrams,
		HealthParameter.DietaryFatSaturated          => r.SaturatedFat?.InGrams,
		HealthParameter.DietaryFatPolyunsaturated    => r.PolyunsaturatedFat?.InGrams,
		HealthParameter.DietaryFatMonounsaturated    => r.MonounsaturatedFat?.InGrams,
		HealthParameter.DietaryFiber                 => r.DietaryFiber?.InGrams,
		HealthParameter.DietarySugar                 => r.Sugar?.InGrams,
		HealthParameter.DietaryCholesterol           => r.Cholesterol?.InGrams,
		HealthParameter.DietarySodium                => r.Sodium?.InGrams,
		HealthParameter.DietaryPotassium             => r.Potassium?.InGrams,
		HealthParameter.DietaryCalcium               => r.Calcium?.InGrams,
		HealthParameter.DietaryIron                  => r.Iron?.InGrams,
		HealthParameter.DietaryVitaminA              => r.VitaminA?.InGrams,
		HealthParameter.DietaryVitaminB6             => r.VitaminB6?.InGrams,
		HealthParameter.DietaryVitaminB12            => r.VitaminB12?.InGrams,
		HealthParameter.DietaryVitaminC              => r.VitaminC?.InGrams,
		HealthParameter.DietaryVitaminD              => r.VitaminD?.InGrams,
		HealthParameter.DietaryVitaminE              => r.VitaminE?.InGrams,
		HealthParameter.DietaryVitaminK              => r.VitaminK?.InGrams,
		HealthParameter.DietaryZinc                  => r.Zinc?.InGrams,
		HealthParameter.DietaryMagnesium             => r.Magnesium?.InGrams,
		HealthParameter.DietaryPhosphorus            => r.Phosphorus?.InGrams,
		HealthParameter.DietaryFolate                => r.Folate?.InGrams,
		HealthParameter.DietaryBiotin                => r.Biotin?.InGrams,
		HealthParameter.DietaryChloride              => r.Chloride?.InGrams,
		HealthParameter.DietaryChromium              => r.Chromium?.InGrams,
		HealthParameter.DietaryCopper                => r.Copper?.InGrams,
		HealthParameter.DietaryIodine                => r.Iodine?.InGrams,
		HealthParameter.DietaryManganese             => r.Manganese?.InGrams,
		HealthParameter.DietaryMolybdenum            => r.Molybdenum?.InGrams,
		HealthParameter.DietaryNiacin                => r.Niacin?.InGrams,
		HealthParameter.DietaryPantothenicAcid       => r.PantothenicAcid?.InGrams,
		HealthParameter.DietaryRiboflavin            => r.Riboflavin?.InGrams,
		HealthParameter.DietarySelenium              => r.Selenium?.InGrams,
		HealthParameter.DietaryThiamin               => r.Thiamin?.InGrams,
		HealthParameter.DietaryCaffeine              => r.Caffeine?.InGrams,
		_                                            => null,
	};

	static DateTime? GetRecordStartTime(Record record)
	{
		if (record is IntervalRecord interval)
			return FromInstant(interval.StartTime);
		if (record is InstantaneousRecord instant)
			return FromInstant(instant.Time);
		return null;
	}

	static DateTime? GetRecordEndTime(Record record)
	{
		if (record is IntervalRecord interval)
			return FromInstant(interval.EndTime);
		if (record is InstantaneousRecord instant)
			return FromInstant(instant.Time);
		return null;
	}

	static string GetRecordSource(Record record) =>
		record.Metadata?.DataOrigin?.PackageName ?? string.Empty;

	// ────────────────────────────────────────────────────────────────────────────
	// Read operations
	// ────────────────────────────────────────────────────────────────────────────

	/// <inheritdoc/>
	public async Task<List<Sample>> ReadAllAsync(HealthParameter healthParameter, DateTime from, DateTime until, string unit)
	{
		await semaphore.WaitAsync();
		try
		{
			if (!IsSupported)
				throw new HealthException("Android Health Connect is not available on this device.");

			if (!healthParameterToRecordType.TryGetValue(healthParameter, out var recordType))
				throw new HealthException($"{healthParameter} is not supported on Android Health Connect.");

			var client = GetClient();
			var request = new ReadRecordsRequest(
				Java.Lang.Class.FromType(recordType),
				BuildTimeRange(from, until));

			var response = await client.ReadRecords(request);
			var results = new List<Sample>();

			foreach (var record in response.Records)
			{
				var value = ExtractValue(record, healthParameter);
				if (value is null)
					continue;

				results.Add(new Sample(
					from: GetRecordStartTime(record),
					until: GetRecordEndTime(record),
					value: value,
					source: GetRecordSource(record),
					unit: unit));
			}

			return results;
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<double> ReadCountAsync(HealthParameter healthParameter, DateTime from, DateTime until)
	{
		var samples = await ReadAllAsync(healthParameter, from, until, string.Empty);
		return samples.Sum(s => s.Value ?? 0d);
	}

	/// <inheritdoc/>
	public async Task<double?> ReadLatestAsync(HealthParameter healthParameter, DateTime from, DateTime until, string unit)
	{
		await semaphore.WaitAsync();
		try
		{
			if (!IsSupported)
				throw new HealthException("Android Health Connect is not available on this device.");

			if (!healthParameterToRecordType.TryGetValue(healthParameter, out var recordType))
				throw new HealthException($"{healthParameter} is not supported on Android Health Connect.");

			var client = GetClient();
			var request = new ReadRecordsRequest(
				Java.Lang.Class.FromType(recordType),
				BuildTimeRange(from, until),
				ascendingOrder: false,
				pageSize: 1);

			var response = await client.ReadRecords(request);
			var record = response.Records.FirstOrDefault();
			return record is null ? null : ExtractValue(record, healthParameter);
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<Sample?> ReadLatestAvailableAsync(HealthParameter healthParameter, string unit)
	{
		await semaphore.WaitAsync();
		try
		{
			if (!IsSupported)
				throw new HealthException("Android Health Connect is not available on this device.");

			if (!healthParameterToRecordType.TryGetValue(healthParameter, out var recordType))
				throw new HealthException($"{healthParameter} is not supported on Android Health Connect.");

			var client = GetClient();
			// Use an unbounded time range (epoch → now) sorted descending to get the most recent record.
			var request = new ReadRecordsRequest(
				Java.Lang.Class.FromType(recordType),
				TimeRangeFilter.Before(ToInstant(DateTime.UtcNow)),
				ascendingOrder: false,
				pageSize: 1);

			var response = await client.ReadRecords(request);
			var record = response.Records.FirstOrDefault();
			if (record is null)
				return null;

			var value = ExtractValue(record, healthParameter);
			return value is null ? null : new Sample(
				from: GetRecordStartTime(record),
				until: GetRecordEndTime(record),
				value: value,
				source: GetRecordSource(record),
				unit: unit);
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<double?> ReadAverageAsync(HealthParameter healthParameter, DateTime from, DateTime until, string unit)
	{
		try
		{
			var samples = await ReadAllAsync(healthParameter, from, until, unit);
			return samples.Count == 0 ? null : samples.Average(s => s.Value);
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
	}

	/// <inheritdoc/>
	public async Task<double?> ReadMinAsync(HealthParameter healthParameter, DateTime from, DateTime until, string unit)
	{
		try
		{
			var samples = await ReadAllAsync(healthParameter, from, until, unit);
			return samples.Count == 0 ? null : samples.Min(s => s.Value);
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
	}

	/// <inheritdoc/>
	public async Task<double?> ReadMaxAsync(HealthParameter healthParameter, DateTime from, DateTime until, string unit)
	{
		try
		{
			var samples = await ReadAllAsync(healthParameter, from, until, unit);
			return samples.Count == 0 ? null : samples.Max(s => s.Value);
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
	}

	// ────────────────────────────────────────────────────────────────────────────
	// Write operations
	// ────────────────────────────────────────────────────────────────────────────

	/// <inheritdoc/>
	public async Task<bool> WriteAsync(HealthParameter healthParameter, DateTime? date, double valueToStore, string unit)
	{
		await semaphore.WaitAsync();
		try
		{
			if (!IsSupported)
				throw new HealthException("Android Health Connect is not available on this device.");

			if (!healthParameterToRecordType.TryGetValue(healthParameter, out var recordType))
				throw new HealthException($"{healthParameter} is not supported on Android Health Connect.");

			var timestamp = date ?? DateTime.UtcNow;
			var instant = ToInstant(timestamp);
			var client = GetClient();

			Record record = BuildWriteRecord(healthParameter, recordType, valueToStore, instant);
			await client.InsertRecords(new Java.Util.ArrayList { record });
			return true;
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
		finally
		{
			semaphore.Release();
		}
	}

	static Record BuildWriteRecord(HealthParameter hp, Type recordType, double value, Instant instant)
	{
		var metadata = new Metadata(
			clientRecordId: null,
			dataOrigin: new DataOrigin(Platform.AppContext.PackageName),
			lastModifiedTime: instant,
			clientRecordVersion: 0,
			device: null,
			recordingMethod: Metadata.RecordingMethodManualEntry);

		if (recordType == typeof(StepsRecord))
			return new StepsRecord(instant, instant, (long)value, metadata);

		if (recordType == typeof(WeightRecord))
			return new WeightRecord(instant, Mass.Kilograms(value), metadata);

		if (recordType == typeof(HeightRecord))
			return new HeightRecord(instant, Length.Meters(value), metadata);

		if (recordType == typeof(HeartRateRecord))
		{
			var sample = new HeartRateRecord.Sample(instant, (long)value);
			return new HeartRateRecord(instant, instant, new[] { sample }, metadata);
		}

		if (recordType == typeof(ActiveCaloriesBurnedRecord))
			return new ActiveCaloriesBurnedRecord(instant, instant, Energy.Calories(value), metadata);

		if (recordType == typeof(OxygenSaturationRecord))
			return new OxygenSaturationRecord(instant, Percentage.Value(value), metadata);

		if (recordType == typeof(BloodGlucoseRecord))
			return new BloodGlucoseRecord(
				instant,
				BloodGlucoseRecord.SpecimenSourceCapillaryBlood,
				BloodGlucose.MillimolesPerLiter(value),
				BloodGlucoseRecord.RelationToMealUnknown,
				BloodGlucoseRecord.MealTypeUnknown,
				metadata);

		if (recordType == typeof(BloodPressureRecord))
			throw new HealthException(
				"Writing blood pressure requires both systolic and diastolic values. " +
				"Use a dedicated blood pressure write method that accepts both components.");

		if (recordType == typeof(BodyTemperatureRecord))
			return new BodyTemperatureRecord(
				instant,
				BodyTemperatureRecord.MeasurementLocationUnknown,
				Temperature.Celsius(value),
				metadata);

		if (recordType == typeof(RespiratoryRateRecord))
			return new RespiratoryRateRecord(instant, value, metadata);

		if (recordType == typeof(DistanceRecord))
			return new DistanceRecord(instant, instant, Length.Meters(value), metadata);

		if (recordType == typeof(FloorsClimbedRecord))
			return new FloorsClimbedRecord(instant, instant, value, metadata);

		if (recordType == typeof(RestingHeartRateRecord))
			return new RestingHeartRateRecord(instant, (long)value, metadata);

		if (recordType == typeof(BodyFatRecord))
			return new BodyFatRecord(instant, Percentage.Value(value), metadata);

		if (recordType == typeof(LeanBodyMassRecord))
			return new LeanBodyMassRecord(instant, Mass.Kilograms(value), metadata);

		if (recordType == typeof(WaistCircumferenceRecord))
			return new WaistCircumferenceRecord(instant, Length.Meters(value), metadata);

		if (recordType == typeof(Vo2MaxRecord))
			return new Vo2MaxRecord(instant, value, Vo2MaxRecord.MeasurementMethodOther, metadata);

		if (recordType == typeof(HydrationRecord))
			return new HydrationRecord(instant, instant, Volume.Milliliters(value), metadata);

		throw new HealthException($"Writing records of type {recordType.Name} is not supported.");
	}

	// ────────────────────────────────────────────────────────────────────────────
	// Workout operations
	// ────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Reads all workouts of <paramref name="workoutType"/> that fall within the given date range.
	/// GPS route data is included when it was recorded and is available in the Health Connect store.
	/// </summary>
	public async Task<List<Workout>> ReadAllWorkoutsAsync(WorkoutType workoutType, DateTime from, DateTime until)
	{
		await semaphore.WaitAsync();
		try
		{
			if (!IsSupported)
				throw new HealthException("Android Health Connect is not available on this device.");

			var client = GetClient();
			var request = new ReadRecordsRequest(
				Java.Lang.Class.FromType(typeof(ExerciseSessionRecord)),
				BuildTimeRange(from, until));

			var response = await client.ReadRecords(request);
			var workouts = new List<Workout>();

			foreach (var record in response.Records.OfType<ExerciseSessionRecord>())
			{
				var recordExerciseType = record.ExerciseType;

				// Filter to requested workout type, or include everything if WorkoutType.Other is requested.
				if (workoutType != WorkoutType.Other)
				{
					if (!workoutTypeToExerciseType.TryGetValue(workoutType, out var expectedType))
						continue;
					if (recordExerciseType != expectedType)
						continue;
				}

				exerciseTypeToWorkoutType.TryGetValue(recordExerciseType, out var mappedType);

				var startTime = FromInstant(record.StartTime);
				var endTime = FromInstant(record.EndTime);
				var durationSeconds = (endTime - startTime).TotalSeconds;

				double? calories = null;
				if (record.ExerciseType > 0)
				{
					// Attempt to read calories for this session from ActiveCaloriesBurnedRecord
					var calorieRequest = new ReadRecordsRequest(
						Java.Lang.Class.FromType(typeof(ActiveCaloriesBurnedRecord)),
						BuildTimeRange(startTime, endTime));
					var calorieResponse = await client.ReadRecords(calorieRequest);
					calories = calorieResponse.Records.OfType<ActiveCaloriesBurnedRecord>()
						.Sum(r => r.Energy.InCalories);
					if (calories == 0)
						calories = null;
				}

				double? distance = null;
				var distanceRequest = new ReadRecordsRequest(
					Java.Lang.Class.FromType(typeof(DistanceRecord)),
					BuildTimeRange(startTime, endTime));
				var distanceResponse = await client.ReadRecords(distanceRequest);
				var totalDistance = distanceResponse.Records.OfType<DistanceRecord>().Sum(r => r.Distance.InMeters);
				if (totalDistance > 0)
					distance = totalDistance;

				// Extract GPS route when available.
				// ExerciseRoute.Location carries latitude, longitude, and an optional altitude (Length).
				List<WorkoutCoordinate>? route = null;
				if (record.Route?.Locations is { Count: > 0 } locations)
				{
					route = locations
						.Select(loc => new WorkoutCoordinate(
							timestamp: DateTimeOffset.FromUnixTimeMilliseconds(loc.Time.ToEpochMilli()).UtcDateTime,
							latitude: loc.Latitude,
							longitude: loc.Longitude,
							altitude: loc.Altitude?.InMeters))
						.ToList();
				}

				var source = GetRecordSource(record);
				workouts.Add(new Workout(
					workoutType: mappedType,
					from: startTime,
					until: endTime,
					durationInSeconds: durationSeconds,
					energyBurnedInCalorie: calories,
					totalDistanceInMeter: distance,
					source: source,
					route: route));
			}

			return workouts;
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <summary>
	/// Returns the most recent workout of <paramref name="workoutType"/> in the given date range,
	/// including GPS route data when available.
	/// </summary>
	public async Task<Workout?> ReadLatestWorkoutAsync(WorkoutType workoutType, DateTime from, DateTime until)
	{
		try
		{
			var workouts = await ReadAllWorkoutsAsync(workoutType, from, until);
			return workouts.OrderByDescending(w => w.Until).FirstOrDefault();
		}
		catch (HealthException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new HealthException(ex.Message, ex);
		}
	}
}
