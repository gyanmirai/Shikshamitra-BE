using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionPaymentStagePolicy
{
	private static readonly HashSet<string> CollectionModes = new(StringComparer.Ordinal)
	{
		"staff", "online", "either"
	};
	private static readonly HashSet<string> Payers = new(StringComparer.Ordinal)
	{
		"applicant", "guardian", "either"
	};
	private static readonly HashSet<string> PaymentMethods = new(StringComparer.Ordinal)
	{
		"cash", "cheque", "demandDraft", "bankTransfer", "creditCard", "debitCard", "upi", "onlineGateway"
	};

	public int Version { get; init; }
	public bool Enabled { get; init; }
	public string PurposeCode { get; init; } = "APPLICATION_FEE";
	public string PurposeName { get; init; } = "Application fee";
	public string Currency { get; init; } = "INR";
	public decimal Amount { get; init; }
	public string CollectionMode { get; init; } = "staff";
	public string Payer { get; init; } = "either";
	public List<string> AcceptedMethods { get; init; } = [];
	public int? DueInDays { get; init; }
	public bool AllowLatePayment { get; init; } = true;
	public bool AllowInstallments { get; init; }
	public decimal? MinimumInstallmentAmount { get; init; }
	public bool RequireIndependentVerification { get; init; }
	public bool AllowWaiver { get; init; }
	public bool AutoAdvanceOnSettlement { get; init; } = true;

	public static AdmissionPaymentStagePolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("payment", out var value)) return null;
		if (value.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Payment stage settings must be an object.");
		var policy = value.Deserialize<AdmissionPaymentStagePolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Payment stage settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Payment stage settings version must be 1.");
		if (!Regex.IsMatch(PurposeCode ?? string.Empty, "^[A-Z][A-Z0-9_]{1,31}$"))
			throw new ArgumentException("Payment purpose code must contain 2 to 32 uppercase letters, numbers, or underscores.");
		if (string.IsNullOrWhiteSpace(PurposeName) || PurposeName.Trim().Length > 120)
			throw new ArgumentException("Payment purpose name is required and cannot exceed 120 characters.");
		if (!Regex.IsMatch(Currency ?? string.Empty, "^[A-Z]{3}$"))
			throw new ArgumentException("Payment currency must be a three-letter uppercase code.");
		if (Amount is < 0.01m or > 10_000_000m || decimal.Round(Amount, 2) != Amount)
			throw new ArgumentException("Payment amount must be between 0.01 and 10,000,000 with at most two decimal places.");
		if (!CollectionModes.Contains(CollectionMode))
			throw new ArgumentException("Choose staff, online, or either collection mode.");
		if (!Payers.Contains(Payer))
			throw new ArgumentException("Choose applicant, guardian, or either payer.");
		if (AcceptedMethods is not { Count: > 0 and <= 8 } ||
			AcceptedMethods.Any(method => !PaymentMethods.Contains(method)) ||
			AcceptedMethods.Distinct(StringComparer.Ordinal).Count() != AcceptedMethods.Count)
			throw new ArgumentException("Choose one or more unique supported payment methods.");
		if (CollectionMode == "online" && AcceptedMethods.Any(method => method != "onlineGateway"))
			throw new ArgumentException("Online-only collection accepts the configured online gateway only.");
		if (CollectionMode == "online" && !AcceptedMethods.Contains("onlineGateway", StringComparer.Ordinal))
			throw new ArgumentException("Online-only collection requires an online gateway.");
		if (CollectionMode == "staff" && AcceptedMethods.Contains("onlineGateway", StringComparer.Ordinal))
			throw new ArgumentException("Use online or either collection mode for online gateway payments.");
		if (DueInDays is < 1 or > 365)
			throw new ArgumentException("Payment due time must be between 1 and 365 days.");
		if (AllowInstallments && (MinimumInstallmentAmount is null or < 0.01m || MinimumInstallmentAmount > Amount))
			throw new ArgumentException("Choose a minimum installment between 0.01 and the required amount.");
		if (!AllowInstallments && MinimumInstallmentAmount.HasValue)
			throw new ArgumentException("A minimum installment applies only when installments are enabled.");
	}
}

public static class AdmissionPaymentStageErrorCodes
{
	public const string InvalidPolicy = "Admissions:Payment:InvalidPolicy";
	public const string InvalidState = "Admissions:Payment:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Payment:ConcurrencyConflict";
	public const string DeadlinePassed = "Admissions:Payment:DeadlinePassed";
	public const string AmountMismatch = "Admissions:Payment:AmountMismatch";
	public const string MethodNotAllowed = "Admissions:Payment:MethodNotAllowed";
	public const string VerificationRequired = "Admissions:Payment:VerificationRequired";
	public const string WaiverNotAllowed = "Admissions:Payment:WaiverNotAllowed";
}
