using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalSourceReference
{
	public FinanceExternalSourceKind Kind { get; set; }
	public Guid SourceId { get; set; }
	[Required, MaxLength(64)] public string SourceVersion { get; set; } = string.Empty;
}

public sealed class PostFinanceExternalReceivableInput
{
	[Required] public FinanceExternalSourceReference Source { get; set; } = new();
	public Guid FinanceChargeCategoryId { get; set; }
	public Guid? CollectionPolicyVersionId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	[Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class FinanceExternalPayerDto
{
	public FinanceExternalPayerKind Kind { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
}

public sealed class FinanceExternalReceivableDto
{
	public FinanceExternalSourceReference Source { get; set; } = new();
	public Guid FinanceChargeRequestId { get; set; }
	public Guid FinanceChargeId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string? AcademicYearName { get; set; }
	public FinanceExternalPayerDto Payer { get; set; } = new();
	public string Currency { get; set; } = string.Empty;
	public DateOnly AssessmentDate { get; set; }
	public DateOnly DueDate { get; set; }
	public decimal OriginalAmount { get; set; }
	public decimal AcceptedPaymentAmount { get; set; }
	public decimal SettledPaymentAmount { get; set; }
	public decimal PendingVerificationAmount { get; set; }
	public decimal WaivedAmount { get; set; }
	public decimal RefundedAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	/// <summary>Canonical unsettled balance, including amounts reserved for bank verification.</summary>
	public decimal FinancialOutstandingAmount { get; set; }
	public ChargeStatus ApprovalStatus { get; set; }
	public bool SettlementAvailable { get; set; }
	public string? SettlementBlockedReason { get; set; }
}

public interface IFinanceExternalReceivableAppService : IApplicationService
{
	Task<FinanceExternalReceivableDto> PostAsync(PostFinanceExternalReceivableInput input);
	Task<FinanceExternalReceivableDto> GetAsync(FinanceExternalSourceReference source);
}
