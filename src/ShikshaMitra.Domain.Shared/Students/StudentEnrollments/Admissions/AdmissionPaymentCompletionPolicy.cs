using System;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Admission progression only. Finance remains the owner of fees and settlement.</summary>
public sealed record AdmissionPaymentCompletionPolicy
{
	public int Version { get; init; } = 1;
	public string PaymentRule { get; init; } = "partial";
	public bool AllowCollectionLater { get; init; } = true;

	public static AdmissionPaymentCompletionPolicy Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return new();
		using var document = JsonDocument.Parse(json);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Stage settings must be an object.");
		if (!document.RootElement.TryGetProperty("paymentCompletion", out var value)) return new();
		if (value.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Payment completion settings must be an object.");
		var policy = value.Deserialize<AdmissionPaymentCompletionPolicy>(new JsonSerializerOptions
		{ PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Payment completion settings are required.");
		if (policy.Version != 1 || policy.PaymentRule is not ("partial" or "full"))
			throw new ArgumentException("Choose a supported Payment completion version and partial or full payment rule.");
		return policy;
	}
}
