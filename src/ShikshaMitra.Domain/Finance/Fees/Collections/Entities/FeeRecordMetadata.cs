using ShikshaMitra.Finance.Fees.Collections;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Represents the top-level metadata for a single fee collection transaction.
/// One record is created per collection event, linking all fee-head payments,
/// charge payments, and payment-method splits together.
/// </summary>
public class FeeRecordMetadata : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private FeeRecordMetadata() { /* For ORM */ }

	/// <summary>
	/// Creates a new collection transaction record. All mutations must go through
	/// domain methods (<see cref="MarkReversed"/>, <see cref="MarkPaid"/>, <see cref="MarkPartial"/>).
	/// </summary>
	public FeeRecordMetadata(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid academicYearId,
		double totalPaidFee,
		string receiptNumber,
		FeeCollectionStatus status,
		string? remarks = null,
		string? idempotencyKey = null,
		string? commandFingerprint = null) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		AcademicYearId = academicYearId;
		TotalPaidFee = totalPaidFee;
		ReceiptNumber = receiptNumber;
		Status = status;
		Remarks = remarks;
		IdempotencyKey = idempotencyKey;
		CommandFingerprint = commandFingerprint;
	}

	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public double TotalPaidFee { get; private set; }

	/// <summary>Unique receipt number formatted as REC-{YYYY}-{NNNNNN}.</summary>
	public string ReceiptNumber { get; private set; } = string.Empty;

	/// <summary>Payment status of this collection record.</summary>
	public FeeCollectionStatus Status { get; private set; }

	public string? Remarks { get; private set; }

	/// <summary>Client retry key for this collection command.</summary>
	public string? IdempotencyKey { get; private set; }

	/// <summary>SHA-256 fingerprint of the command bound to the retry key.</summary>
	public string? CommandFingerprint { get; private set; }

	/// <summary>Serialized successful response returned for safe command replays.</summary>
	public string? CommandResultJson { get; private set; }

	public void SetCommandResult(string resultJson)
	{
		if (string.IsNullOrWhiteSpace(resultJson))
			throw new ArgumentException("Command result is required.", nameof(resultJson));
		CommandResultJson = resultJson;
	}

	/// <summary>
	/// Marks this collection transaction as reversed after instrument rejection
	/// (e.g. cheque bounce). Only valid if the current status is not already Reversed.
	/// </summary>
	public void MarkReversed(string? remarks = null)
	{
		Status = FeeCollectionStatus.Reversed;
		if (!string.IsNullOrWhiteSpace(remarks))
			Remarks = remarks;
	}

	/// <summary>Marks this transaction as fully paid after cheque/DD reconciliation.</summary>
	public void MarkPaid()
	{
		Status = FeeCollectionStatus.Paid;
	}

	/// <summary>Marks this transaction as partially paid (some dues remain outstanding).</summary>
	public void MarkPartial()
	{
		Status = FeeCollectionStatus.Partial;
	}
}
