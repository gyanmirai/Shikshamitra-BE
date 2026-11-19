using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyProposedRuleDifferenceReader(
	WorkCalendarPolicyReader calendarReader,
	IRepository<PayrollPolicy, Guid> payrollRepository,
	IRepository<AttendancePolicy, Guid> attendanceRepository,
	HrPolicyVersionReadService versions) : ITransientDependency
{
	public async Task FillAsync(HrPolicyImpactPreviewDto preview, HrPolicyImpactPreviewRequestDto request)
	{
		if (!request.PolicyId.HasValue)
		{
			preview.RuleDifferencesStatus = "new_policy";
			return;
		}
		object? proposed = preview.PolicyType switch
		{
			"work_calendar" => request.WorkCalendarPolicy,
			"payroll" => request.PayrollPolicy,
			"attendance" => request.AttendancePolicy,
			_ => null
		};
		if (proposed == null)
		{
			proposed = preview.Action switch
			{
				"archive" => new { status = "archived" },
				"set-default" => new { isDefault = true },
				_ => null
			};
		}
		if (proposed == null) return;
		object? current = preview.PolicyType switch
		{
			"work_calendar" => await calendarReader.GetAsync(request.PolicyId.Value),
			"payroll" => HrPolicyMapping.ToDto(await payrollRepository.GetAsync(request.PolicyId.Value)),
			"attendance" => HrPolicyMapping.ToDto(await attendanceRepository.GetAsync(request.PolicyId.Value)),
			_ => null
		};
		if (current == null) return;
		preview.RuleDifferences = await versions.CompareProposedRulesAsync(current, proposed);
		preview.RuleDifferencesStatus = "calculated";
	}
}