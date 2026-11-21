using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public enum FinanceExternalWaiverAction { Apply, Approve, Reject, Reverse }

public sealed class FinanceExternalWaiverContextDto
{
	public FinanceExternalSourceReference Source { get; set; } = new();
	public FinanceExternalReceivableDto Receivable { get; set; } = new();
	public string Revision { get; set; } = string.Empty;
	public Guid? CollectionPolicyVersionId { get; set; }
	public bool WaiverEnabled { get; set; }
	public bool RequiresApproval { get; set; }
	public bool CanExecute { get; set; }
	public List<string> Issues { get; set; } = [];
	public PagedResultDto<FinanceExternalWaiverContextItemDto> Requests { get; set; } = new(0, []);
}

public sealed class FinanceExternalWaiverContextItemDto
{
	public Guid RequestId { get; set; }
	public Guid? ApplicationId { get; set; }
	public FinanceExternalWaiverState State { get; set; }
	public decimal RequestedAmount { get; set; }
	public decimal AppliedAmount { get; set; }
	public decimal ReversedAmount { get; set; }
	public decimal ReversibleAmount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public bool CanAct { get; set; }
	public List<string> Issues { get; set; } = [];
}
