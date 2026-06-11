using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ISalarySlipAppService _salarySlipAppService;
	private readonly IRunPayrollAppService _runPayrollAppService;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfo;
	private readonly IEmployeePayrollAppService _employeePayrollAppService;
	private readonly IEarningComponentAppService _earningComponentAppService;
	private readonly IDeductionComponentAppService _deductionComponentAppService;
	private readonly IWorkCalendarPolicyAppService _workCalendars;
	private readonly IPayrollPolicyAppService _payrollPolicies;
	private readonly IAttendancePolicyAppService _attendancePolicies;
	private readonly IHrPolicyAssignmentAppService _assignments;
	private readonly SalarySlipAccessService _salarySlipAccessService;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IdentityUserManager _userManager;
	private readonly IUserScopeService _userScopeService;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly ICurrentTenant _currentTenant;
	private readonly IUnitOfWorkManager _unitOfWorkManager;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IClock _clock;

	public SalarySlipAppService_Tests()
	{
		_salarySlipAppService = GetRequiredService<ISalarySlipAppService>();
		_runPayrollAppService = GetRequiredService<IRunPayrollAppService>();
		_employeePersonalInfo = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeePayrollAppService = GetRequiredService<IEmployeePayrollAppService>();
		_earningComponentAppService = GetRequiredService<IEarningComponentAppService>();
		_deductionComponentAppService = GetRequiredService<IDeductionComponentAppService>();
		_workCalendars = GetRequiredService<IWorkCalendarPolicyAppService>();
		_payrollPolicies = GetRequiredService<IPayrollPolicyAppService>();
		_attendancePolicies = GetRequiredService<IAttendancePolicyAppService>();
		_assignments = GetRequiredService<IHrPolicyAssignmentAppService>();
		_salarySlipAccessService = GetRequiredService<SalarySlipAccessService>();
		_attendanceRepository = GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_payrollRunItemSnapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_userManager = GetRequiredService<IdentityUserManager>();
		_userScopeService = GetRequiredService<IUserScopeService>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_clock = GetRequiredService<IClock>();
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GetSalarySlipAsync(Guid.NewGuid(), "2026-04"));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public async Task GenerateSalarySlipAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var input = new GenerateSalarySlipInput
		{
			EmployeeId = Guid.NewGuid(),
			MonthYear = "2026-04",
			ForceRegenerate = false
		};

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GenerateSalarySlipAsync(input));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public async Task GenerateSalarySlipPdfAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GenerateSalarySlipPdfAsync(Guid.NewGuid(), "2026-04"));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public async Task SalarySlipAccess_Should_Allow_Assigned_Employee_Without_Attendance_Rows()
	{
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var employee = await CreateEmployeeAsync(organizationId: organizationId);
		var userId = Guid.NewGuid();
		await SeedPayrollStageScopeAsync(userId, organizationId);

		using (_currentPrincipalAccessor.Change(BuildPayrollScopePrincipal(userId)))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await _salarySlipAccessService.EnsureEmployeeScopeAllowedAsync(employee.Id);
			});
		}
	}

	[Fact]
	public async Task SalarySlipAccess_Should_Allow_Assigned_Employee_When_Attendance_Stage_Is_Stale()
	{
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var employee = await CreateEmployeeAsync(organizationId: organizationId);
		var staleStageId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		await SeedPayrollStageScopeAsync(userId, organizationId);
		await SeedAttendanceScopeAsync(employee.Id, staleStageId, "2026-04");

		using (_currentPrincipalAccessor.Change(BuildPayrollScopePrincipal(userId)))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await _salarySlipAccessService.EnsureEmployeeScopeAllowedAsync(employee.Id);
			});
		}
	}

	[Fact]
	public async Task SalarySlipAccess_Should_Deny_When_Only_Stale_Attendance_Stage_Is_InScope()
	{
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var employee = await CreateEmployeeAsync(organizationId: organizationId);
		var staleStageId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		await SeedPayrollStageScopeAsync(userId, staleStageId);
		await SeedAttendanceScopeAsync(employee.Id, staleStageId, "2026-04");

		using (_currentPrincipalAccessor.Change(BuildPayrollScopePrincipal(userId)))
		{
			var ex = await Should.ThrowAsync<BusinessException>(async () =>
				await WithUnitOfWorkAsync(async () =>
				{
					await _salarySlipAccessService.EnsureEmployeeScopeAllowedAsync(employee.Id);
				}));

			ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		organizationId.ShouldNotBe(staleStageId);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Refresh_Draft_From_Current_Unlocked_Payroll_Policy()
	{
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var oldSlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
			{
				EmployeeId = employee.Id,
				MonthYear = monthYear
			});
		oldSlip.PayrollBasisDays.ShouldBe(DaysInMonth(monthYear));
		oldSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		var preview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var payrollRow = preview.Items.Single(x => x.EmployeeId == employee.Id);

		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		refreshedSlip.PayrollDayCalculationMode.ShouldBe("working-days");
		refreshedSlip.PayrollBasisDays.ShouldBe((decimal) payrollRow.BasisDays);
		refreshedSlip.PayableDays.ShouldBe((decimal) payrollRow.PayableDays);
		refreshedSlip.NetSalary.ShouldBe((decimal) payrollRow.NetPay);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Preserve_Draft_From_Locked_Payroll_Snapshot()
	{
		var context = await CreatePayrollPolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, "2026-01");
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, "2026-01");

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var oldSlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
			{
				EmployeeId = employee.Id,
				MonthYear = "2026-01"
			});
		oldSlip.PayrollBasisDays.ShouldBe(31m);
		oldSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");

		await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = "2026-01",
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var snapshot = await GetPayrollSnapshotAsync(employee.Id, "2026-01");
		snapshot.ShouldNotBeNull();
		snapshot.MarkLocked();
		await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: true);

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = "2026-01",
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});

		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, "2026-01");

		refreshedSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");
		refreshedSlip.PayrollBasisDays.ShouldBe(snapshot.BasisDays);
		refreshedSlip.PayableDays.ShouldBe(snapshot.PayableDays);
		refreshedSlip.NetSalary.ShouldBe(snapshot.NetPay);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Preserve_Historical_Draft_Deduction_After_Master_Delete()
	{
		var monthYear = HistoricalPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		var basic = await CreateEarningComponentAsync($"Historical Basic {Guid.NewGuid():N}", 31000m);
		var deduction = await CreateDeductionComponentAsync($"Historical Deduction {Guid.NewGuid():N}", 3100m);
		await SaveSalaryComponentsAsync(employee.Id, [basic], [deduction]);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var original = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		var originalDeduction = original.Deductions.Single(x => x.Name == deduction.Name);
		var originalTotalDeduction = original.TotalDeduction;
		var originalNetSalary = original.NetSalary;

		await DeleteDeductionComponentWithPreviewAsync(deduction.Id);
		var refreshed = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		refreshed.Deductions.ShouldContain(x => x.Name == deduction.Name && x.Amount == originalDeduction.Amount);
		refreshed.TotalDeduction.ShouldBe(originalTotalDeduction);
		refreshed.NetSalary.ShouldBe(originalNetSalary);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Preserve_Historical_Draft_Earning_After_Master_Delete()
	{
		var monthYear = HistoricalPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		var basic = await CreateEarningComponentAsync($"Historical Basic {Guid.NewGuid():N}", 31000m);
		var allowance = await CreateEarningComponentAsync($"Historical Allowance {Guid.NewGuid():N}", 6200m);
		await SaveSalaryComponentsAsync(employee.Id, [basic, allowance], []);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var original = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		var originalAllowance = original.Earnings.Single(x => x.Name == allowance.Name);
		var originalTotalEarning = original.TotalEarning;
		var originalNetSalary = original.NetSalary;

		await DeleteEarningComponentWithPreviewAsync(allowance.Id);
		var refreshed = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		refreshed.Earnings.ShouldContain(x => x.Name == allowance.Name && x.Amount == originalAllowance.Amount);
		refreshed.TotalEarning.ShouldBe(originalTotalEarning);
		refreshed.NetSalary.ShouldBe(originalNetSalary);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Not_Refresh_Historical_Draft_From_Unlocked_Snapshot()
	{
		var monthYear = HistoricalPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var oldSlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		oldSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");

		await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var snapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		snapshot.ShouldNotBeNull();
		snapshot.IsLocked.ShouldBeFalse();

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});

		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		refreshedSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");
		refreshedSlip.PayrollBasisDays.ShouldBe(oldSlip.PayrollBasisDays);
		refreshedSlip.NetSalary.ShouldBe(oldSlip.NetSalary);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Include_Overtime_Earning_From_Attendance()
	{
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(
			PayrollYear(monthYear),
			overtimeEnabled: true,
			overtimeRate: "2.0x",
			minOvertimeMinutes: 30);
		var shiftId = await CreateShiftAsync(new TimeOnly(7, 0), new TimeOnly(17, 30));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear), shiftId);
		await SeedAttendanceScopeAsync(
			employee.Id,
			context.OrganizationId,
			monthYear,
			new TimeOnly(7, 0),
			new TimeOnly(19, 0));
		await SaveSalaryComponentsAsync(employee.Id, isTemp: false);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);

		var payrollRow = await EnsurePayrollSnapshotAsync(employee.Id, monthYear);
		var overtime = payrollRow.EarningComponents.Single(x => x.Name == "Overtime Pay");
		var regularSalary = Math.Round(31000m / (decimal)payrollRow.BasisDays, 2, MidpointRounding.AwayFromZero);
		var expectedOvertime = CalculateExpectedOvertime(
			monthlyGrossSalary: 31000m,
			basisDays: (decimal)payrollRow.BasisDays,
			shiftStart: new TimeOnly(7, 0),
			shiftEnd: new TimeOnly(17, 30),
			overtimeMinutes: 90m,
			multiplier: 2m);
		overtime.Amount.ShouldBe((double)expectedOvertime);
		payrollRow.GrossSalary.ShouldBe((double)(regularSalary + expectedOvertime));
		payrollRow.NetPay.ShouldBe((double)(regularSalary + expectedOvertime));

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var salarySlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});

		salarySlip.Earnings.ShouldContain(x => x.Name == "Overtime Pay" && x.Amount == expectedOvertime);
		salarySlip.TotalEarning.ShouldBe(regularSalary + expectedOvertime);
		salarySlip.NetSalary.ShouldBe(regularSalary + expectedOvertime);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Not_Add_Overtime_Earning_Below_Threshold()
	{
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(
			PayrollYear(monthYear),
			overtimeEnabled: true,
			overtimeRate: "2.0x",
			minOvertimeMinutes: 30);
		var shiftId = await CreateShiftAsync(new TimeOnly(7, 0), new TimeOnly(17, 30));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear), shiftId);
		await SeedAttendanceScopeAsync(
			employee.Id,
			context.OrganizationId,
			monthYear,
			new TimeOnly(7, 0),
			new TimeOnly(17, 45));
		await SaveSalaryComponentsAsync(employee.Id, isTemp: false);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);

		var payrollRow = await EnsurePayrollSnapshotAsync(employee.Id, monthYear);
		var regularSalary = Math.Round(31000m / (decimal)payrollRow.BasisDays, 2, MidpointRounding.AwayFromZero);

		payrollRow.EarningComponents.ShouldNotContain(x => x.Name == "Overtime Pay");
		payrollRow.GrossSalary.ShouldBe((double)regularSalary);
		payrollRow.NetPay.ShouldBe((double)regularSalary);
	}

	private async Task<PayrollPolicyContext> CreatePayrollPolicyContextAsync(
		int year = 2026,
		bool overtimeEnabled = false,
		string? overtimeRate = null,
		int? minOvertimeMinutes = null)
	{
		var academicYearId = await CreateAcademicYearAsync(year);
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var calendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"SalarySlip Calendar {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = new DateTime(year, 1, 1),
			EffectiveTo = new DateTime(year, 12, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});
		var workingDays = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Working Days Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var totalMonth = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Total Month Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var attendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"SalarySlip Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			OvertimeEnabled = overtimeEnabled,
			OvertimeRate = overtimeRate,
			MinOvertimeMinutes = minOvertimeMinutes,
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		});

		return new PayrollPolicyContext(year, academicYearId, organizationId, workingDays, totalMonth, attendance);
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(
		DateOnly? dateOfJoining = null,
		Guid? shiftId = null,
		Guid? organizationId = null)
	{
		var roleId = await EnsureRoleExistsAsync();
		var resolvedShiftId = shiftId ?? await EnsureShiftExistsAsync();
		var resolvedOrganizationId = organizationId
			?? await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		return await _employeePersonalInfo.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = "Payslip",
			LastName = "Policy",
			MobileNo = "9876543210",
			Email = $"payslip.policy.{_guidGenerator.Create():N}@test.com",
			PanNo = UniqueEmployeePan(),
			AdharNo = UniqueEmployeeAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
			GenderType = GenderType.Male,
			DateOfJoining = dateOfJoining ?? DateOnly.FromDateTime(new DateTime(2026, 1, 1)),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = resolvedShiftId,
			OrganizationIds = [resolvedOrganizationId]
		});
	}

	private async Task SeedAttendanceScopeAsync(
		Guid employeeId,
		Guid organizationId,
		string monthYear,
		TimeOnly? timeIn = null,
		TimeOnly? timeOut = null)
	{
		var attendanceDate = FirstDayOfMonth(monthYear);
		var attendanceDateTime = attendanceDate.ToDateTime(TimeOnly.MinValue);
		var resolvedTimeIn = timeIn ?? new TimeOnly(9, 0);
		var resolvedTimeOut = timeOut ?? new TimeOnly(17, 0);
		await _attendanceRepository.InsertAsync(new EmployeeAttendance
		{
			TenantId = _currentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = attendanceDate,
			StatusType = EmployeeAttendanceStatusType.Present,
			TimeIn = new DateTimeOffset(attendanceDateTime.Add(resolvedTimeIn.ToTimeSpan()), TimeSpan.Zero),
			TimeOut = new DateTimeOffset(attendanceDateTime.Add(resolvedTimeOut.ToTimeSpan()), TimeSpan.Zero),
			EducationMediumStageId = organizationId
		}, autoSave: true);
	}

	private Task SaveSalaryComponentsAsync(Guid employeeId, bool isTemp = true) =>
		_employeePayrollAppService.UpdatePayrollContractAsync(employeeId, new PayrollContract
		{
			EarningComponents = new List<SalaryComponentContract>
			{
				new()
				{
					Name = "Basic",
					Amount = 31000m,
					IsFixed = true,
					IsTemp = isTemp
				}
			}
		});

	private Task SaveSalaryComponentsAsync(
		Guid employeeId,
		IReadOnlyList<EarningComponentDto> earnings,
		IReadOnlyList<DeductionComponentDto> deductions) =>
		_employeePayrollAppService.UpdatePayrollContractAsync(employeeId, new PayrollContract
		{
			EarningComponents = earnings.Select(x => new SalaryComponentContract
			{
				ComponentId = x.Id,
				Name = x.Name,
				Amount = x.Amount,
				IsFixed = true,
				IsTemp = false
			}).ToList(),
			DeductionComponents = deductions.Select(x => new SalaryComponentContract
			{
				ComponentId = x.Id,
				Name = x.Name,
				Amount = x.Amount,
				IsFixed = true,
				IsTemp = false
			}).ToList()
		});

	private async Task<RunPayrollItem> EnsurePayrollSnapshotAsync(Guid employeeId, string monthYear)
	{
		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var preview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});

		return preview.Items.Single(x => x.EmployeeId == employeeId);
	}

	private async Task<EarningComponentDto> CreateEarningComponentAsync(string name, decimal amount) =>
		await _earningComponentAppService.CreateAsync(new CreateEarningComponentDto
		{
			Name = name,
			Description = $"{name} description",
			ComponentType = EarningComponentType.Fixed,
			CalculationType = CalculationType.Amount,
			Amount = amount,
			DependOn = ComponentDependency.None,
			VisibleInPayslip = true
		});

	private async Task<DeductionComponentDto> CreateDeductionComponentAsync(string name, decimal amount) =>
		await _deductionComponentAppService.CreateAsync(new CreateDeductionComponentDto
		{
			Name = name,
			Description = $"{name} description",
			ComponentType = DeductionComponentType.Fixed,
			CalculationType = CalculationType.Amount,
			Amount = amount,
			DependOn = ComponentDependency.None,
			VisibleInPayslip = true
		});

	private async Task DeleteEarningComponentWithPreviewAsync(Guid id)
	{
		var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(id);
		await _earningComponentAppService.DeleteConfirmedAsync(id, preview.PreviewHash);
	}

	private async Task DeleteDeductionComponentWithPreviewAsync(Guid id)
	{
		var preview = await _deductionComponentAppService.PreviewDeleteImpactAsync(id);
		await _deductionComponentAppService.DeleteConfirmedAsync(id, preview.PreviewHash);
	}

	private async Task<HrPolicyAssignmentDto> UpsertEmployeePayrollPolicyAsync(
		PayrollPolicyContext context,
		Guid employeeId,
		Guid payrollPolicyId)
	{
		var effectiveFrom = new DateTime(context.Year, 1, 1);
		var reason = "Salary slip policy staleness regression";
		var targetKey = employeeId.ToString("D").ToLowerInvariant();
		var assignmentId = await FindExistingEmployeeAssignmentIdAsync(context.AcademicYearId, targetKey, effectiveFrom);
		var input = new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employeeId,
			AcademicYearId = context.AcademicYearId,
			AssignmentId = assignmentId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = payrollPolicyId,
			EffectiveFrom = effectiveFrom,
			AssignmentReason = reason
		};

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = assignmentId.HasValue ? HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction : "create",
			AssignmentId = assignmentId,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = targetKey,
				AttendancePolicyId = context.AttendancePolicy.Id,
				PayrollPolicyId = payrollPolicyId,
				EffectiveFrom = effectiveFrom,
				IsActive = true,
				Priority = 100,
				AssignmentReason = reason
			}
		});

		if (preview.RequiresConfirmation)
		{
			preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
			input.ConfirmationToken = preview.ConfirmationToken;
		}

		return await _assignments.UpsertEmployeeOverrideAsync(input);
	}

	private async Task<Guid?> FindExistingEmployeeAssignmentIdAsync(Guid academicYearId, string targetKey, DateTime effectiveFrom)
	{
		var effectiveEnd = DateTime.MaxValue.Date;
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			TargetType = "employee",
			MaxResultCount = 1000
		});

		return assignments.Items
			.Where(x =>
				string.Equals(x.TargetKey, targetKey, StringComparison.OrdinalIgnoreCase) &&
				x.IsActive &&
				x.EffectiveFrom.Date <= effectiveEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= effectiveFrom.Date))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id)
			.Select(x => (Guid?) x.Id)
			.FirstOrDefault();
	}

	private async Task<PayrollRunItemSnapshot> GetPayrollSnapshotAsync(Guid employeeId, string monthYear)
	{
		var snapshots = await _payrollRunItemSnapshotRepository.GetListAsync(x =>
			x.EmployeeId == employeeId &&
			x.MonthYear == monthYear);
		return snapshots.Single();
	}

	private async Task<IDisposable> ChangeToAdminPrincipalAsync()
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var userId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");
		var adminRole = (await _roleRepository.GetListAsync())
			.FirstOrDefault(x => x.Name.Equals("admin", StringComparison.OrdinalIgnoreCase));
		if (adminRole == null)
		{
			adminRole = new IdentityRole(_guidGenerator.Create(), "admin", _currentTenant.Id);
			await _roleRepository.InsertAsync(adminRole, autoSave: true);
		}

		var userName = $"salary-slip-admin-{userId:N}";
		var user = await _userManager.FindByIdAsync(userId.ToString());
		if (user == null)
		{
			user = new IdentityUser(userId, userName, $"{userName}@test.com", _currentTenant.Id);
			await _userManager.CreateAsync(user, "Test@1234");
		}

		if (!await _userManager.IsInRoleAsync(user, adminRole.Name))
		{
			await _userManager.AddToRoleAsync(user, adminRole.Name);
		}
		await uow.CompleteAsync();

		return _currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.UserName, userName),
			new Claim(AbpClaimTypes.Email, user.Email!),
			new Claim(AbpClaimTypes.Role, adminRole.Name),
			new Claim(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		})));
	}

	private async Task SeedPayrollStageScopeAsync(Guid userId, Guid stageId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					_currentTenant.Id,
					userId: userId,
					educationMediumStageId: stageId,
					description: "Salary slip payroll scope regression test"),
				autoSave: true);

			await _userScopeService.InvalidateUserScopeCacheAsync(userId);
		});
	}

	private ClaimsPrincipal BuildPayrollScopePrincipal(Guid userId)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, $"salary-slip-scope-{userId:N}"),
			new(AbpClaimTypes.Email, $"salary-slip-scope-{userId:N}@test.local")
		};

		if (_currentTenant.Id.HasValue)
		{
			claims.Add(new Claim(AbpClaimTypes.TenantId, _currentTenant.Id.Value.ToString()));
		}

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var existing = (await _roleRepository.GetListAsync()).FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var id = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(id, $"SalarySlipRole-{id:N}", _currentTenant.Id), autoSave: true);
		return id;
	}

	private async Task<Guid> EnsureShiftExistsAsync()
	{
		var existing = (await _shiftRepository.GetListAsync()).FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = $"SalarySlipShift-{_guidGenerator.Create():N}",
			StartTime = new TimeOnly(9, 0),
			EndTime = new TimeOnly(17, 0)
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task<Guid> CreateShiftAsync(TimeOnly startTime, TimeOnly endTime)
	{
		var shift = new ShiftManagement
		{
			ShiftName = $"SalarySlipShift-{_guidGenerator.Create():N}",
			StartTime = startTime,
			EndTime = endTime
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task<Guid> CreateAcademicYearAsync()
	{
		return await CreateAcademicYearAsync(2026);
	}

	private async Task<Guid> CreateAcademicYearAsync(int year)
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var created = await _academicYearManager.CreateAsync(
			$"SalarySlipAY-{Guid.NewGuid():N}",
			new DateTime(year, 1, 1),
			new DateTime(year, 12, 31));
		await _academicYearRepository.InsertAsync(created, autoSave: true);
		await uow.CompleteAsync();
		return created.Id;
	}

	private static string UniqueEmployeePan()
	{
		var digits = Math.Abs(Guid.NewGuid().GetHashCode()).ToString("D10");
		return $"ABCDE{digits[..4]}F";
	}

	private static string UniqueEmployeeAdhar()
	{
		var digits = Math.Abs(Guid.NewGuid().GetHashCode()).ToString("D10");
		return $"98{digits[..10]}";
	}

	private string CurrentPayrollMonth() => _clock.Now.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

	private static decimal DaysInMonth(string monthYear)
	{
		var year = PayrollYear(monthYear);
		var month = PayrollMonth(monthYear);
		return DateTime.DaysInMonth(year, month);
	}

	private string HistoricalPayrollMonth() => _clock.Now.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

	private static int PayrollYear(string monthYear) =>
		int.Parse(monthYear[..4], System.Globalization.CultureInfo.InvariantCulture);

	private static int PayrollMonth(string monthYear) =>
		int.Parse(monthYear[5..7], System.Globalization.CultureInfo.InvariantCulture);

	private static DateOnly FirstDayOfMonth(string monthYear) =>
		new(PayrollYear(monthYear), PayrollMonth(monthYear), 1);

	private static decimal CalculateExpectedOvertime(
		decimal monthlyGrossSalary,
		decimal basisDays,
		TimeOnly shiftStart,
		TimeOnly shiftEnd,
		decimal overtimeMinutes,
		decimal multiplier)
	{
		var shiftHours = (decimal)(shiftEnd.ToTimeSpan() - shiftStart.ToTimeSpan()).TotalHours;
		var overtimeHours = overtimeMinutes / 60m;
		return Math.Round(monthlyGrossSalary / basisDays / shiftHours * overtimeHours * multiplier, 2, MidpointRounding.AwayFromZero);
	}

	private sealed record PayrollPolicyContext(
		int Year,
		Guid AcademicYearId,
		Guid OrganizationId,
		PayrollPolicyDto WorkingDaysPayroll,
		PayrollPolicyDto TotalMonthPayroll,
		AttendancePolicyDto AttendancePolicy);
}
