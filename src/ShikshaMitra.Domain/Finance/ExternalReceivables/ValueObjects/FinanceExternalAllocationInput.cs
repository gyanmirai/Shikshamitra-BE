using System;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalAllocationInput
{
	public Guid? ChargeTenant { get; init; }
	public Guid ChargeYear { get; init; }
	public Guid? ChargeStudent { get; init; }
	public Guid? ChargeEmployee { get; init; }
	public Guid? RowTenant { get; init; }
	public Guid? RowStudent { get; init; }
	public Guid? RowEmployee { get; init; }
	public decimal Amount { get; init; }
	public Guid? HeaderId { get; init; }
	public Guid? HeaderTenant { get; init; }
	public Guid? HeaderYear { get; init; }
	public Guid? HeaderStudent { get; init; }
	public Guid? HeaderEmployee { get; init; }
	public Guid? HeaderAccount { get; init; }
	public string? HeaderCurrency { get; init; }
	public FeeCollectionStatus? Status { get; init; }
}
