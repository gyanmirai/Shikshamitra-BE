using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalRefundSelection
{
	[Required] public FinanceExternalSourceReference Source { get; set; } = new();
	[Required, MaxLength(64)] public string Revision { get; set; } = string.Empty;
	public Guid OriginalReceiptId { get; set; }
	[Required, MinLength(1)] public List<FinanceExternalRefundLineInput> Lines { get; set; } = [];
	[Required, MaxLength(100)] public string Method { get; set; } = string.Empty;
	public Dictionary<string, object?>? PaymentDetails { get; set; }
}

public sealed class FinanceExternalRefundRequestInput : FinanceExternalRefundSelection
{
	[Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExternalRefundLineInput
{
	public Guid SourceAllocationId { get; set; }
	public Guid SourceReceiptId { get; set; }
	public decimal Amount { get; set; }
}

public sealed class FinanceExternalRefundableAllocationDto
{
	public Guid SourceAllocationId { get; set; }
	public Guid SourceReceiptId { get; set; }
	public string ReceiptNumber { get; set; } = string.Empty;
	public DateOnly SchoolReceiptDate { get; set; }
	public decimal PaidAmount { get; set; }
	public decimal CommittedAmount { get; set; }
	public decimal AvailableAmount { get; set; }
}

public sealed class FinanceExternalRefundFormDto
{
	/// <summary>Methods from the validated immutable refund policy version; no defaults.</summary>
	public List<string> AllowedPayoutMethods { get; set; } = [];
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
	public string Revision { get; set; } = string.Empty;
	public Guid? RefundPolicyVersionId { get; set; }
	public bool CanRequest { get; set; }
	public List<string> SetupIssues { get; set; } = [];
	public List<FinanceExternalRefundableAllocationDto> Sources { get; set; } = [];
}

public interface IFinanceExternalRefundAppService : IApplicationService
{
	Task<FinanceExternalRefundFormDto> GetFormAsync(FinanceExternalSourceReference source);
	Task<FeeRefundPreviewDto> PreviewAsync(FinanceExternalRefundSelection input);
	Task<FeeRefundRequestDto> CreateAsync(FinanceExternalRefundRequestInput input);
}
