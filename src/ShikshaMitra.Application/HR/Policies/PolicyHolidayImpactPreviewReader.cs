using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class PolicyHolidayImpactPreviewReader(
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	PolicyHolidayImpactFinalizedHolidayReader holidayReader,
	PolicyHolidayImpactItemBuilder itemBuilder)
	: ApplicationService, ITransientDependency
{
	public async Task<PolicyHolidayImpactPreviewDto> GetPreviewAsync(
		Guid academicYearId,
		Guid? workCalendarPolicyId,
		Guid? payrollPolicyId,
		Guid? attendancePolicyId,
		DateTime? fromDate,
		DateTime? toDate,
		string? month)
	{
		var attendancePolicy = attendancePolicyId.HasValue
			? await attendancePolicyRepository.GetAsync(attendancePolicyId.Value)
			: await GetDefaultAttendancePolicyAsync(academicYearId);
		var payrollPolicy = payrollPolicyId.HasValue
			? await payrollPolicyRepository.GetAsync(payrollPolicyId.Value)
			: await GetDefaultPayrollPolicyAsync(academicYearId);
		var workCalendarPolicy = workCalendarPolicyId.HasValue
			? await workCalendarPolicyRepository.GetAsync(workCalendarPolicyId.Value)
			: await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId);
		EnsureUsablePolicy(attendancePolicy.AcademicYearId, attendancePolicy.Status, academicYearId, HRErrorCodes.DefaultAttendancePolicyRequired);
		EnsureUsablePolicy(payrollPolicy.AcademicYearId, payrollPolicy.Status, academicYearId, HRErrorCodes.DefaultPayrollPolicyRequired);
		EnsureUsableWorkCalendar(workCalendarPolicy, academicYearId);

		var (rangeStart, rangeEnd) = ResolveHolidayRange(fromDate, toDate, month);
		var finalizedHolidays = await holidayReader.GetAsync(academicYearId, workCalendarPolicy);
		var rangeStartDate = rangeStart.HasValue ? DateOnly.FromDateTime(rangeStart.Value) : (DateOnly?)null;
		var rangeEndDate = rangeEnd.HasValue ? DateOnly.FromDateTime(rangeEnd.Value) : (DateOnly?)null;
		var holidays = finalizedHolidays
			.Where(x => x.IsIncluded)
			.Where(x =>
				(!rangeStartDate.HasValue || x.ToDate >= rangeStartDate.Value) &&
				(!rangeEndDate.HasValue || x.FromDate <= rangeEndDate.Value))
			.OrderBy(x => x.FromDate)
			.ToList();

		return new PolicyHolidayImpactPreviewDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = workCalendarPolicy.Id,
			PayrollPolicyId = payrollPolicy.Id,
			AttendancePolicyId = attendancePolicy.Id,
			Items = itemBuilder.Build(holidays, workCalendarPolicy, payrollPolicy, attendancePolicy)
		};
	}

	private static (DateTime? Start, DateTime? End) ResolveHolidayRange(DateTime? fromDate, DateTime? toDate, string? month)
	{
		if (!string.IsNullOrWhiteSpace(month))
		{
			if (!DateTime.TryParseExact(
				    month.Trim(),
				    "yyyy-MM",
				    CultureInfo.InvariantCulture,
				    DateTimeStyles.None,
				    out var monthDate))
			{
				throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
			}

			var start = new DateTime(monthDate.Year, monthDate.Month, 1);
			return (start, start.AddMonths(1).AddDays(-1));
		}

		if (fromDate.HasValue && toDate.HasValue && toDate.Value.Date < fromDate.Value.Date)
		{
			throw new BusinessException(HRErrorCodes.PolicyEffectiveRangeInvalid);
		}

		return (fromDate?.Date, toDate?.Date);
	}

	private async Task<AttendancePolicy> GetDefaultAttendancePolicyAsync(Guid academicYearId)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active));

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private async Task<PayrollPolicy> GetDefaultPayrollPolicyAsync(Guid academicYearId)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active));

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private static void EnsureUsablePolicy(Guid policyAcademicYearId, HrPolicyStatus status, Guid academicYearId, string inactiveErrorCode)
	{
		if (policyAcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(inactiveErrorCode);
		}
	}

	private static void EnsureUsableWorkCalendar(WorkCalendarPolicy workCalendarPolicy, Guid academicYearId)
	{
		if (workCalendarPolicy.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidWorkCalendar);
		}

		if (workCalendarPolicy.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyMustBeActive);
		}
	}
}
