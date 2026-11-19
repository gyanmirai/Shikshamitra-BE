using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task LeaveImpact_Explicit_Leave_Usability_Matches_Operational_Resolver(bool isActive)
	{
		var context = await CreatePolicyContextAsync();
		var dto = await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var policies = GetRequiredService<ILeavePolicyRepository>();
		var policy = (await policies.GetWithRulesAsync(context.LeavePolicy.Id))!;
		// Model an inconsistent stored row explicitly. This does not establish that
		// normal production mutations can produce Status=Active, IsActive=false.
		typeof(LeavePolicy).GetProperty(nameof(LeavePolicy.IsActive))!.SetValue(policy, isActive);
		await policies.UpdateAsync(policy, true);
		await GetRequiredService<IRepository<HrPolicyAssignment, Guid>>().InsertAsync(new HrPolicyAssignment(
			Guid.NewGuid(), _currentTenant.Id, context.AcademicYearId, HrPolicyAssignmentTargetType.Employee,
			dto.Id.ToString("D").ToLowerInvariant(), "Synthetic resolver parity", context.AttendancePolicy.Id,
			context.PayrollPolicy.Id, policy.Id, new DateTime(2026, 4, 1), null, true), true);
		var employee = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(dto.Id);
		var currentPolicies = await policies.GetListByAcademicYearIdAsync(context.AcademicYearId, includeInactive: true);
		var date = new DateTime(2026, 8, 1);
		var operational = GetRequiredService<HrPolicyResolver>();
		if (isActive)
			(await operational.ResolveAsync(dto.Id, date, context.AcademicYearId)).LeavePolicy!.Id.ShouldBe(policy.Id);
		else
			(await Should.ThrowAsync<BusinessException>(() => operational.ResolveAsync(dto.Id, date, context.AcademicYearId)))
				.Code.ShouldBe(HRErrorCodes.LeavePolicyInactive);
		var adapter = GetRequiredService<LeavePolicyImpactResolutionAdapter>();
		(await adapter.ResolveAsync(employee, context.AcademicYearId, date, currentPolicies)).Usable.ShouldBe(isActive);
		// A default-selection overlay alone must not activate the stored policy.
		(await adapter.ResolveAsync(employee, context.AcademicYearId, date, currentPolicies,
			proposedDefaultId: policy.Id, proposedPolicyId: policy.Id)).Usable.ShouldBe(isActive);
		await unit.CompleteAsync();
	}
}
