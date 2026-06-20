using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveWorkCalendarUnitService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<WorkCalendarPolicy, Guid> _workCalendarPolicyRepository;
	private readonly IRepository<WorkCalendarHoliday, Guid> _workCalendarHolidayRepository;
	private readonly IRepository<PayrollPolicy, Guid> _payrollPolicyRepository;
	private readonly IRepository<AttendancePolicy, Guid> _attendancePolicyRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IRepository<LeavePolicyRule, Guid> _leavePolicyRuleRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly LeaveWorkCalendarUnitService _unitService;
	private readonly ReadSafeLeaveUnitResolver _readSafeUnitResolver;
	private readonly LeaveBalanceUnitService _balanceUnitService;
	private readonly LeaveValidationService _validationService;

	public LeaveWorkCalendarUnitService_Tests()
	{
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_workCalendarPolicyRepository = GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		_workCalendarHolidayRepository = GetRequiredService<IRepository<WorkCalendarHoliday, Guid>>();
		_payrollPolicyRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		_attendancePolicyRepository = GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		_leavePolicyRepository = GetRequiredService<ILeavePolicyRepository>();
		_leavePolicyRuleRepository = GetRequiredService<IRepository<LeavePolicyRule, Guid>>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_employeeLeaveRepository = GetRequiredService<IEmployeeLeaveRepository>();
		_unitService = GetRequiredService<LeaveWorkCalendarUnitService>();
		_readSafeUnitResolver = GetRequiredService<ReadSafeLeaveUnitResolver>();
		_balanceUnitService = GetRequiredService<LeaveBalanceUnitService>();
		_validationService = GetRequiredService<LeaveValidationService>();
	}

	[Fact]
	public async Task LeaveUnits_Should_Use_WorkCalendar_Holidays_For_List_And_Balance_Scenarios()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync());

		var units = await WithUnitOfWorkAsync(() => _unitService.CalculateUnitsAsync(
			setup.EmployeeId,
			new DateTime(2026, 6, 19),
			new DateTime(2026, 6, 30),
			halfDay: false,
			setup.AcademicYearId));

		units.ShouldBe(8m);

		await WithUnitOfWorkAsync(async () =>
		{
			await InsertLeaveAsync(
				setup,
				new DateTime(2026, 6, 19),
				new DateTime(2026, 6, 30),
				EmployeeLeaveStatusType.Approved,
				approvedUnits: 10m);
			await InsertLeaveAsync(
				setup,
				new DateTime(2026, 6, 19),
				new DateTime(2026, 6, 30),
				EmployeeLeaveStatusType.Pending);
		});

		var balances = await WithUnitOfWorkAsync(() =>
			_balanceUnitService.GetBalanceUnitsByPolicyAndPaidTypeAsync(
				setup.EmployeeId,
				setup.LeavePolicyId,
				[LeavePaidType.Paid]));

		balances[LeavePaidType.Paid].ApprovedDays.ShouldBe(10m);
		balances[LeavePaidType.Paid].PendingDays.ShouldBe(8m);
	}

	[Fact]
	public async Task LeaveUnits_Should_Count_HalfDay_With_WorkCalendar_Units()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync());

		var workingDayUnits = await WithUnitOfWorkAsync(() => _unitService.CalculateUnitsAsync(
			setup.EmployeeId,
			new DateTime(2026, 6, 22),
			new DateTime(2026, 6, 22),
			halfDay: true,
			setup.AcademicYearId));
		var holidayUnits = await WithUnitOfWorkAsync(() => _unitService.CalculateUnitsAsync(
			setup.EmployeeId,
			new DateTime(2026, 6, 19),
			new DateTime(2026, 6, 19),
			halfDay: true,
			setup.AcademicYearId));
		var multiDayHalfDayUnits = await WithUnitOfWorkAsync(() => _unitService.CalculateUnitsAsync(
			setup.EmployeeId,
			new DateTime(2026, 6, 22),
			new DateTime(2026, 6, 23),
			halfDay: true,
			setup.AcademicYearId));

		workingDayUnits.ShouldBe(0.5m);
		holidayUnits.ShouldBe(0m);
		multiDayHalfDayUnits.ShouldBe(0m);
	}

	[Fact]
	public async Task LeaveRequest_Should_Reject_MultiDay_HalfDay_Request()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync());

		var ex = await WithUnitOfWorkAsync(() => Should.ThrowAsync<BusinessException>(() =>
			_validationService.ValidateLeaveRequestAsync(
				setup.EmployeeId,
				LeavePaidType.Paid,
				new DateTime(2026, 6, 22),
				new DateTime(2026, 6, 23),
				halfDay: true,
				academicYearId: setup.AcademicYearId,
				enforceEmployeeTimingRules: false)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveHalfDayMustBeSingleDate);
	}

	[Fact]
	public async Task LeaveRequest_Should_Reject_Range_With_No_WorkCalendar_Working_Days()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync());

		var ex = await WithUnitOfWorkAsync(() => Should.ThrowAsync<BusinessException>(() =>
			_validationService.ValidateLeaveRequestAsync(
				setup.EmployeeId,
				LeavePaidType.Paid,
				new DateTime(2026, 6, 19),
				new DateTime(2026, 6, 20),
				academicYearId: setup.AcademicYearId,
				enforceEmployeeTimingRules: false)));

		ex.Code.ShouldBe(HRErrorCodes.LeaveNoBusinessDaysInRange);
	}

	[Fact]
	public async Task LeaveUnits_Should_Fail_When_Default_Payroll_Policy_Is_Missing()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync(createPayrollPolicy: false));

		var ex = await WithUnitOfWorkAsync(() => Should.ThrowAsync<BusinessException>(() =>
			_unitService.CalculateUnitsAsync(
				setup.EmployeeId,
				new DateTime(2026, 6, 19),
				new DateTime(2026, 6, 30),
				halfDay: false,
				setup.AcademicYearId)));

		ex.Code.ShouldBe(HRErrorCodes.DefaultPayrollPolicyRequired);
	}

	[Fact]
	public async Task LeaveReadUnits_Should_Fallback_When_Default_Payroll_Policy_Is_Missing()
	{
		var setup = await WithUnitOfWorkAsync(() => CreateSetupAsync(createPayrollPolicy: false));

		var units = await WithUnitOfWorkAsync(() => _readSafeUnitResolver.CalculateUnitsForReadAsync(
			setup.EmployeeId,
			new DateTime(2026, 6, 19),
			new DateTime(2026, 6, 30),
			halfDay: false,
			setup.AcademicYearId));

		units.ShouldBe(10m);
	}

	[Fact]
	public void LeaveReadUnits_Should_Use_Legacy_Display_Rules_For_Fallback()
	{
		ReadSafeLeaveUnitResolver.CountLegacyDisplayUnits(
			new DateTime(2026, 6, 22),
			new DateTime(2026, 6, 22),
			halfDay: true).ShouldBe(0.5m);
		ReadSafeLeaveUnitResolver.CountLegacyDisplayUnits(
			new DateTime(2026, 6, 22),
			new DateTime(2026, 6, 23),
			halfDay: true).ShouldBe(0m);
		ReadSafeLeaveUnitResolver.CountLegacyDisplayUnits(
			new DateTime(2026, 6, 19),
			new DateTime(2026, 6, 30),
			halfDay: false).ShouldBe(10m);
	}

	private async Task<LeaveUnitSetup> CreateSetupAsync(bool createPayrollPolicy = true)
	{
		var tenantId = _currentTenant.Id;
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var academicYear = await _academicYearManager.CreateAsync(
			$"Leave Unit AY {suffix}",
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31));
		await _academicYearRepository.InsertAsync(academicYear, autoSave: true);

		var calendar = new WorkCalendarPolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			$"Leave Unit Calendar {suffix}",
			HrPolicyStatus.Active,
			academicYear.StartDate,
			academicYear.EndDate,
			Settings.AttendanceWeeklyOffPattern.Sunday,
			[],
			WorkCalendarHolidaySyncMode.Snapshot);
		await _workCalendarPolicyRepository.InsertAsync(calendar, autoSave: true);
		await _workCalendarHolidayRepository.InsertAsync(new WorkCalendarHoliday(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			calendar.Id,
			new DateOnly(2026, 6, 19),
			new DateOnly(2026, 6, 20),
			"Local Payroll Holiday",
			WorkCalendarHolidaySource.Manual,
			calendarHolidayId: null,
			isIncluded: true,
			notes: null), autoSave: true);

		if (createPayrollPolicy)
		{
			var payrollPolicy = new PayrollPolicy(
				_guidGenerator.Create(),
				tenantId,
				academicYear.Id,
				calendar.Id,
				$"Leave Unit Payroll {suffix}",
				HrPolicyStatus.Active,
				isDefault: true,
				DayCalculationType.WorkingDays,
				manualDays: null,
				manualMonthDays: [],
				SaturdayWorkPolicy.FullDay,
				holidayOverrides: []);
			await _payrollPolicyRepository.InsertAsync(payrollPolicy, autoSave: true);
		}

		await _attendancePolicyRepository.InsertAsync(new AttendancePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			calendar.Id,
			$"Leave Unit Attendance {suffix}",
			HrPolicyStatus.Active,
			isDefault: true,
			AttendanceCaptureMode.Integrated,
			fullDayThresholdPercentage: 75,
			halfDayThresholdPercentage: 50,
			absentThresholdPercentage: 0,
			Settings.AttendanceMissingPunchBehavior.RequiresReview,
			manualCorrectionAllowed: true,
			graceTime: 0,
			maxLateAllowedPerMonth: 0,
			Settings.AttendancePenaltyType.None,
			penaltyCountThreshold: 0,
			earlyExitEnabled: false,
			earlyExitGraceTime: 0,
			overtimeEnabled: false,
			overtimeRate: null,
			minOvertimeMinutes: null,
			Settings.AttendanceHolidayWorkBehavior.CompOff,
			payrollPreviewEnabled: true), autoSave: true);

		var leavePolicy = new LeavePolicy(
			_guidGenerator.Create(),
			tenantId,
			academicYear.Id,
			$"Leave Unit Policy {suffix}",
			academicYear.StartDate,
			academicYear.EndDate,
			HrPolicyStatus.Active,
			isDefault: true);
		await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: true);

		var leaveRule = new LeavePolicyRule(
			_guidGenerator.Create(),
			tenantId,
			leavePolicy.Id,
			LeavePaidType.Paid,
			LeaveEntitlementMode.Limited,
			LeaveAccrualPolicyType.Yearly,
			leaveCount: 30,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance: false,
			isActive: true);
		await _leavePolicyRuleRepository.InsertAsync(leaveRule, autoSave: true);

		var roleId = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(
			roleId,
			$"leave-unit-role-{suffix}",
			tenantId), autoSave: true);
		var employee = await _employeeRepository.InsertAsync(new Employee(
			_guidGenerator.Create(),
			tenantId,
			userId: null,
			employeeCode: $"LUT{suffix}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Unit",
				FirstName: "Leave",
				MiddleName: null,
				ShortName: "LU",
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
				Email: $"leave-unit-{suffix}@test.local",
				PanNo: UniquePan(),
				AdharNo: UniqueAdhar(),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: null,
				Designation: null,
				DateOfJoining: new DateOnly(2026, 4, 1),
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
			workExperiences: null), autoSave: true);

		return new LeaveUnitSetup(academicYear.Id, employee.Id, leavePolicy.Id, leaveRule.Id);
	}

	private async Task InsertLeaveAsync(
		LeaveUnitSetup setup,
		DateTime startDate,
		DateTime endDate,
		EmployeeLeaveStatusType status,
		decimal? approvedUnits = null)
	{
		var leave = EmployeeLeave.Create(
			_guidGenerator.Create(),
			_currentTenant.Id,
			setup.EmployeeId,
			setup.LeavePolicyId,
			setup.LeavePolicyRuleId,
			LeavePaidType.Paid,
			new DateTimeOffset(startDate),
			new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1)),
			"Leave unit test",
			false);
		if (approvedUnits.HasValue)
		{
			leave.SetApprovalPayrollSnapshot(LeavePaidType.Paid, approvedUnits.Value);
		}

		leave.UpdateLeaveStatus(status, rejectionReason: null);
		await _employeeLeaveRepository.InsertAsync(leave, autoSave: true);
	}

	private sealed record LeaveUnitSetup(
		Guid AcademicYearId,
		Guid EmployeeId,
		Guid LeavePolicyId,
		Guid LeavePolicyRuleId);
}
