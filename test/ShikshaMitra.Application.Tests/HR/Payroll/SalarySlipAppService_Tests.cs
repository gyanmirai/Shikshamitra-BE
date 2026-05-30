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
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ISalarySlipAppService _salarySlipAppService;
	private readonly IRunPayrollAppService _runPayrollAppService;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfo;
	private readonly IEmployeePayrollAppService _employeePayrollAppService;
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

	public SalarySlipAppService_Tests()
	{
		_salarySlipAppService = GetRequiredService<ISalarySlipAppService>();
		_runPayrollAppService = GetRequiredService<IRunPayrollAppService>();
		_employeePersonalInfo = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeePayrollAppService = GetRequiredService<IEmployeePayrollAppService>();
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
		var context = await CreatePayrollPolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var oldSlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
			{
				EmployeeId = employee.Id,
				MonthYear = "2026-01"
			});
		oldSlip.PayrollBasisDays.ShouldBe(31m);
		oldSlip.PayrollDayCalculationMode.ShouldBe("total-month-days");

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		var preview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = "2026-01",
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var payrollRow = preview.Items.Single(x => x.EmployeeId == employee.Id);

		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, "2026-01");

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
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);

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

	private async Task<PayrollPolicyContext> CreatePayrollPolicyContextAsync()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var calendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"SalarySlip Calendar {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = new DateTime(2026, 1, 1),
			EffectiveTo = new DateTime(2026, 12, 31),
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

		return new PayrollPolicyContext(academicYearId, organizationId, workingDays, totalMonth, attendance);
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync()
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
			DateOfJoining = DateOnly.FromDateTime(new DateTime(2026, 1, 1)),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		});
	}

	private async Task SeedAttendanceScopeAsync(Guid employeeId, Guid organizationId)
	{
		await _attendanceRepository.InsertAsync(new EmployeeAttendance
		{
			TenantId = _currentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = new DateOnly(2026, 1, 1),
			StatusType = EmployeeAttendanceStatusType.Present,
			TimeIn = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
			TimeOut = new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.Zero),
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
			EffectiveFrom = new DateTime(2026, 1, 1),
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
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var created = await _academicYearManager.CreateAsync(
			$"SalarySlipAY-{Guid.NewGuid():N}",
			new DateTime(2026, 1, 1),
			new DateTime(2026, 12, 31));
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

	private sealed record PayrollPolicyContext(
		Guid AcademicYearId,
		Guid OrganizationId,
		PayrollPolicyDto WorkingDaysPayroll,
		PayrollPolicyDto TotalMonthPayroll,
		AttendancePolicyDto AttendancePolicy);
}
