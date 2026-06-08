using System;

namespace ShikshaMitra.Finance.Common;

public class FinanceClassDivisionFilterDto : IFinanceClassDivisionFilter
{
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
}
