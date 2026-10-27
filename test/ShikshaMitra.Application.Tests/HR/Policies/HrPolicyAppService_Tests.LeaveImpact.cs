using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Common;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(80)]
	public async Task LeaveImpact_Counts_Actual_Default_Staff_Without_Assignments(int count)
	{
		using var tenant = _currentTenant.Change(Guid.NewGuid());
		var context = await CreatePolicyContextAsync();
		for (var i = 0; i < count; i++) await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var proposal = LeavePolicySnapshots.Capture(context.LeavePolicy);
		proposal.Rules.Single(x => x.PaidType == "Paid").LeaveCount++;
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(new()
		{
			AcademicYearId = context.AcademicYearId, PolicyId = context.LeavePolicy.Id,
			AsOfDate = new DateTime(2026, 8, 1), Proposal = proposal
		});
		var measures = assessment.Preview.LeaveImpact!.Measures;
		measures["effectiveStaff"].Count.ShouldBe(count);
		measures["changedStaff"].Count.ShouldBe(count);
		measures["assignmentReferences"].Count.ShouldBe(0);
		measures["changedBalanceRows"].Count.ShouldBeNull();
		measures["changedBalanceRows"].Status.ShouldBe("not_calculated");
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Same_Count_Proposal_Changes_Fingerprint_And_Preserves_Policy_Date_Semantics()
	{
		var context = await CreatePolicyContextAsync();
		await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var reader = GetRequiredService<LeavePolicyImpactPreviewReader>();
		var proposal = LeavePolicySnapshots.Capture(context.LeavePolicy);
		proposal.EffectiveFrom = new DateTime(2026, 11, 1);
		var request = new LeavePolicyImpactRequest { AcademicYearId = context.AcademicYearId,
			PolicyId = context.LeavePolicy.Id, AsOfDate = new DateTime(2026, 8, 1), Proposal = proposal };
		var first = await reader.ReadAsync(request);
		proposal.Rules.Single(x => x.PaidType == "Paid").MinNoticeDays++;
		var second = await reader.ReadAsync(request);
		first.Preview.LeaveImpact!.Measures["effectiveStaff"].Count!.Value.ShouldBeGreaterThan(0);
		first.Preview.LeaveImpact.Measures["changedStaff"].Count.ShouldBe(second.Preview.LeaveImpact!.Measures["changedStaff"].Count);
		first.Preview.LeaveImpact.Fingerprint.ShouldNotBe(second.Preview.LeaveImpact.Fingerprint);
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Uses_Real_Group_Winner_And_Null_Employee_Leave_Default_Fallback()
	{
		var context = await CreatePolicyContextAsync();
		var role = await CreateUniqueRoleAsync();
		var first = await CreateActiveLeaveImpactEmployeeAsync(role);
		var second = await CreateActiveLeaveImpactEmployeeAsync(role);
		var group = await CreateRoleGroupAsync(context.AcademicYearId, role, "Leave impact role group");
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var alternative = await _leavePolicyManager.CreateAsync(context.AcademicYearId, $"Alternative {Guid.NewGuid():N}",
			new DateTime(2026, 4, 1), null, HrPolicyStatus.Active, false,
			LeavePolicySnapshots.Capture(context.LeavePolicy).Rules.Select(LeavePolicyProjection.ToRuleInput));
		await _leavePolicyRepository.InsertAsync(alternative, true);
		var repository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		await repository.InsertAsync(new HrPolicyAssignment(Guid.NewGuid(), _currentTenant.Id, context.AcademicYearId,
			HrPolicyAssignmentTargetType.Group, group.Id.ToString("D").ToLowerInvariant(), group.Name,
			context.AttendancePolicy.Id, context.PayrollPolicy.Id, alternative.Id, new DateTime(2026, 4, 1), null, true, 100), true);
		await repository.InsertAsync(new HrPolicyAssignment(Guid.NewGuid(), _currentTenant.Id, context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee, first.Id.ToString("D").ToLowerInvariant(), "Synthetic override",
			context.AttendancePolicy.Id, context.PayrollPolicy.Id, null, new DateTime(2026, 4, 1), null, true), true);
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(new()
		{
			AcademicYearId = context.AcademicYearId, PolicyId = context.LeavePolicy.Id,
			AsOfDate = new DateTime(2026, 8, 1), Proposal = LeavePolicySnapshots.Capture(context.LeavePolicy)
		});
		assessment.Population.Staff.Single(x => x.EmployeeId == first.Id).Current.PolicyId.ShouldBe(context.LeavePolicy.Id);
		assessment.Population.Staff.Single(x => x.EmployeeId == first.Id).Current.Source.ShouldBe("default");
		assessment.Population.Staff.Single(x => x.EmployeeId == second.Id).Current.PolicyId.ShouldBe(alternative.Id);
		assessment.Preview.LeaveImpact!.Measures["assignmentReferences"].Count.ShouldBe(0);
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Does_Not_Treat_Null_Assignments_As_References_To_A_New_Policy()
	{
		var context = await CreatePolicyContextAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var refs = await GetRequiredService<LeavePolicyImpactReferenceReader>().ReadAsync(context.AcademicYearId,
			null, [], new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		refs.Assignments.ShouldBeEmpty();
		refs.Requests.ShouldBeEmpty();
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Retains_Source_And_Target_Settlement_Rule_References_Without_Requests()
	{
		var context = await CreatePolicyContextAsync();
		var paid = context.LeavePolicy.Rules.Single(x => x.PaidType == AttendanceAndLeaves.Settings.LeavePaidType.Paid);
		var unpaid = context.LeavePolicy.Rules.Single(x => x.PaidType == AttendanceAndLeaves.Settings.LeavePaidType.Unpaid);
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var item = new LeaveYearEndSettlementItem(Guid.NewGuid(), _currentTenant.Id, Guid.NewGuid(), context.AcademicYearId,
			context.AcademicYearId, Guid.NewGuid(), context.LeavePolicy.Id, paid.Id);
		item.UpdateDraft(context.AcademicYearId, unpaid.Id, 0, 0, 0, 0, 0, 0, 0, 0, null, 0, []);
		await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().InsertAsync(item, true);
		var ids = await GetRequiredService<AttendanceAndLeaves.IEmployeeLeaveRepository>().GetReferencedRuleIdsAsync([paid.Id, unpaid.Id]);
		ids.ShouldContain(paid.Id);
		ids.ShouldContain(unpaid.Id);
		await unit.CompleteAsync();
	}

	private async Task<EmployeePersonalInfoDto> CreateActiveLeaveImpactEmployeeAsync(Guid? role = null)
	{
		var dto = await CreateEmployeeAsync(role);
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var repository = GetRequiredService<IRepository<Employee, Guid>>();
		var employee = await repository.GetAsync(dto.Id);
		// The production onboarding API deliberately creates inactive drafts. Seed the
		// active population explicitly in this disposable fixture; never change that API.
		employee.UpdateEmployeeStatus(EmployeeStatusType.Active, "Synthetic active fixture", null, DateTimeOffset.UtcNow);
		await repository.UpdateAsync(employee, true);
		await unit.CompleteAsync();
		return dto;
	}
}
