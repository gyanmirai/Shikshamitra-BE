using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyWorkCalendarReferenceResolver(
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	internal async Task<WorkCalendarPolicyReferences> ResolveAsync(
		string policyType,
		Guid? policyId,
		Guid academicYearId,
		bool includeDraftReferences)
	{
		if (policyType != "work_calendar" || !policyId.HasValue)
		{
			return WorkCalendarPolicyReferences.Empty;
		}

		var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
		var payrollPolicies = await asyncExecuter.ToListAsync(payrollQuery.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.WorkCalendarPolicyId == policyId.Value &&
			(includeDraftReferences ? x.Status != HrPolicyStatus.Archived : x.Status == HrPolicyStatus.Active)));
		var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
		var attendancePolicies = await asyncExecuter.ToListAsync(attendanceQuery.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.WorkCalendarPolicyId == policyId.Value &&
			(includeDraftReferences ? x.Status != HrPolicyStatus.Archived : x.Status == HrPolicyStatus.Active)));

		return new WorkCalendarPolicyReferences(
			payrollPolicies.Select(x => x.Id).ToHashSet(),
			attendancePolicies.Select(x => x.Id).ToHashSet(),
			payrollPolicies.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(5).ToList(),
			attendancePolicies.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(5).ToList());
	}
}
