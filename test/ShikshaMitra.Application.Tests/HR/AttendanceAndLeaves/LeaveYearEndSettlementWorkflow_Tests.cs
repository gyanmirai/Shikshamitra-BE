using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveYearEndSettlementWorkflow_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly LeaveYearEndSettlementWorkflow _workflow;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeeLeaveRepository _leaveRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IRepository<WorkCalendarPolicy, Guid> _workCalendarPolicyRepository;
	private readonly IRepository<PayrollPolicy, Guid> _payrollPolicyRepository;
	private readonly IRepository<AttendancePolicy, Guid> _attendancePolicyRepository;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly IRepository<LeavePolicyRule, Guid> _leavePolicyRuleRepository;
	private readonly IRepository<PayrollAdjustment, Guid> _payrollAdjustmentRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _snapshotRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public LeaveYearEndSettlementWorkflow_Tests()
	{
		_workflow = GetRequiredService<LeaveYearEndSettlementWorkflow>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_leaveRepository = GetRequiredService<IEmployeeLeaveRepository>();
		_payrollComponentRepository = GetRequiredService<IEmployeePayrollComponentRepository>();
		_workCalendarPolicyRepository = GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		_payrollPolicyRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		_attendancePolicyRepository = GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		_leavePolicyRepository = GetRequiredService<IRepository<LeavePolicy, Guid>>();
		_leavePolicyRuleRepository = GetRequiredService<IRepository<LeavePolicyRule, Guid>>();
		_payrollAdjustmentRepository = GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		_snapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Preview_Should_Carry_First_Then_Encash_Remaining_Unused_Days()
	{
		var scenario = await SeedScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var run = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			var item = run.Items.Single(x => x.EmployeeId == scenario.EmployeeId);

			run.TotalItems.ShouldBe(1);
			run.BlockedItems.ShouldBe(0);
			item.AccruedDays.ShouldBe(12m);
			item.ApprovedUsedDays.ShouldBe(3m);
			item.PendingDays.ShouldBe(0m);
			item.UnusedDays.ShouldBe(9m);
			item.CarriedDays.ShouldBe(4m);
			item.TargetLeavePolicyRuleId.ShouldBe(scenario.TargetPaidRuleId);
			item.EncashedDays.ShouldBe(5m);
			item.ForfeitedDays.ShouldBe(0m);
			item.PayrollMonth.ShouldBe("2027-03");
			item.Amount.ShouldBe(5000m);
			item.IssueCodes.ShouldBeEmpty();
			run.TotalCarriedDays.ShouldBe(4m);
			run.TotalEncashedDays.ShouldBe(5m);
			run.TotalEncashmentAmount.ShouldBe(5000m);
		}
	}

	[Fact]
	public async Task Preview_Should_Block_Item_When_Source_Rule_Has_Pending_Leave()
	{
		var scenario = await SeedScenarioAsync(includePendingLeave: true);

		using (_currentTenant.Change(scenario.TenantId))
		{
			var run = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			var item = run.Items.Single(x => x.EmployeeId == scenario.EmployeeId);

			run.BlockedItems.ShouldBe(1);
			item.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Blocked));
			item.PendingDays.ShouldBe(1m);
			item.IssueCodes.ShouldContain(LeaveYearEndSettlementIssueCodes.PendingLeave);
		}
	}

	[Fact]
	public async Task Finalize_Should_Post_System_Generated_Encashment_Adjustment_Once()
	{
		var scenario = await SeedScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var preview = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			var finalized = await WithUnitOfWorkAsync(() => _workflow.FinalizeAsync(preview.Id));
			await WithUnitOfWorkAsync(() => _workflow.FinalizeAsync(preview.Id));

			await WithUnitOfWorkAsync(async () =>
			{
				var item = finalized.Items.Single(x => x.EmployeeId == scenario.EmployeeId);
				item.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Finalized));
				item.PayrollAdjustmentId.ShouldNotBeNull();

				var adjustments = await _payrollAdjustmentRepository.GetListAsync(x =>
					x.SourceType == PayrollAdjustmentSourceTypes.LeaveYearEndEncashment &&
					x.SourceId == item.Id);

				adjustments.Count.ShouldBe(1);
				var adjustment = adjustments[0];
				adjustment.MonthYear.ShouldBe("2027-03");
				adjustment.EmployeeId.ShouldBe(scenario.EmployeeId);
				adjustment.Name.ShouldBe("Leave Encashment");
				adjustment.Amount.ShouldBe(5000m);
				adjustment.IsEarning.ShouldBeTrue();
				adjustment.IsSystemGenerated.ShouldBeTrue();
			});
		}
	}

	[Fact]
	public async Task Finalize_Should_Keep_Run_Draft_And_Block_Item_When_Payroll_Month_Is_Locked()
	{
		var scenario = await SeedScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var preview = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			await WithUnitOfWorkAsync(async () =>
			{
				var lockedSnapshot = new PayrollRunItemSnapshot(
					_guidGenerator.Create(),
					scenario.TenantId,
					"2027-03",
					scenario.EmployeeId);
				lockedSnapshot.MarkLocked();
				await _snapshotRepository.InsertAsync(lockedSnapshot, autoSave: true);
			});

			var ex = await Should.ThrowAsync<BusinessException>(() =>
				WithUnitOfWorkAsync(() => _workflow.FinalizeAsync(preview.Id)));
			ex.Code.ShouldBe(HRErrorCodes.LeaveYearEndSettlementFinalizeBlocked);

			var run = await WithUnitOfWorkAsync(() => _workflow.GetAsync(preview.Id));
			run.Status.ShouldBe(nameof(LeaveYearEndSettlementRunStatus.Draft));
			var item = run.Items.Single(x => x.EmployeeId == scenario.EmployeeId);
			item.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Blocked));
			item.IssueCodes.ShouldContain(LeaveYearEndSettlementIssueCodes.PayrollLocked);
		}
	}

	[Fact]
	public async Task Finalize_Should_Block_All_Items_And_Post_Nothing_When_Any_Item_Has_Issue()
	{
		var scenario = await SeedScenarioAsync(includeBlockedEmployee: true);

		using (_currentTenant.Change(scenario.TenantId))
		{
			var preview = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			preview.Items.Count.ShouldBe(2);
			preview.Items.Count(x => x.Status == nameof(LeaveYearEndSettlementItemStatus.Blocked)).ShouldBe(1);

			var ex = await Should.ThrowAsync<BusinessException>(() =>
				WithUnitOfWorkAsync(() => _workflow.FinalizeAsync(preview.Id)));
			ex.Code.ShouldBe(HRErrorCodes.LeaveYearEndSettlementFinalizeBlocked);

			var run = await WithUnitOfWorkAsync(() => _workflow.GetAsync(preview.Id));
			run.Status.ShouldBe(nameof(LeaveYearEndSettlementRunStatus.Draft));
			run.Items.Single(x => x.EmployeeId == scenario.EmployeeId)
				.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Draft));
			run.Items.Single(x => x.EmployeeId == scenario.BlockedEmployeeId)
				.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Blocked));

			await WithUnitOfWorkAsync(async () =>
			{
				var adjustments = await _payrollAdjustmentRepository.GetListAsync(x =>
					x.SourceType == PayrollAdjustmentSourceTypes.LeaveYearEndEncashment);
				adjustments.ShouldBeEmpty();
			});
		}
	}

	[Fact]
	public async Task Finalize_Should_Recalculate_Draft_Before_Posting()
	{
		var scenario = await SeedScenarioAsync();

		using (_currentTenant.Change(scenario.TenantId))
		{
			var preview = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			preview.Items.Single(x => x.EmployeeId == scenario.EmployeeId).Amount.ShouldBe(5000m);

			await WithUnitOfWorkAsync(() => InsertLeaveAsync(
				scenario.TenantId,
				scenario.EmployeeId,
				scenario.SourceLeavePolicyId,
				scenario.SourcePaidRuleId,
				new DateTime(2026, 5, 12),
				new DateTime(2026, 5, 12),
				EmployeeLeaveStatusType.Approved,
				approvedUnits: 1m));

			var finalized = await WithUnitOfWorkAsync(() => _workflow.FinalizeAsync(preview.Id));
			var item = finalized.Items.Single(x => x.EmployeeId == scenario.EmployeeId);
			item.ApprovedUsedDays.ShouldBe(4m);
			item.CarriedDays.ShouldBe(4m);
			item.EncashedDays.ShouldBe(4m);
			item.Amount.ShouldBe(4000m);

			await WithUnitOfWorkAsync(async () =>
			{
				var adjustment = (await _payrollAdjustmentRepository.GetListAsync(x =>
					x.SourceType == PayrollAdjustmentSourceTypes.LeaveYearEndEncashment &&
					x.SourceId == item.Id)).Single();
				adjustment.Amount.ShouldBe(4000m);
			});
		}
	}

	[Fact]
	public async Task Preview_Should_Encash_All_Unused_When_Target_Paid_Rule_Is_Missing_And_Encashment_Is_Enabled()
	{
		var scenario = await SeedScenarioAsync(createTargetPaidRule: false);

		using (_currentTenant.Change(scenario.TenantId))
		{
			var run = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			var item = run.Items.Single(x => x.EmployeeId == scenario.EmployeeId);

			item.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Draft));
			item.TargetLeavePolicyRuleId.ShouldBeNull();
			item.IssueCodes.ShouldBeEmpty();
			item.CarriedDays.ShouldBe(0m);
			item.EncashedDays.ShouldBe(9m);
			item.Amount.ShouldBe(9000m);
		}
	}

	[Fact]
	public async Task Preview_Should_Block_When_Target_Paid_Rule_Is_Missing_And_Encashment_Is_Disabled()
	{
		var scenario = await SeedScenarioAsync(sourceEncashable: false, createTargetPaidRule: false);

		using (_currentTenant.Change(scenario.TenantId))
		{
			var run = await WithUnitOfWorkAsync(() => _workflow.PreviewAsync(scenario.SourceAcademicYearId));
			var item = run.Items.Single(x => x.EmployeeId == scenario.EmployeeId);

			item.Status.ShouldBe(nameof(LeaveYearEndSettlementItemStatus.Blocked));
			item.TargetLeavePolicyRuleId.ShouldBeNull();
			item.IssueCodes.ShouldContain(LeaveYearEndSettlementIssueCodes.MissingTargetPaidRule);
			item.CarriedDays.ShouldBe(0m);
			item.EncashedDays.ShouldBe(0m);
		}
	}

	private async Task<YearEndSettlementScenario> SeedScenarioAsync(
		bool includePendingLeave = false,
		bool includeBlockedEmployee = false,
		bool sourceEncashable = true,
		bool createTargetPaidRule = true)
	{
		var tenantId = _guidGenerator.Create();

		using (_currentTenant.Change(tenantId))
		{
			return await WithUnitOfWorkAsync(async () =>
			{
				var sourceYear = await CreateAcademicYearAsync(
					"Leave Settlement Source",
					new DateTime(2026, 4, 1),
					new DateTime(2027, 3, 31));
				var targetYear = await CreateAcademicYearAsync(
					"Leave Settlement Target",
					new DateTime(2027, 4, 1),
					new DateTime(2028, 3, 31));

				var sourcePolicies = await CreatePolicyBundleAsync(
					tenantId,
					sourceYear,
					leaveCount: 12,
					carryForward: true,
					carryForwardLimit: 4,
					encashable: sourceEncashable,
					createPaidRule: true);
				var targetPolicies = await CreatePolicyBundleAsync(
					tenantId,
					targetYear,
					leaveCount: 12,
					carryForward: true,
					carryForwardLimit: 4,
					encashable: true,
					createPaidRule: createTargetPaidRule);

				var employee = await CreateEmployeeAsync(tenantId, new DateOnly(2026, 4, 1));
				await _payrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
					_guidGenerator.Create(),
					employee.Id,
					"Basic",
					null,
					"Year-end settlement gross basis",
					31000m,
					isFixed: true,
					isTemp: false,
					applicableMonth: null,
					SalaryComponentType.Fixed,
					SalaryComponentDependency.None,
					isEarning: true,
					usesMasterValue: false), autoSave: true);

				await InsertLeaveAsync(
					tenantId,
					employee.Id,
					sourcePolicies.LeavePolicyId,
					sourcePolicies.PaidRuleId!.Value,
					new DateTime(2026, 5, 4),
					new DateTime(2026, 5, 6),
					EmployeeLeaveStatusType.Approved,
					approvedUnits: 3m);
				await InsertLeaveAsync(
					tenantId,
					employee.Id,
					sourcePolicies.LeavePolicyId,
					sourcePolicies.PaidRuleId!.Value,
					new DateTime(2026, 5, 7),
					new DateTime(2026, 5, 7),
					EmployeeLeaveStatusType.Rejected);
				await InsertLeaveAsync(
					tenantId,
					employee.Id,
					sourcePolicies.LeavePolicyId,
					sourcePolicies.PaidRuleId!.Value,
					new DateTime(2026, 5, 8),
					new DateTime(2026, 5, 8),
					EmployeeLeaveStatusType.Cancelled);
				if (includePendingLeave)
				{
					await InsertLeaveAsync(
						tenantId,
						employee.Id,
						sourcePolicies.LeavePolicyId,
						sourcePolicies.PaidRuleId!.Value,
					new DateTime(2026, 5, 11),
					new DateTime(2026, 5, 11),
					EmployeeLeaveStatusType.Pending);
				}

				Guid? blockedEmployeeId = null;
				if (includeBlockedEmployee)
				{
					var blockedEmployee = await CreateEmployeeAsync(tenantId, new DateOnly(2026, 4, 1));
					blockedEmployeeId = blockedEmployee.Id;
					await _payrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
						_guidGenerator.Create(),
						blockedEmployee.Id,
						"Basic",
						null,
						"Year-end settlement gross basis",
						31000m,
						isFixed: true,
						isTemp: false,
						applicableMonth: null,
						SalaryComponentType.Fixed,
						SalaryComponentDependency.None,
						isEarning: true,
						usesMasterValue: false), autoSave: true);
					await InsertLeaveAsync(
						tenantId,
						blockedEmployee.Id,
						sourcePolicies.LeavePolicyId,
						sourcePolicies.PaidRuleId!.Value,
						new DateTime(2026, 5, 11),
						new DateTime(2026, 5, 11),
						EmployeeLeaveStatusType.Pending);
				}

				return new YearEndSettlementScenario(
					tenantId,
					sourceYear.Id,
					targetYear.Id,
					employee.Id,
					blockedEmployeeId,
					sourcePolicies.LeavePolicyId,
					sourcePolicies.PaidRuleId!.Value,
					targetPolicies.PaidRuleId);
			});
		}
	}

	private async Task<AcademicYear> CreateAcademicYearAsync(string prefix, DateTime startDate, DateTime endDate)
	{
		var academicYear = (AcademicYear)Activator.CreateInstance(
			typeof(AcademicYear),
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
			binder: null,
			args:
			[
				_guidGenerator.Create(),
				_currentTenant.Id,
				$"{prefix} {Guid.NewGuid():N}"[..50],
				startDate,
				endDate
			],
			culture: null)!;

		await _academicYearRepository.InsertAsync(academicYear, autoSave: true);
		return academicYear;
	}

	private async Task<PolicyBundle> CreatePolicyBundleAsync(
		Guid tenantId,
		AcademicYear academicYear,
		int leaveCount,
		bool carryForward,
		int? carryForwardLimit,
		bool encashable,
		bool createPaidRule = true)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var calendar = new WorkCalendarPolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			$"Settlement Calendar {suffix}",
			HrPolicyStatus.Active,
			academicYear.StartDate,
			academicYear.EndDate,
			AttendanceWeeklyOffPattern.Sunday,
			[],
			WorkCalendarHolidaySyncMode.Snapshot);
		await _workCalendarPolicyRepository.InsertAsync(calendar, autoSave: true);

		await _payrollPolicyRepository.InsertAsync(new PayrollPolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			calendar.Id,
			$"Settlement Payroll {suffix}",
			HrPolicyStatus.Active,
			isDefault: true,
			DayCalculationType.TotalMonthDays,
			manualDays: null,
			manualMonthDays: [],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			lockedMonthSettings: []), autoSave: true);

		await _attendancePolicyRepository.InsertAsync(new AttendancePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			calendar.Id,
			$"Settlement Attendance {suffix}",
			HrPolicyStatus.Active,
			isDefault: true,
			AttendanceCaptureMode.Integrated,
			fullDayThresholdPercentage: 75,
			halfDayThresholdPercentage: 50,
			absentThresholdPercentage: 0,
			AttendanceMissingPunchBehavior.RequiresReview,
			manualCorrectionAllowed: true,
			graceTime: 0,
			maxLateAllowedPerMonth: 0,
			AttendancePenaltyType.None,
			penaltyCountThreshold: 0,
			earlyExitEnabled: false,
			earlyExitGraceTime: 0,
			overtimeEnabled: false,
			overtimeRate: null,
			minOvertimeMinutes: null,
			AttendanceHolidayWorkBehavior.CompOff,
			payrollPreviewEnabled: true), autoSave: true);

		var leavePolicy = new LeavePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			$"Settlement Leave {suffix}",
			academicYear.StartDate,
			academicYear.EndDate,
			HrPolicyStatus.Active,
			isDefault: true);
		await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: true);

		Guid? paidRuleId = null;
		if (createPaidRule)
		{
			var paidRule = new LeavePolicyRule(
				_guidGenerator.Create(),
				tenantId,
				leavePolicy.Id,
				LeavePaidType.Paid,
				LeaveEntitlementMode.Limited,
				LeaveAccrualPolicyType.Yearly,
				leaveCount,
				carryForward,
				carryForwardLimit,
				encashable,
				encashable ? LeaveEncashmentTiming.EndOfYear : null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: false,
				isActive: true);
			await _leavePolicyRuleRepository.InsertAsync(paidRule, autoSave: true);
			paidRuleId = paidRule.Id;
		}

		var unpaidRule = new LeavePolicyRule(
			_guidGenerator.Create(),
			tenantId,
			leavePolicy.Id,
			LeavePaidType.Unpaid,
			LeaveEntitlementMode.Unlimited,
			LeaveAccrualPolicyType.Yearly,
			null,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance: true,
			isActive: true);
		await _leavePolicyRuleRepository.InsertAsync(unpaidRule, autoSave: true);

		return new PolicyBundle(leavePolicy.Id, paidRuleId);
	}

	private async Task<Employee> CreateEmployeeAsync(Guid tenantId, DateOnly dateOfJoining)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var roleId = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(roleId, $"leave-settlement-{suffix[..8]}", tenantId), autoSave: true);

		var employee = new Employee(
			_guidGenerator.Create(),
			tenantId,
			userId: null,
			employeeCode: $"LYE{suffix[..10]}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Settlement",
				FirstName: $"Leave{suffix[..8]}",
				MiddleName: null,
				ShortName: "LYE",
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: new DateOnly(1990, 1, 1),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"leavesettlement{suffix[..8]}@test.local",
				PanNo: UniquePan(),
				AdharNo: UniqueAdhar(),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: null,
				Designation: null,
				DateOfJoining: dateOfJoining,
				EmployeeGroup: null,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private async Task InsertLeaveAsync(
		Guid tenantId,
		Guid employeeId,
		Guid leavePolicyId,
		Guid leavePolicyRuleId,
		DateTime startDate,
		DateTime endDate,
		EmployeeLeaveStatusType status,
		decimal? approvedUnits = null)
	{
		var leave = EmployeeLeave.Create(
			_guidGenerator.Create(),
			tenantId,
			employeeId,
			leavePolicyId,
			leavePolicyRuleId,
			LeavePaidType.Paid,
			new DateTimeOffset(startDate),
			new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1)),
			"Year-end settlement test leave");
		if (approvedUnits.HasValue)
		{
			leave.SetApprovalPayrollSnapshot(LeavePaidType.Paid, approvedUnits.Value);
		}

		leave.UpdateLeaveStatus(status, status == EmployeeLeaveStatusType.Rejected ? "Rejected" : null);
		await _leaveRepository.InsertAsync(leave, autoSave: true);
	}

	private sealed record YearEndSettlementScenario(
		Guid TenantId,
		Guid SourceAcademicYearId,
		Guid TargetAcademicYearId,
		Guid EmployeeId,
		Guid? BlockedEmployeeId,
		Guid SourceLeavePolicyId,
		Guid SourcePaidRuleId,
		Guid? TargetPaidRuleId);

	private sealed record PolicyBundle(Guid LeavePolicyId, Guid? PaidRuleId);
}
