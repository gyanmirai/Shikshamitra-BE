using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionSeatStagePolicy
{
	private static readonly HashSet<string> ReservationModes = new(StringComparer.Ordinal)
	{
		"manual", "onEntry"
	};
	private static readonly HashSet<string> CompletionOutcomes = new(StringComparer.Ordinal)
	{
		"confirmed", "waitlisted", "released", "expired"
	};

	public int Version { get; init; }
	public bool Enabled { get; init; }
	public int DefaultCapacity { get; init; } = 40;
	public string ReservationMode { get; init; } = "manual";
	public int ReservationDurationHours { get; init; } = 72;
	public bool AllowDurationOverride { get; init; }
	public int MinimumDurationHours { get; init; } = 72;
	public int MaximumDurationHours { get; init; } = 72;
	public bool AllowRenewal { get; init; } = true;
	public int MaximumRenewals { get; init; } = 1;
	public bool AllowWaitlist { get; init; } = true;
	public bool AllowCapacityOverride { get; init; }
	public bool RequireReleaseReason { get; init; } = true;
	public List<string> AllowedCompletionOutcomes { get; init; } = ["confirmed"];
	public bool AutoAdvanceOnConfirmation { get; init; } = true;

	public static AdmissionSeatStagePolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("seat", out var value)) return null;
		if (value.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Seat stage settings must be an object.");
		var policy = value.Deserialize<AdmissionSeatStagePolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Seat stage settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Seat stage settings version must be 1.");
		if (DefaultCapacity is < 1 or > 100000)
			throw new ArgumentException("Seat capacity must be between 1 and 100,000.");
		if (!ReservationModes.Contains(ReservationMode))
			throw new ArgumentException("Choose manual reservation or reservation on stage entry.");
		if (MinimumDurationHours is < 1 or > 2160 || MaximumDurationHours is < 1 or > 2160 ||
			MinimumDurationHours > MaximumDurationHours || ReservationDurationHours < MinimumDurationHours ||
			ReservationDurationHours > MaximumDurationHours)
			throw new ArgumentException("Reservation duration must be within the configured 1 to 2,160 hour range.");
		if (!AllowDurationOverride &&
			(MinimumDurationHours != ReservationDurationHours || MaximumDurationHours != ReservationDurationHours))
			throw new ArgumentException("Use the default duration as both limits when duration override is disabled.");
		if (MaximumRenewals is < 0 or > 20 || (!AllowRenewal && MaximumRenewals != 0))
			throw new ArgumentException("Seat renewals must be between 0 and 20 and enabled before they are allowed.");
		if (AllowedCompletionOutcomes is not { Count: > 0 and <= 4 } ||
			AllowedCompletionOutcomes.Any(value => !CompletionOutcomes.Contains(value)) ||
			AllowedCompletionOutcomes.Distinct(StringComparer.Ordinal).Count() != AllowedCompletionOutcomes.Count ||
			!AllowedCompletionOutcomes.Contains("confirmed", StringComparer.Ordinal))
			throw new ArgumentException("Choose unique supported completion outcomes and include confirmed.");
		if (!AllowWaitlist && AllowedCompletionOutcomes.Contains("waitlisted", StringComparer.Ordinal))
			throw new ArgumentException("Enable the waitlist before allowing it to complete the stage.");
	}
}

public enum AdmissionSeatReservationStatus
{
	Reserved = 0,
	Confirmed = 1,
	Waitlisted = 2,
	Released = 3,
	Expired = 4
}

public static class AdmissionSeatStageErrorCodes
{
	public const string InvalidPolicy = "Admissions:Seat:InvalidPolicy";
	public const string InvalidState = "Admissions:Seat:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Seat:ConcurrencyConflict";
	public const string CapacityFull = "Admissions:Seat:CapacityFull";
	public const string CapacityInvalid = "Admissions:Seat:CapacityInvalid";
	public const string DurationInvalid = "Admissions:Seat:DurationInvalid";
	public const string RenewalNotAllowed = "Admissions:Seat:RenewalNotAllowed";
	public const string WaitlistNotAllowed = "Admissions:Seat:WaitlistNotAllowed";
	public const string ReasonRequired = "Admissions:Seat:ReasonRequired";
	public const string CompletionRequired = "Admissions:Seat:CompletionRequired";
	public const string RouteRequired = "Admissions:Seat:RouteRequired";
	public const string IdempotencyConflict = "Admissions:Seat:IdempotencyConflict";
}
