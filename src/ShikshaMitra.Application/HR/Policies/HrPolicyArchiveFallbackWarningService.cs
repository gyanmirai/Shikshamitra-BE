using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyArchiveFallbackWarningService(
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task AddAsync(HrPolicyImpactPreviewDto preview, IReadOnlyCollection<HrPolicyAssignment> affectedAssignments)
	{
		if (affectedAssignments.Count == 0 ||
			!preview.PolicyId.HasValue ||
			!string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (preview.PolicyType == "payroll")
		{
			var query = await payrollPolicyRepository.GetQueryableAsync();
			var defaults = await asyncExecuter.ToListAsync(query.Where(x =>
				x.AcademicYearId == preview.AcademicYearId &&
				x.Id != preview.PolicyId.Value &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active));
			await AddDefaultWarningsAsync(preview, defaults, x => x.WorkCalendarPolicyId, HRErrorCodes.DefaultPayrollPolicyRequired, "payroll");
			return;
		}

		if (preview.PolicyType == "attendance")
		{
			var query = await attendancePolicyRepository.GetQueryableAsync();
			var defaults = await asyncExecuter.ToListAsync(query.Where(x =>
				x.AcademicYearId == preview.AcademicYearId &&
				x.Id != preview.PolicyId.Value &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active));
			await AddDefaultWarningsAsync(preview, defaults, x => x.WorkCalendarPolicyId, HRErrorCodes.DefaultAttendancePolicyRequired, "attendance");
		}
	}

	private async Task AddDefaultWarningsAsync<TPolicy>(
		HrPolicyImpactPreviewDto preview,
		IReadOnlyList<TPolicy> defaults,
		Func<TPolicy, Guid> getWorkCalendarPolicyId,
		string defaultErrorCode,
		string policyType)
	{
		if (defaults.Count != 1)
		{
			AddBlockingWarning(preview, defaultErrorCode, $"Archiving this {policyType} policy would reassign active assignments, but exactly one active default {policyType} policy is required.");
			return;
		}

		var workCalendar = await workCalendarPolicyRepository.FindAsync(getWorkCalendarPolicyId(defaults[0]));
		if (workCalendar == null || workCalendar.AcademicYearId != preview.AcademicYearId || workCalendar.Status == HrPolicyStatus.Archived)
		{
			AddBlockingWarning(preview, HRErrorCodes.HrPolicyInvalidWorkCalendar, $"The default {policyType} policy selected for archive fallback references an invalid work calendar.");
			return;
		}

		if (workCalendar.Status != HrPolicyStatus.Active)
		{
			AddBlockingWarning(preview, HRErrorCodes.WorkCalendarPolicyMustBeActive, $"The default {policyType} policy selected for archive fallback references a non-active work calendar.");
		}
	}

	private static void AddBlockingWarning(HrPolicyImpactPreviewDto preview, string code, string message) =>
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = code,
			Message = message
		});
}
