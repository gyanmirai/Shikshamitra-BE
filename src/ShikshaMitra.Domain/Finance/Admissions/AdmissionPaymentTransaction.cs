using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Admissions;

public static class AdmissionPaymentStatuses
{
	public const string Pending = "pending";
	public const string PendingVerification = "pendingVerification";
	public const string Settled = "settled";
	public const string Failed = "failed";
	public const string Rejected = "rejected";
	public const string Reversed = "reversed";
	public const string Waived = "waived";
}

public class AdmissionPaymentTransaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public int AttemptNumber { get; private set; }
	public string PurposeCode { get; private set; } = string.Empty;
	public string Currency { get; private set; } = string.Empty;
	public decimal Amount { get; private set; }
	public string Method { get; private set; } = string.Empty;
	public string CollectionMode { get; private set; } = string.Empty;
	public string Status { get; private set; } = AdmissionPaymentStatuses.Pending;
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string? ExternalReference { get; private set; }
	public string? Provider { get; private set; }
	public string? ReceiptNumber { get; private set; }
	public string? Notes { get; private set; }
	public string? DecisionReason { get; private set; }
	public Guid? RecordedBy { get; private set; }
	public DateTime RecordedAt { get; private set; }
	public Guid? VerifiedBy { get; private set; }
	public DateTime? VerifiedAt { get; private set; }
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public string? LastOperationType { get; private set; }
	public string? LastOperationIdempotencyKey { get; private set; }

	protected AdmissionPaymentTransaction() { }

	public static AdmissionPaymentTransaction RecordStaffPayment(
		Guid id, Guid? tenantId, Guid applicationId, Guid stageId, int attemptNumber,
		decimal amount, string purposeCode, string currency, string method, string? externalReference, string? receiptNumber,
		string? notes, string policySnapshotJson, string idempotencyKey, Guid actorId,
		DateTime now, bool requireIndependentVerification)
	{
		var payment = Create(id, tenantId, applicationId, stageId, attemptNumber, amount, purposeCode, currency, method,
			"staff", policySnapshotJson, idempotencyKey, actorId, now);
		payment.ExternalReference = NormalizeOptional(externalReference, 160);
		payment.ReceiptNumber = NormalizeOptional(receiptNumber, 80);
		payment.Notes = NormalizeOptional(notes, 1024);
		if (method != "cash" && payment.ExternalReference == null)
			throw Invalid("A transaction reference is required for non-cash payments.");
		payment.Status = requireIndependentVerification
			? AdmissionPaymentStatuses.PendingVerification
			: AdmissionPaymentStatuses.Settled;
		if (!requireIndependentVerification)
		{
			payment.VerifiedBy = actorId;
			payment.VerifiedAt = now.ToUniversalTime();
		}
		return payment;
	}

	public static AdmissionPaymentTransaction CreateOnlineIntent(
		Guid id, Guid? tenantId, Guid applicationId, Guid stageId, int attemptNumber,
		decimal amount, string purposeCode, string currency, string policySnapshotJson, string idempotencyKey, Guid actorId, DateTime now)
	{
		var payment = Create(id, tenantId, applicationId, stageId, attemptNumber, amount, purposeCode, currency,
			"onlineGateway", "online", policySnapshotJson, idempotencyKey, actorId, now);
		payment.Status = AdmissionPaymentStatuses.Pending;
		return payment;
	}

	public static AdmissionPaymentTransaction RecordWaiver(
		Guid id, Guid? tenantId, Guid applicationId, Guid stageId, int attemptNumber,
		string purposeCode, string currency, string reason, string policySnapshotJson,
		string idempotencyKey, Guid actorId, DateTime now)
	{
		var payment = Create(id, tenantId, applicationId, stageId, attemptNumber, 0m, purposeCode, currency,
			"waiver", "staff", policySnapshotJson, idempotencyKey, actorId, now, allowZero: true);
		payment.Status = AdmissionPaymentStatuses.Waived;
		payment.DecisionReason = NormalizeRequired(reason, 1024);
		payment.VerifiedBy = actorId;
		payment.VerifiedAt = now.ToUniversalTime();
		return payment;
	}

	public bool IsOperationRetry(string operationType, string idempotencyKey) =>
		LastOperationType == operationType.Trim() && LastOperationIdempotencyKey == idempotencyKey.Trim();

	public void Verify(bool approved, string? reason, string idempotencyKey, Guid actorId, DateTime now)
	{
		var operation = approved ? "verify" : "reject";
		if (IsOperationRetry(operation, idempotencyKey)) return;
		if (Status != AdmissionPaymentStatuses.PendingVerification)
			throw Invalid("Only a payment awaiting verification can be reviewed.");
		if (RecordedBy == actorId)
			throw Invalid("A different authorized staff member must verify this payment.");
		DecisionReason = approved ? NormalizeOptional(reason, 1024) : NormalizeRequired(reason, 1024);
		VerifiedBy = actorId;
		VerifiedAt = now.ToUniversalTime();
		Status = approved ? AdmissionPaymentStatuses.Settled : AdmissionPaymentStatuses.Rejected;
		LastOperationType = operation;
		LastOperationIdempotencyKey = NormalizeRequired(idempotencyKey, 80);
	}

	public void SettleOnline(string provider, string externalReference, string receiptNumber, DateTime now)
	{
		if (Status == AdmissionPaymentStatuses.Settled &&
			ExternalReference == externalReference.Trim() && Provider == provider.Trim()) return;
		if (Status != AdmissionPaymentStatuses.Pending)
			throw Invalid("Only a pending online payment can be settled.");
		Provider = NormalizeRequired(provider, 80);
		ExternalReference = NormalizeRequired(externalReference, 160);
		ReceiptNumber = NormalizeRequired(receiptNumber, 80);
		VerifiedAt = now.ToUniversalTime();
		Status = AdmissionPaymentStatuses.Settled;
	}

	public void FailOnline(string reason, DateTime now)
	{
		if (Status != AdmissionPaymentStatuses.Pending)
			throw Invalid("Only a pending online payment can fail.");
		DecisionReason = NormalizeRequired(reason, 1024);
		VerifiedAt = now.ToUniversalTime();
		Status = AdmissionPaymentStatuses.Failed;
	}

	public void Reverse(string reason, string idempotencyKey, Guid actorId, DateTime now)
	{
		if (IsOperationRetry("reverse", idempotencyKey)) return;
		if (Status != AdmissionPaymentStatuses.Settled)
			throw Invalid("Only a settled payment can be reversed.");
		DecisionReason = NormalizeRequired(reason, 1024);
		VerifiedBy = actorId;
		VerifiedAt = now.ToUniversalTime();
		Status = AdmissionPaymentStatuses.Reversed;
		LastOperationType = "reverse";
		LastOperationIdempotencyKey = NormalizeRequired(idempotencyKey, 80);
	}

	private static AdmissionPaymentTransaction Create(
		Guid id, Guid? tenantId, Guid applicationId, Guid stageId, int attemptNumber,
		decimal amount, string purposeCode, string currency, string method, string collectionMode, string policySnapshotJson,
		string idempotencyKey, Guid actorId, DateTime now, bool allowZero = false)
	{
		if (applicationId == Guid.Empty || stageId == Guid.Empty || actorId == Guid.Empty)
			throw Invalid("Application, stage, and actor are required.");
		if (attemptNumber < 1) throw Invalid("Payment attempt number must be positive.");
		if ((!allowZero && amount <= 0m) || amount < 0m || amount > 10_000_000m || decimal.Round(amount, 2) != amount)
			throw Invalid("Payment amount must be positive with at most two decimal places.");
		return new AdmissionPaymentTransaction
		{
			Id = id,
			TenantId = tenantId,
			ApplicationId = applicationId,
			StageInstanceId = stageId,
			AttemptNumber = attemptNumber,
			PurposeCode = NormalizeRequired(purposeCode, 32),
			Currency = NormalizeRequired(currency, 3),
			Amount = amount,
			Method = NormalizeRequired(method, 32),
			CollectionMode = NormalizeRequired(collectionMode, 16),
			IdempotencyKey = NormalizeRequired(idempotencyKey, 80),
			PolicySnapshotJson = NormalizeRequired(policySnapshotJson, 8000),
			RecordedBy = actorId,
			RecordedAt = now.ToUniversalTime()
		};
	}

	private static string NormalizeRequired(string? value, int max) =>
		Check.NotNullOrWhiteSpace(value, nameof(value), max).Trim();
	private static string? NormalizeOptional(string? value, int max) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), max);
	private static BusinessException Invalid(string message) =>
		new(AdmissionPaymentStageErrorCodes.InvalidState, message);
}
