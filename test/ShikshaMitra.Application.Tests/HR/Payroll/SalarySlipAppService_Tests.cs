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
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IdentityUserManager _userManager;
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
		_attendanceRepository = GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		_payrollRunItemSnapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_userManager = GetRequiredService<IdentityUserManager>();
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

	private async Task<PayrollPolicyContext> CreatePayrollPolicyContextAsync(int year = 2026)
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
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		});

		return new PayrollPolicyContext(year, academicYearId, organizationId, workingDays, totalMonth, attendance);
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(DateOnly? dateOfJoining = null)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
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
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		});
	}

	private async Task SeedAttendanceScopeAsync(Guid employeeId, Guid organizationId, string monthYear)
	{
		var attendanceDate = FirstDayOfMonth(monthYear);
		var attendanceDateTime = attendanceDate.ToDateTime(TimeOnly.MinValue);
		await _attendanceRepository.InsertAsync(new EmployeeAttendance
		{
			TenantId = _currentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = attendanceDate,
			StatusType = EmployeeAttendanceStatusType.Present,
			TimeIn = new DateTimeOffset(attendanceDateTime.AddHours(9), TimeSpan.Zero),
			TimeOut = new DateTimeOffset(attendanceDateTime.AddHours(17), TimeSpan.Zero),
			EducationMediumStageId = organizationId
		}, autoSave: true);
	}

	private Task SaveSalaryComponentsAsync(Guid employeeId) =>
		_employeePayrollAppService.UpdatePayrollContractAsync(employeeId, new PayrollContract
		{
			EarningComponents = new List<SalaryComponentContract>
			{
				new()
				{
					Name = "Basic",
					Amount = 31000m,
					IsFixed = true,
					IsTemp = true
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

	private Task<HrPolicyAssignmentDto> UpsertEmployeePayrollPolicyAsync(
		PayrollPolicyContext context,
		Guid employeeId,
		Guid payrollPolicyId) =>
		_assignments.UpsertEmployeeOverrideAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employeeId,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = payrollPolicyId,
			EffectiveFrom = new DateTime(context.Year, 1, 1),
			AssignmentReason = "Salary slip policy staleness regression"
		});

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

	private sealed record PayrollPolicyContext(
		int Year,
		Guid AcademicYearId,
		Guid OrganizationId,
		PayrollPolicyDto WorkingDaysPayroll,
		PayrollPolicyDto TotalMonthPayroll,
		AttendancePolicyDto AttendancePolicy);
}
