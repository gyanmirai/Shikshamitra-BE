using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalSettlementCommand
{
	[Required] public FinanceExternalSourceReference Source { get; set; } = new();
	[Required, MaxLength(64)] public string Revision { get; set; } = string.Empty;
	[Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class BindFinanceExternalSettlementPolicyInput : FinanceExternalSettlementCommand
{
	public Guid CollectionPolicyVersionId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExternalApprovalInput : FinanceExternalSettlementCommand
{
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExternalCollectInput : FinanceExternalSettlementCommand
{
	public DateOnly SchoolReceiptDate { get; set; }
	[MaxLength(1000)] public string? Notes { get; set; }
	[Required, MinLength(1)] public List<FinanceExternalPaymentInput> Payments { get; set; } = [];
}

public sealed class FinanceExternalPaymentInput
{
	public PaymentMethodEnum PaymentMethod { get; set; }
	public decimal Amount { get; set; }
	public Guid? BankAccountId { get; set; }
	public Dictionary<string, object?>? Details { get; set; }
}

public sealed class FinanceExternalCollectionFormDto
{
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
	public string Revision { get; set; } = string.Empty;
	public Guid? SettlementPolicyBindingId { get; set; }
	public Guid? CollectionPolicyVersionId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	public List<PaymentMethodEnum> PaymentMethods { get; set; } = [];
	public bool CanCollect { get; set; }
	public List<string> SetupIssues { get; set; } = [];
}

public sealed class FinanceExternalCollectionDto
{
	public Guid CollectionId { get; set; }
	public Guid ReceiptId { get; set; }
	public string ReceiptNumber { get; set; } = string.Empty;
	public FeeReceiptKind ReceiptKind { get; set; }
	public FeeReceiptVerificationStatus VerificationStatus { get; set; }
	public DateOnly SchoolReceiptDate { get; set; }
	public decimal AcceptedAmount { get; set; }
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
}

public sealed class FinanceExternalWaiverInput : FinanceExternalSettlementCommand
{
	public decimal Amount { get; set; }
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExternalWaiverDecisionInput : FinanceExternalSettlementCommand
{
	public Guid WaiverRequestId { get; set; }
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class ReverseFinanceExternalWaiverInput : FinanceExternalSettlementCommand
{
	public Guid WaiverApplicationId { get; set; }
	public decimal Amount { get; set; }
	[Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExternalWaiverDto
{
	public Guid RequestId { get; set; }
	public Guid? ApplicationId { get; set; }
	public FinanceExternalWaiverState State { get; set; }
	public decimal RequestedAmount { get; set; }
	public decimal AppliedAmount { get; set; }
	public decimal ReversedAmount { get; set; }
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
}

public interface IFinanceExternalSettlementAppService : IApplicationService
{
	Task<FinanceExternalCollectionFormDto> GetCollectFormAsync(FinanceExternalSourceReference source);
	Task<FinanceExternalCollectionFormDto> BindPolicyAsync(BindFinanceExternalSettlementPolicyInput input);
	Task<FinanceExternalReceivableDto> ApproveAsync(FinanceExternalApprovalInput input);
	Task<FinanceExternalCollectionDto> CollectAsync(FinanceExternalCollectInput input);
	Task<FinanceExternalWaiverDto> RequestWaiverAsync(FinanceExternalWaiverInput input);
	Task<FinanceExternalWaiverDto> ApproveWaiverAsync(FinanceExternalWaiverDecisionInput input);
	Task<FinanceExternalWaiverDto> RejectWaiverAsync(FinanceExternalWaiverDecisionInput input);
	Task<FinanceExternalWaiverDto> ReverseWaiverAsync(ReverseFinanceExternalWaiverInput input);
}
