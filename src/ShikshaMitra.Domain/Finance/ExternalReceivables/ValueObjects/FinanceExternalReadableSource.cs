using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalReadableSource
{
	public Guid SourceId { get; init; }
	public Guid? StudentId { get; init; }
	public Guid? EmployeeId { get; init; }
	public Guid? AcademicYearId { get; init; }
}
