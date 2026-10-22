using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Finance-owned balance shared by an admission application and its onboarding case.</summary>
public class AdmissionFeeCollectionDto
{
	public Guid ApplicationId { get; set; }
	public Guid? AccountId { get; set; }
	public Guid? StudentId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string ApplicationNumber { get; set; } = string.Empty;
	public AdmissionCollectionAcademicYearDto AcademicYear { get; set; } = new();
	public string Currency { get; set; } = string.Empty;
	public string StructureName { get; set; } = string.Empty;
	public int StructureRevision { get; set; }
	public decimal? TotalAmount { get; set; }
	public decimal ConfirmedPaidAmount { get; set; }
	public decimal PendingVerificationAmount { get; set; }
	public decimal? OutstandingAmount { get; set; }
	public bool CanCollect { get; set; }
	/// <summary>Prepared Finance ledger, charges and late fees are clear, with no payment awaiting verification.</summary>
	public bool IsSettled { get; set; }
	public string? Blocker { get; set; }
	public bool HasPaymentEvidence { get; set; }
	public List<AdmissionCollectionFeeHeadDto> FeeHeads { get; set; } = [];
	public List<AdmissionCollectionReceiptDto> Receipts { get; set; } = [];
	public List<AssignmentHeadDto> VariableFeeHeads { get; set; } = [];
}

public class AdmissionCollectionAcademicYearDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Year { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class AdmissionCollectionFeeHeadDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public decimal TotalAmount { get; set; }
	public decimal PaidAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	public string Status { get; set; } = "pending";
}

public class AdmissionCollectionReceiptDto
{
	public string Scope { get; set; } = "fee";
	public Guid Id { get; set; }
	public string ReceiptNumber { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string Status { get; set; } = "confirmed";
	public DateTimeOffset CollectedAt { get; set; }
	public List<string> PaymentMethods { get; set; } = [];
}

public class PrepareAdmissionFeeCollectionDto
{
	/// <summary>Only Finance-configured variable heads may receive initial amounts.</summary>
	public List<VariableFeeHeadAmountDto> VariableFeeHeads { get; set; } = [];
}

/// <summary>No student or academic-scope identifier is accepted from the client.</summary>
public class CollectAdmissionFeeInput
{
	public AutomaticCollectionSelectionDto? AutomaticAllocation { get; set; }
	[Required, MaxLength(100)]
	public string IdempotencyKey { get; set; } = string.Empty;
	[MaxLength(1000)]
	public string? Notes { get; set; }
	public bool? PaymentConfirmed { get; set; }
	public List<AdmissionDueFeePaymentDto> DueFeePayments { get; set; } = [];
	public List<AdmissionChargePaymentDto> ChargePayments { get; set; } = [];
	public List<AdmissionLateFeePaymentDto> LateFeePayments { get; set; } = [];
	[Required, MinLength(1), MaxLength(5)]
	public List<AdmissionFeePaymentDto> Payments { get; set; } = [];
}

public class AdmissionDueFeePaymentDto
{
	public Guid DueFeeId { get; set; }
	public Guid FeeHeadId { get; set; }
	public decimal Amount { get; set; }
}

public class AdmissionChargePaymentDto
{
	public Guid ChargeId { get; set; }
	public decimal Amount { get; set; }
}

public class AdmissionLateFeePaymentDto
{
	public Guid DueFeeId { get; set; }
	public decimal Amount { get; set; }
}

public class AdmissionFeePaymentDto
{
	public Guid? BankAccountId { get; set; }
	public PaymentMethodEnum PaymentMethod { get; set; }
	public decimal Amount { get; set; }
	public Dictionary<string, object?>? Details { get; set; }
}
