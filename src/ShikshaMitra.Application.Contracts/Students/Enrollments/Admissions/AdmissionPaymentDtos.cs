using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentTransactionDto
{
	public Guid Id { get; set; }
	public int AttemptNumber { get; set; }
	public decimal Amount { get; set; }
	public string Currency { get; set; } = string.Empty;
	public string Method { get; set; } = string.Empty;
	public string CollectionMode { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? ExternalReference { get; set; }
	public string? Provider { get; set; }
	public string? ReceiptNumber { get; set; }
	public string? Notes { get; set; }
	public string? DecisionReason { get; set; }
	public Guid? RecordedBy { get; set; }
	public DateTime RecordedAt { get; set; }
	public Guid? VerifiedBy { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionPaymentStageDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageId { get; set; }
	public bool PaymentStageEnabled { get; set; }
	public AdmissionPaymentStagePolicy? Policy { get; set; }
	public AdmissionPaymentCompletionDto? Completion { get; set; }
	public bool IsActive { get; set; }
	public DateTime? DueAt { get; set; }
	public bool IsOverdue { get; set; }
	public decimal RequiredAmount { get; set; }
	public decimal SettledAmount { get; set; }
	public decimal PendingAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	public string Currency { get; set; } = string.Empty;
	public string Status { get; set; } = "unpaid";
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public List<AdmissionPaymentTransactionDto> Transactions { get; set; } = [];
	public List<string> Blockers { get; set; } = [];
	public bool CanRecord { get; set; }
	public bool CanStartOnlinePayment { get; set; }
	public bool CanVerify { get; set; }
	public bool CanWaive { get; set; }
	public bool CanReverse { get; set; }
	public bool CanComplete { get; set; }
}

public class RecordAdmissionPaymentDto
{
	public decimal Amount { get; set; }
	public string Method { get; set; } = string.Empty;
	public string? ExternalReference { get; set; }
	public string? Notes { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class ReviewAdmissionPaymentDto
{
	public bool Approved { get; set; }
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? TransactionConcurrencyStamp { get; set; }
}

public class AdmissionPaymentReasonDto
{
	public string Reason { get; set; } = string.Empty;
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? TransactionConcurrencyStamp { get; set; }
}
