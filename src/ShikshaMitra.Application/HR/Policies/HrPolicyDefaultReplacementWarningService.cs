using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyDefaultReplacementWarningService(
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task AddAsync(HrPolicyImpactPreviewDto preview, HrPolicyImpactPreviewRequestDto input)
	{
		if (!IsDefaultChangeAction(preview.Action) ||
			!await IsChangingToActiveDefaultAsync(preview, input))
		{
			return;
		}

		var existingDefaultName = await FindExistingDefaultNameAsync(
			preview.PolicyType,
			preview.AcademicYearId,
			preview.PolicyId);
		if (!string.IsNullOrWhiteSpace(existingDefaultName))
		{
			HrPolicyImpactWarningBuilder.AddDefaultReplacementWarning(preview, existingDefaultName);
		}
	}

	private static bool IsDefaultChangeAction(string action) =>
		string.Equals(action, "create-default", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(action, "set-default", StringComparison.OrdinalIgnoreCase);

	private async Task<bool> IsChangingToActiveDefaultAsync(
		HrPolicyImpactPreviewDto preview,
		HrPolicyImpactPreviewRequestDto input) =>
		preview.PolicyType switch
		{
			"payroll" when input.PayrollPolicy != null =>
				input.PayrollPolicy.IsDefault &&
				HrPolicyMapping.ParsePolicyStatus(input.PayrollPolicy.Status) == HrPolicyStatus.Active,
			"attendance" when input.AttendancePolicy != null =>
				input.AttendancePolicy.IsDefault &&
				HrPolicyMapping.ParsePolicyStatus(input.AttendancePolicy.Status) == HrPolicyStatus.Active,
			"payroll" when preview.PolicyId.HasValue =>
				await IsActivePayrollPolicyAsync(preview.PolicyId.Value),
			"attendance" when preview.PolicyId.HasValue =>
				await IsActiveAttendancePolicyAsync(preview.PolicyId.Value),
			_ => false
		};

	private async Task<bool> IsActivePayrollPolicyAsync(Guid policyId)
	{
		var policy = await payrollPolicyRepository.FindAsync(policyId);
		return policy?.Status == HrPolicyStatus.Active;
	}

	private async Task<bool> IsActiveAttendancePolicyAsync(Guid policyId)
	{
		var policy = await attendancePolicyRepository.FindAsync(policyId);
		return policy?.Status == HrPolicyStatus.Active;
	}

	private async Task<string?> FindExistingDefaultNameAsync(
		string policyType,
		Guid academicYearId,
		Guid? excludeId) =>
		policyType switch
		{
			"payroll" => (await asyncExecuter.FirstOrDefaultAsync(
				(await payrollPolicyRepository.GetQueryableAsync())
					.Where(x =>
						x.AcademicYearId == academicYearId &&
						x.Status == HrPolicyStatus.Active &&
						x.IsDefault &&
						(!excludeId.HasValue || x.Id != excludeId.Value))))
				?.Name,
			"attendance" => (await asyncExecuter.FirstOrDefaultAsync(
				(await attendancePolicyRepository.GetQueryableAsync())
					.Where(x =>
						x.AcademicYearId == academicYearId &&
						x.Status == HrPolicyStatus.Active &&
						x.IsDefault &&
						(!excludeId.HasValue || x.Id != excludeId.Value))))
				?.Name,
			_ => null
		};
}
