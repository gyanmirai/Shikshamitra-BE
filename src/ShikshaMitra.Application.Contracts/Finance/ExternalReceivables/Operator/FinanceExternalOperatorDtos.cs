using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public enum FinanceExternalOperatorOptionKind { ChargeCategory, CollectionPolicyVersion, RefundPolicyVersion }

public sealed class FinanceExternalOperatorOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid? PolicyId { get; set; }
	public int? VersionNumber { get; set; }
	public bool IsSelectable { get; set; }
	public string? BlockedReason { get; set; }
}

public sealed class FinanceExternalOperatorContextDto
{
	public FinanceExternalSourceReference Source { get; set; } = new();
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
	public string Revision { get; set; } = string.Empty;
	public Guid? SettlementPolicyBindingId { get; set; }
	public Guid? CollectionPolicyVersionId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	public bool CanExecute { get; set; }
	public List<string> Issues { get; set; } = [];
}

public sealed class FinanceExternalOperatorHistoryDto
{
	public Guid Id { get; set; }
	public string Operation { get; set; } = string.Empty;
	public DateTimeOffset RecordedAt { get; set; }
	public string? ActorName { get; set; }
	public string? ResultKind { get; set; }
	public Guid? ResultId { get; set; }
	public bool CanOpenResult { get; set; }
}
