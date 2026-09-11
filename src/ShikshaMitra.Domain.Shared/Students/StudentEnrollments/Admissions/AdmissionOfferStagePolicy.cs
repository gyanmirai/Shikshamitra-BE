using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionOfferStagePolicy
{
	private static readonly HashSet<string> OfferTypes = new(StringComparer.Ordinal)
	{
		"unconditional", "conditional"
	};
	private static readonly HashSet<string> CompletionOutcomes = new(StringComparer.Ordinal)
	{
		"accepted", "declined", "expired", "withdrawn"
	};

	public int Version { get; init; }
	public bool Enabled { get; init; }
	public string OfferType { get; init; } = "unconditional";
	public string Title { get; init; } = "Admission offer";
	public string Message { get; init; } = "We are pleased to offer admission.";
	public string? Terms { get; init; }
	public string? Conditions { get; init; }
	public int ValidityDays { get; init; } = 14;
	public bool AllowExpiryOverride { get; init; } = true;
	public int MinimumValidityDays { get; init; } = 1;
	public int MaximumValidityDays { get; init; } = 90;
	public bool RequireTermsAcceptance { get; init; } = true;
	public bool RequireConditionsConfirmation { get; init; }
	public bool RequireDeclineReason { get; init; } = true;
	public bool AllowExtension { get; init; } = true;
	public int MaximumExtensions { get; init; } = 2;
	public bool AllowReissue { get; init; } = true;
	public int MaximumReissues { get; init; } = 1;
	public List<string> AllowedCompletionOutcomes { get; init; } = ["accepted", "declined"];
	public bool AutoAdvanceOnAcceptance { get; init; } = true;

	public static AdmissionOfferStagePolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("offer", out var value)) return null;
		if (value.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Offer stage settings must be an object.");
		var policy = value.Deserialize<AdmissionOfferStagePolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Offer stage settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Offer stage settings version must be 1.");
		if (!OfferTypes.Contains(OfferType)) throw new ArgumentException("Choose an unconditional or conditional offer.");
		Required(Title, 160, "Offer title");
		Required(Message, 4000, "Offer message");
		Optional(Terms, 8000, "Offer terms");
		Optional(Conditions, 4000, "Offer conditions");
		if (OfferType == "conditional" && string.IsNullOrWhiteSpace(Conditions))
			throw new ArgumentException("Conditional offers require clear conditions.");
		if (OfferType == "unconditional" && RequireConditionsConfirmation)
			throw new ArgumentException("Conditions confirmation applies only to conditional offers.");
		if (MinimumValidityDays is < 1 or > 365 || MaximumValidityDays is < 1 or > 365 ||
			MinimumValidityDays > MaximumValidityDays || ValidityDays < MinimumValidityDays || ValidityDays > MaximumValidityDays)
			throw new ArgumentException("Offer validity must be within the configured 1 to 365 day range.");
		if (!AllowExpiryOverride && (MinimumValidityDays != ValidityDays || MaximumValidityDays != ValidityDays))
			throw new ArgumentException("Use the default validity as both limits when expiry override is disabled.");
		if (MaximumExtensions is < 0 or > 20 || (!AllowExtension && MaximumExtensions != 0))
			throw new ArgumentException("Offer extensions must be between 0 and 20 and enabled before they are allowed.");
		if (MaximumReissues is < 0 or > 10 || (!AllowReissue && MaximumReissues != 0))
			throw new ArgumentException("Offer reissues must be between 0 and 10 and enabled before they are allowed.");
		if (AllowedCompletionOutcomes is not { Count: > 0 and <= 4 } ||
			AllowedCompletionOutcomes.Any(value => !CompletionOutcomes.Contains(value)) ||
			AllowedCompletionOutcomes.Distinct(StringComparer.Ordinal).Count() != AllowedCompletionOutcomes.Count)
			throw new ArgumentException("Choose one or more unique supported completion outcomes.");
	}

	private static void Required(string? value, int max, string label)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
			throw new ArgumentException($"{label} is required and cannot exceed {max} characters.");
	}

	private static void Optional(string? value, int max, string label)
	{
		if (value?.Trim().Length > max) throw new ArgumentException($"{label} cannot exceed {max} characters.");
	}
}

public static class AdmissionOfferStageErrorCodes
{
	public const string InvalidPolicy = "Admissions:Offer:InvalidPolicy";
	public const string InvalidState = "Admissions:Offer:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Offer:ConcurrencyConflict";
	public const string ExpiryInvalid = "Admissions:Offer:ExpiryInvalid";
	public const string ResponseRequired = "Admissions:Offer:ResponseRequired";
	public const string ConfirmationRequired = "Admissions:Offer:ConfirmationRequired";
	public const string ExtensionNotAllowed = "Admissions:Offer:ExtensionNotAllowed";
	public const string ReissueNotAllowed = "Admissions:Offer:ReissueNotAllowed";
	public const string RouteRequired = "Admissions:Offer:RouteRequired";
	public const string IdempotencyConflict = "Admissions:Offer:IdempotencyConflict";
}
