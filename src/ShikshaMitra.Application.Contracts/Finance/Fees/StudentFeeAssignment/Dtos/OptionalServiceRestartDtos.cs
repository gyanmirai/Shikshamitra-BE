using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public record OptionalServiceRestartInput(Guid AcademicYearId, Guid FeeHeadId, DateOnly StartDate,
	DateOnly EndDate, decimal MonthlyRate, Guid ProrationPolicyVersionId, Guid PaymentTermVersionId, Guid RequestId)
{
	public string? PreviewToken { get; init; }
}

public record OptionalServiceRestartOption(Guid VersionId, string Name, int Version);

public record OptionalServiceRestartContext(DateOnly EarliestStartDate, DateOnly LatestEndDate, decimal? MonthlyRate,
	Guid? DefaultProrationPolicyVersionId, Guid? DefaultPaymentTermVersionId,
	IReadOnlyList<OptionalServiceRestartOption> ProrationPolicies,
	IReadOnlyList<OptionalServiceRestartOption> PaymentTerms, decimal PrepaidCredit);

public record OptionalServiceRestartResult(DateOnly StartDate, DateOnly EndDate, decimal MonthlyRate,
	decimal ScheduledTotal, decimal ProrationReduction, decimal AssignedTotal, decimal PrepaidCredit,
	IReadOnlyList<StudentFeeAssignmentPreviewLineDto> Lines, string? PreviewToken = null)
{
	public IReadOnlyList<OptionalServiceExistingInstallment> ExistingInstallments { get; init; } = [];
}

public record OptionalServiceExistingInstallment(Guid DueFeeId, string InstallmentLabel,
	DateOnly CoverageStartDate, DateOnly CoverageEndDate, decimal BilledAmount, decimal OutstandingAmount);

public record OptionalFeeServicePeriodDto(Guid Id, DateOnly StartDate, DateOnly EndDate,
	DateOnly? CancellationEffectiveDate, decimal MonthlyRate, string ProrationPolicyName,
	int ProrationPolicyVersion, decimal AssignedAmount);
