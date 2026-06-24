using System;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class CustomOptionalFeeHeadDetail
{
	public Guid FeeHeadId { get; set; }
	public double Amount { get; set; }

	// For one-time optional fees
	public MonthEnum? PayableMonth { get; set; }
	public int? PayableYear { get; set; }

	// For recurring optional fees
	public MonthEnum? StartPayableMonth { get; set; }
	public int? StartPayableYear { get; set; }
	public MonthEnum? EndPayableMonth { get; set; }
	public int? EndPayableYear { get; set; }
}
