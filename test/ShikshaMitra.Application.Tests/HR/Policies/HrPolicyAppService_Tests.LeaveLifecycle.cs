using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Fact]
	public async Task LeaveLifecycle_Enrolls_Honest_Baseline_And_Preserves_Requests_And_Both_Settlement_Rules()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateActiveLeaveImpactEmployeeAsync();
		var paid = context.LeavePolicy.Rules.Single(x => x.PaidType == LeavePaidType.Paid);
		var unpaid = context.LeavePolicy.Rules.Single(x => x.PaidType == LeavePaidType.Unpaid);
		var request = ImpactRequest(context, employee.Id, paid.Id, EmployeeLeaveStatusType.Approved, new DateTime(2026, 8, 7));
		var settlement = new LeaveYearEndSettlementItem(Guid.NewGuid(), _currentTenant.Id, Guid.NewGuid(), context.AcademicYearId,
			context.AcademicYearId, employee.Id, context.LeavePolicy.Id, paid.Id);
		settlement.UpdateDraft(context.AcademicYearId, unpaid.Id, 12, 0, 1, 0, 11, 0, 0, 11, null, 0, []);
		using (var unit = _unitOfWorkManager.Begin(requiresNew: true))
		{
			await GetRequiredService<IEmployeeLeaveRepository>().InsertAsync(request, true);
			await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().InsertAsync(settlement, true);
			await unit.CompleteAsync();
		}
		(await _policyVersions.GetListAsync("leave", context.LeavePolicy.Id, new())).Items.ShouldBeEmpty(); // GET never backfills.
		using (var unit = _unitOfWorkManager.Begin(requiresNew: true))
		{
			var persisted = await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().GetAsync(settlement.Id);
			persisted.TargetLeavePolicyRuleId.ShouldBe(unpaid.Id);
			var references = await GetRequiredService<IEmployeeLeaveRepository>().GetReferencedRuleIdsAsync([paid.Id, unpaid.Id]);
			references.ShouldContain(paid.Id); references.ShouldContain(unpaid.Id);
			await unit.CompleteAsync();
		}
		var snapshot = LeavePolicySnapshots.Capture(context.LeavePolicy);
		var update = new LeavePolicyUpdateRequest
		{
			PolicyTitle = snapshot.PolicyTitle, Status = snapshot.Status, IsDefault = snapshot.IsDefault,
			EffectiveFrom = snapshot.EffectiveFrom, EffectiveTo = snapshot.EffectiveTo, Description = snapshot.Description,
			Rules = snapshot.Rules, ExpectedVersion = 1
		};
		update.Rules.Single(x => x.PaidType == "Paid").LeaveCount = 24;
		update.Rules.Single(x => x.PaidType == "Unpaid").MinNoticeDays += 1;
		var changed = await ReviewedLeaveUpdateAsync(context, update);
		changed.Version.ShouldBe(2);
		changed.Rules.Single(x => x.PaidType == "Paid" && x.IsActive).Id.ShouldNotBe(paid.Id);
		changed.Rules.Single(x => x.PaidType == "Unpaid" && x.IsActive).Id.ShouldNotBe(unpaid.Id);
		var history = await _policyVersions.GetListAsync("leave", context.LeavePolicy.Id, new());
		history.Items.Count.ShouldBe(2);
		history.Items.Single(x => x.VersionNumber == 1).ChangeAction.ShouldBe("baseline");
		history.Items.Single(x => x.VersionNumber == 1).ChangeReason.ShouldContain("earlier history is unavailable");
		var old = history.Items.Single(x => x.VersionNumber == 1);
		var diff = await _policyVersions.CompareAsync("leave", context.LeavePolicy.Id, old.Id, history.Items.Single(x => x.VersionNumber == 2).Id);
		diff.Differences.ShouldContain(x => x.FieldKey.Contains("leaveCount"));
		diff.Differences.ShouldContain(x => x.FieldKey.Contains("minNoticeDays"));
		var restored = (LeavePolicyDetailResponse) await _policyVersions.RestoreDraftAsync("leave", context.LeavePolicy.Id, old.Id,
			new() { Name = $"Restored {Guid.NewGuid():N}", ChangeReason = "Synthetic restore" });
		restored.Status.ShouldBe("Draft"); restored.IsDefault.ShouldBeFalse(); restored.SourceVersionId.ShouldBe(old.Id);
		restored.Rules.Select(x => x.Id).Intersect(context.LeavePolicy.Rules.Select(x => x.Id)).ShouldBeEmpty();
		restored.Rules.Single(x => x.PaidType == "Unpaid" && x.IsActive).MinNoticeDays.ShouldBe(unpaid.MinNoticeDays);
		restored.Rules.Single(x => x.PaidType == "Paid" && x.IsActive).LeaveCount.ShouldBe(paid.LeaveCount);
		using (var unit = _unitOfWorkManager.Begin(requiresNew: true))
		{
			var preserved = await GetRequiredService<IEmployeeLeaveRepository>().GetAsync(request.Id);
			preserved.LeavePolicyRuleId.ShouldBe(paid.Id); preserved.ApprovedLeaveUnits.ShouldBe(1m);
			var item = await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().GetAsync(settlement.Id);
			item.SourceLeavePolicyRuleId.ShouldBe(paid.Id); item.TargetLeavePolicyRuleId.ShouldBe(unpaid.Id); item.ForfeitedDays.ShouldBe(11);
			var source = await GetRequiredService<ILeavePolicyRepository>().GetWithRulesAsync(context.LeavePolicy.Id);
			source!.Rules.ShouldContain(x => x.Id == paid.Id && x.LeaveCount == paid.LeaveCount && !x.IsActive);
			await unit.CompleteAsync();
		}
		update.ExpectedVersion = 2;
		(await ReviewedLeaveUpdateAsync(context, update)).Version.ShouldBe(2); // Exact no-op adds no fake event.
		(await _policyVersions.GetListAsync("leave", context.LeavePolicy.Id, new())).Items.Count.ShouldBe(2);
	}

	[Fact]
	public async Task LeaveLifecycle_Invalid_Default_Replacement_Rolls_Back_Default_And_History()
	{
		var context = await CreatePolicyContextAsync();
		var body = LeavePolicySnapshots.Capture(context.LeavePolicy);
		body.IsDefault = true; // A duplicate name must fail without changing the current default or enrolling history.
		body.ImpactScope = new() { AsOfDate = new DateTime(2026, 8, 15) };
		var preview = await GetRequiredService<ILeavePolicyImpactAppService>().PreviewAsync(new()
			{ AcademicYearId = context.AcademicYearId, Action = "create", AsOfDate = body.ImpactScope.AsOfDate, Proposal = body });
		var previous = _httpContextAccessor.HttpContext;
		_httpContextAccessor.HttpContext = new DefaultHttpContext();
		try
		{
			_httpContextAccessor.HttpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
			await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILeavePolicyAppService>().CreateLeavePolicyAsync(context.AcademicYearId, body));
		}
		finally { _httpContextAccessor.HttpContext = previous; }
		(await GetRequiredService<ILeavePolicyAppService>().GetLeavePolicyByIdAsync(context.LeavePolicy.Id)).IsDefault.ShouldBeTrue();
		(await _policyVersions.GetListAsync("leave", context.LeavePolicy.Id, new())).Items.ShouldBeEmpty();
	}

	private async Task<LeavePolicyDetailResponse> ReviewedLeaveUpdateAsync(PolicyContext context, LeavePolicyUpdateRequest update)
	{
		var scope = ImpactScope(context);
		scope.Proposal = update; scope.ExpectedVersion = update.ExpectedVersion;
		var preview = await GetRequiredService<ILeavePolicyImpactAppService>().PreviewAsync(scope);
		var previous = _httpContextAccessor.HttpContext;
		_httpContextAccessor.HttpContext = new DefaultHttpContext();
		try
		{
			_httpContextAccessor.HttpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
			return await GetRequiredService<ILeavePolicyAppService>().UpdateLeavePolicyAsync(context.LeavePolicy.Id, update);
		}
		finally { _httpContextAccessor.HttpContext = previous; }
	}
}
