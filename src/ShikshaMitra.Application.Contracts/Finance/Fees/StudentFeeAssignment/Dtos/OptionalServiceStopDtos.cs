using System;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public record OptionalServiceStopInput(Guid AcademicYearId, Guid FeeHeadId, DateOnly LastServiceDate, string Reason, Guid RequestId)
{
	public string? PreviewToken { get; init; }
}

public record OptionalServiceStopResult(int InstallmentCount, decimal CancelledAmount, string? PreviewToken = null);
