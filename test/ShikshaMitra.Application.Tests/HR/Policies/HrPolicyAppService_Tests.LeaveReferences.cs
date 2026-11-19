using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Fact]
	public async Task LeaveImpact_Request_References_Keep_Status_History_And_Inclusive_Date_Boundaries_Separate()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var repository = GetRequiredService<IEmployeeLeaveRepository>();
		var rule = context.LeavePolicy.Rules.Single(x => x.PaidType == LeavePaidType.Paid);
		var requests = new[]
		{
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Pending, new DateTime(2026, 8, 1)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Reviewed, new DateTime(2026, 8, 31)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Approved, new DateTime(2026, 8, 7)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Cancelled, new DateTime(2026, 8, 8)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Rejected, new DateTime(2026, 8, 9)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Pending, new DateTime(2026, 7, 31)),
			ImpactRequest(context, employee.Id, rule.Id, EmployeeLeaveStatusType.Reviewed, new DateTime(2026, 9, 1))
		};
		foreach (var request in requests) await repository.InsertAsync(request, true);
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(ImpactScope(context));
		var measures = assessment.Preview.LeaveImpact!.Measures;
		measures["requestReferences"].Count.ShouldBe(7);
		measures["openRequestReferences"].Count.ShouldBe(2);
		measures["pendingRequestReferences"].Count.ShouldBe(1);
		measures["reviewedRequestReferences"].Count.ShouldBe(1);
		measures["approvedRequestReferences"].Count.ShouldBe(1);
		measures["historicalRequestReferences"].Count.ShouldBe(1);
		measures["futureRequestReferences"].Count.ShouldBe(1);
		measures["requestReferences"].Scope.ShouldBe("all_policy_history");
		measures["openRequestReferences"].Scope.ShouldBe("requested_window");
		assessment.References.Requests.Select(x => x.LeavePolicyRuleId).Distinct().ShouldBe([rule.Id]);
		assessment.References.Requests.Single(x => x.Status == EmployeeLeaveStatusType.Approved).ApprovedLeaveUnits.ShouldBe(1m);
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Uses_Request_And_Settlement_Provenance_And_Reports_Actual_Lock_State()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateActiveLeaveImpactEmployeeAsync();
		var settlementEmployee = await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var paid = context.LeavePolicy.Rules.Single(x => x.PaidType == LeavePaidType.Paid);
		await GetRequiredService<IEmployeeLeaveRepository>().InsertAsync(
			ImpactRequest(context, employee.Id, paid.Id, EmployeeLeaveStatusType.Approved, new DateTime(2026, 8, 7)), true);
		var snapshots = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var locked = new PayrollRunItemSnapshot(Guid.NewGuid(), _currentTenant.Id, "2026-08", employee.Id);
		locked.MarkLocked();
		await snapshots.InsertAsync(locked, true);
		// These snapshots have no request/settlement overlap in their own employee/month.
		await snapshots.InsertAsync(new PayrollRunItemSnapshot(Guid.NewGuid(), _currentTenant.Id, "2026-09", employee.Id), true);
		await snapshots.InsertAsync(new PayrollRunItemSnapshot(Guid.NewGuid(), _currentTenant.Id, "2026-08", settlementEmployee.Id), true);
		var settlement = new LeaveYearEndSettlementItem(Guid.NewGuid(), _currentTenant.Id, Guid.NewGuid(), context.AcademicYearId,
			null, settlementEmployee.Id, context.LeavePolicy.Id, paid.Id);
		settlement.UpdateDraft(null, null, 0, 0, 0, 0, 0, 0, 0, 0, "2026-09", 0, []);
		await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().InsertAsync(settlement, true);
		var request = ImpactScope(context);
		request.ToDate = new DateTime(2026, 9, 30);
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(request);
		assessment.Preview.LeaveImpact!.Measures["payrollExposure"].Count.ShouldBe(2);
		assessment.References.PayrollSnapshots.Select(x => x.Id).ShouldBe([locked.Id]);
		var periods = assessment.Preview.LeaveImpact.PayrollPeriods;
		periods.Single(x => x.Month == "2026-08").LockState.ShouldBe("locked");
		periods.Single(x => x.Month == "2026-08").LockedSnapshotEmployeeCount.ShouldBe(1);
		periods.Single(x => x.Month == "2026-09").LockState.ShouldBe("no_snapshot");
		periods.Single(x => x.Month == "2026-09").SettlementEmployeeCount.ShouldBe(1);
		assessment.Preview.LockedPayrollMonths.ShouldBeEmpty(); // Mere pinned exposure is not changed payroll input.
		assessment.Preview.LeaveImpact.Measures["salaryDifferences"].Count.ShouldBeNull();
		await unit.CompleteAsync();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeaveImpact_Malformed_Payroll_Month_Is_Unavailable_Never_Zero(bool settlementMonth)
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var paid = context.LeavePolicy.Rules.Single(x => x.PaidType == LeavePaidType.Paid);
		await GetRequiredService<IEmployeeLeaveRepository>().InsertAsync(
			ImpactRequest(context, employee.Id, paid.Id, EmployeeLeaveStatusType.Approved, new DateTime(2026, 8, 7)), true);
		var snapshots = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		await snapshots.InsertAsync(new PayrollRunItemSnapshot(Guid.NewGuid(), _currentTenant.Id, "2026-08", employee.Id), true);
		if (settlementMonth)
		{
			var item = new LeaveYearEndSettlementItem(Guid.NewGuid(), _currentTenant.Id, Guid.NewGuid(), context.AcademicYearId,
				null, employee.Id, context.LeavePolicy.Id, paid.Id);
			item.UpdateDraft(null, null, 0, 0, 0, 0, 0, 0, 0, 0, "invalid", 0, []);
			await GetRequiredService<IRepository<LeaveYearEndSettlementItem, Guid>>().InsertAsync(item, true);
		}
		else await snapshots.InsertAsync(new PayrollRunItemSnapshot(Guid.NewGuid(), _currentTenant.Id, "invalid", employee.Id), true);
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(ImpactScope(context));
		assessment.Preview.LeaveImpact!.Completeness.ShouldBe("partial");
		assessment.Preview.LeaveImpact.Measures["payrollExposure"].Count.ShouldBeNull();
		assessment.Preview.LeaveImpact.Measures["payrollExposure"].Status.ShouldBe("unavailable");
		assessment.Preview.LeaveImpact.Measures["payrollExposure"].AvailabilityReason.ShouldNotBeNull().ShouldContain("invalid month");
		assessment.Preview.LeaveImpact.PayrollPeriods.Count.ShouldBe(1); // Explicitly partial known periods.
		assessment.Preview.LeaveImpact.Measures["effectiveStaff"].Status.ShouldBe("calculated");
		assessment.Preview.ConfirmationToken.ShouldBeNull();
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Unresolved_Population_Does_Not_Erase_Independent_Reference_Counts()
	{
		var context = await CreatePolicyContextAsync();
		await CreateActiveLeaveImpactEmployeeAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var policy = await _leavePolicyRepository.GetAsync(context.LeavePolicy.Id);
		policy.SetDefault(false); // Deliberately incomplete disposable fixture.
		await _leavePolicyRepository.UpdateAsync(policy, true);
		var request = ImpactScope(context);
		request.Proposal!.IsDefault = false;
		var assessment = await GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(request);
		assessment.Preview.LeaveImpact!.Measures["effectiveStaff"].Count.ShouldBeNull();
		assessment.Preview.LeaveImpact.Measures["effectiveStaff"].Status.ShouldBe("unavailable");
		assessment.Preview.LeaveImpact.Measures["assignmentReferences"].Count.ShouldBe(0);
		assessment.Preview.LeaveImpact.Measures["assignmentReferences"].Status.ShouldBe("calculated");
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task LeaveImpact_Rejects_Wrong_Year_Known_Policy_And_Stale_Version()
	{
		var context = await CreatePolicyContextAsync();
		var otherYear = await CreateAcademicYearAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var reader = GetRequiredService<LeavePolicyImpactPreviewReader>();
		var request = ImpactScope(context);
		request.AcademicYearId = otherYear;
		await Should.ThrowAsync<EntityNotFoundException>(() => reader.ReadAsync(request));
		request.AcademicYearId = context.AcademicYearId;
		request.ExpectedVersion = context.LeavePolicy.Version + 1;
		(await Should.ThrowAsync<BusinessException>(() => reader.ReadAsync(request))).Code.ShouldBe(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		await unit.CompleteAsync();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task LeaveImpact_Rejects_Invalid_Requested_Date_Scope(int invalidScope)
	{
		var context = await CreatePolicyContextAsync();
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var request = ImpactScope(context);
		if (invalidScope == 0) request.FromDate = new DateTime(2026, 9, 1);
		if (invalidScope == 1) request.AsOfDate = new DateTime(2026, 9, 1);
		if (invalidScope == 2) request.FromDate = new DateTime(2026, 3, 31);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<LeavePolicyImpactPreviewReader>().ReadAsync(request)))
			.Code.ShouldBe(HRErrorCodes.PolicyAssignmentRangeInvalid);
		await unit.CompleteAsync();
	}

	private static LeavePolicyImpactRequest ImpactScope(PolicyContext context) => new()
	{
		AcademicYearId = context.AcademicYearId, PolicyId = context.LeavePolicy.Id,
		AsOfDate = new DateTime(2026, 8, 15), FromDate = new DateTime(2026, 8, 1), ToDate = new DateTime(2026, 8, 31),
		Proposal = LeavePolicySnapshots.Capture(context.LeavePolicy)
	};

	private EmployeeLeave ImpactRequest(PolicyContext context, Guid employeeId, Guid ruleId, EmployeeLeaveStatusType status, DateTime date)
	{
		var request = EmployeeLeave.Create(Guid.NewGuid(), _currentTenant.Id, employeeId, context.LeavePolicy.Id, ruleId,
			LeavePaidType.Paid, new DateTimeOffset(date, TimeSpan.Zero), new DateTimeOffset(date, TimeSpan.Zero), "Synthetic impact request");
		request.UpdateLeaveStatus(status, null);
		if (status == EmployeeLeaveStatusType.Approved) request.SetApprovalPayrollSnapshot(LeavePaidType.Paid, 1m);
		return request;
	}
}
