using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Theory]
	[InlineData(false, 0, 5)]
	[InlineData(false, 1, 0)]
	[InlineData(false, 5, 1)]
	[InlineData(true, 0, 5)]
	[InlineData(true, 1, 0)]
	[InlineData(true, 5, 1)]
	public async Task EffectivePolicyPreview_Should_Project_Notice_Days_From_Each_Selected_Active_Rule(
		bool useEmployeeAssignment,
		int paidNoticeDays,
		int unpaidNoticeDays)
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		if (!useEmployeeAssignment)
		{
			context.LeavePolicy.SetDefault(false);
			await _leavePolicyRepository.UpdateAsync(context.LeavePolicy, autoSave: true);
		}

		var policy = new LeavePolicy(
			Guid.NewGuid(), _currentTenant.Id, context.AcademicYearId,
			$"Notice preview {Guid.NewGuid():N}", new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31), HrPolicyStatus.Active, isDefault: !useEmployeeAssignment);
		var paid = CreateNoticePreviewRule(policy.Id, LeavePaidType.Paid, paidNoticeDays);
		var unpaid = CreateNoticePreviewRule(policy.Id, LeavePaidType.Unpaid, unpaidNoticeDays);
		// Reverse insertion order and retain a historical rule to catch bucket mixing
		// or accidentally projecting inactive rules instead of the selected active one.
		policy.ReplaceRules([unpaid, paid]);
		var retiredPaid = CreateNoticePreviewRule(policy.Id, LeavePaidType.Paid, 99);
		retiredPaid.Deactivate();
		policy.Rules.Add(retiredPaid);
		await _leavePolicyRepository.InsertAsync(policy, autoSave: true);

		if (useEmployeeAssignment)
		{
			// The existing default has zero-day notice for both buckets. Its values
			// must not overwrite the explicitly assigned policy's distinct values.
			await GetRequiredService<IRepository<HrPolicyAssignment, Guid>>().InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(), _currentTenant.Id, context.AcademicYearId, HrPolicyAssignmentTargetType.Employee,
				employee.Id.ToString("D").ToLowerInvariant(), employee.FullName,
				context.AttendancePolicy.Id, context.PayrollPolicy.Id, policy.Id,
				new DateTime(2026, 4, 1), null, true), autoSave: true);
		}

		var preview = await _assignments.GetPreviewAsync(new PolicyAssignmentPreviewRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			Date = new DateTime(2026, 5, 1),
			EmployeeId = employee.Id
		});

		preview.PolicySource.ShouldBe(useEmployeeAssignment ? "employee" : "default");
		preview.LeavePolicy.ShouldNotBeNull();
		preview.LeavePolicy.Id.ShouldBe(policy.Id);
		preview.LeavePolicy.Rules.Count.ShouldBe(2);
		var paidPreview = preview.LeavePolicy.Rules.Single(rule => rule.PaidType == "Paid");
		var unpaidPreview = preview.LeavePolicy.Rules.Single(rule => rule.PaidType == "Unpaid");
		paidPreview.RuleId.ShouldBe(paid.Id);
		paidPreview.MinNoticeDays.ShouldBe(paidNoticeDays);
		unpaidPreview.RuleId.ShouldBe(unpaid.Id);
		unpaidPreview.MinNoticeDays.ShouldBe(unpaidNoticeDays);

		// Exercise the actual projected response contract, including an explicit
		// zero value rather than a missing property that clients could default.
		using var json = JsonDocument.Parse(JsonSerializer.Serialize(preview, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
		var rules = json.RootElement.GetProperty("leavePolicy").GetProperty("rules").EnumerateArray().ToArray();
		rules.Single(rule => rule.GetProperty("paidType").GetString() == "Paid")
			.GetProperty("minNoticeDays").GetInt32().ShouldBe(paidNoticeDays);
		rules.Single(rule => rule.GetProperty("paidType").GetString() == "Unpaid")
			.GetProperty("minNoticeDays").GetInt32().ShouldBe(unpaidNoticeDays);
	}

	private LeavePolicyRule CreateNoticePreviewRule(Guid policyId, LeavePaidType paidType, int minNoticeDays) => new(
		Guid.NewGuid(), _currentTenant.Id, policyId, paidType,
		paidType == LeavePaidType.Paid ? LeaveEntitlementMode.Limited : LeaveEntitlementMode.Unlimited,
		LeaveAccrualPolicyType.Yearly, paidType == LeavePaidType.Paid ? 12 : null,
		carryForward: false, carryForwardLimit: null, encashable: false, encashmentTiming: null,
		requiresApproval: true, minNoticeDays: minNoticeDays, maxDaysPerRequest: 30,
		allowHalfDay: true, attachmentRequiredAfterDays: null, allowNegativeBalance: false);
}
