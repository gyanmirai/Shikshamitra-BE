using System;

namespace ShikshaMitra.Finance.Common;

/// <summary>
/// Optional class (standard) and division filters for finance list queries.
/// Omitted or null values mean "All".
/// </summary>
public interface IFinanceClassDivisionFilter
{
	Guid? StandardId { get; set; }
	Guid? DivisionId { get; set; }
}
