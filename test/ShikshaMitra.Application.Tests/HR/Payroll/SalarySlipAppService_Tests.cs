using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Statutory;
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
	private readonly ILeavePolicyAppService _leavePolicyAppService;
	private readonly SalarySlipAccessService _salarySlipAccessService;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly IRepository<WorkCalendarHoliday, Guid> _workCalendarHolidayRepository;
	private readonly IRepository<EmployeeOrganizationAssignment, Guid> _employeeOrganizationAssignmentRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
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
		QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
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
		_leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
		_salarySlipAccessService = GetRequiredService<SalarySlipAccessService>();
		_attendanceRepository = GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		_employeeLeaveRepository = GetRequiredService<IEmployeeLeaveRepository>();
		_workCalendarHolidayRepository = GetRequiredService<IRepository<WorkCalendarHoliday, Guid>>();
		_employeeOrganizationAssignmentRepository = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_payrollRunItemSnapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_salarySlipRepository = GetRequiredService<ISalarySlipRepository>();
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
	public async Task Bank_updates_accept_projected_identifiers_without_creating_a_second_statutory_owner()
	{
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(CurrentPayrollMonth()));
		using var principal = await ChangeToAdminPrincipalAsync();
		var profiles = GetRequiredService<IEmployeeStatutoryProfileAppService>();
		var current = await profiles.GetAsync(employee.Id);
		await profiles.SaveAsync(employee.Id, new SaveEmployeeStatutoryProfileDto
		{
			Profile = current.Profile with { PfNumber = "7" }, ConcurrencyStamp = current.ConcurrencyStamp,
			EffectiveMonth = current.EffectiveMonth, IdempotencyKey = Guid.NewGuid(), UpdateEsic = false
		});
		var details = await _employeePayrollAppService.GetPayrollDetailsAsync(employee.Id);
		details.PayrollDetails.EpfNumber.ShouldBe("7");
		details.PayrollDetails.BankName = "Updated test bank";
		if (details.Earnings.Count == 0) details.Earnings.Add(new PayrollComponentDto { Name = "Legacy fixture earning", Amount = 10000 });
		if (details.Deductions.Count == 0) details.Deductions.Add(new PayrollComponentDto { Name = "Legacy fixture deduction", Amount = 0 });
		await _employeePayrollAppService.UpdatePayrollDetailsAsync(employee.Id, new UpdateEmployeePayrollDetailsDto
		{ PayrollDetails = details.PayrollDetails, Earnings = details.Earnings, Deductions = details.Deductions });
		var updated = await _employeePayrollAppService.GetPayrollDetailsAsync(employee.Id);
		updated.PayrollDetails.BankName.ShouldBe("Updated test bank");
		updated.PayrollDetails.EpfNumber.ShouldBe("7");
		(await GetRequiredService<IEmployeePayrollDetailsRepository>().GetByEmployeeIdAsync(employee.Id))!.EpfNumber.ShouldBeNull();
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
	public async Task GetSalarySlipAsync_Should_Block_Policy_Change_And_Preserve_Draft_From_Locked_Payroll_Snapshot()
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

		var policyChangeEx = await Should.ThrowAsync<BusinessException>(() =>
			UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id));
		policyChangeEx.Code.ShouldBe(HRErrorCodes.HrPolicyImpactBlocking);

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
	public async Task LockedPayroll_Should_Preserve_Snapshot_And_SalarySlip_After_Salary_Structure_Change()
	{
		const string monthYear = "2026-01";
		var context = await CreatePayrollPolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		var originalBasic = await CreateEarningComponentAsync($"Locked Basic {Guid.NewGuid():N}", 31000m);
		var originalDeduction = await CreateDeductionComponentAsync($"Locked Deduction {Guid.NewGuid():N}", 1500m);
		await SaveSalaryComponentsAsync(employee.Id, [originalBasic], [originalDeduction]);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		var snapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		snapshot.ShouldNotBeNull();
		snapshot.MarkLocked();
		await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: true);
		var lockedGross = snapshot.GrossSalary;
		var lockedDeduction = snapshot.TotalDeduction;
		var lockedNet = snapshot.NetPay;
		var lockedBasisDays = snapshot.BasisDays;
		var lockedPayableDays = snapshot.PayableDays;
		var lockedProrationFactor = snapshot.ProrationFactor;
		var lockedEarningsJson = snapshot.EarningComponentsJson;
		var lockedDeductionsJson = snapshot.DeductionComponentsJson;

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var originalSlip = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		originalSlip.TotalEarning.ShouldBe(lockedGross);
		originalSlip.TotalDeduction.ShouldBe(lockedDeduction);
		originalSlip.NetSalary.ShouldBe(lockedNet);

		var changedBasic = await CreateEarningComponentAsync($"Changed Locked Basic {Guid.NewGuid():N}", 45000m);
		var changedDeduction = await CreateDeductionComponentAsync($"Changed Locked Deduction {Guid.NewGuid():N}", 2500m);
		await SaveSalaryComponentsAsync(employee.Id, [changedBasic], [changedDeduction]);
		var refreshedPreview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var refreshedRow = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);
		var refreshedSnapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		refreshedRow.IsLocked.ShouldBeTrue();
		refreshedRow.GrossSalary.ShouldBe((double) lockedGross);
		refreshedRow.TotalDeduction.ShouldBe((double) lockedDeduction);
		refreshedRow.NetPay.ShouldBe((double) lockedNet);
		refreshedSnapshot.IsLocked.ShouldBeTrue();
		refreshedSnapshot.GrossSalary.ShouldBe(lockedGross);
		refreshedSnapshot.TotalDeduction.ShouldBe(lockedDeduction);
		refreshedSnapshot.NetPay.ShouldBe(lockedNet);
		refreshedSnapshot.BasisDays.ShouldBe(lockedBasisDays);
		refreshedSnapshot.PayableDays.ShouldBe(lockedPayableDays);
		refreshedSnapshot.ProrationFactor.ShouldBe(lockedProrationFactor);
		refreshedSnapshot.EarningComponentsJson.ShouldBe(lockedEarningsJson);
		refreshedSnapshot.DeductionComponentsJson.ShouldBe(lockedDeductionsJson);
		refreshedSlip.TotalEarning.ShouldBe(originalSlip.TotalEarning);
		refreshedSlip.TotalDeduction.ShouldBe(originalSlip.TotalDeduction);
		refreshedSlip.NetSalary.ShouldBe(originalSlip.NetSalary);
		refreshedSlip.PayrollBasisDays.ShouldBe(originalSlip.PayrollBasisDays);
		refreshedSlip.PayableDays.ShouldBe(originalSlip.PayableDays);
		refreshedSlip.ProrationFactor.ShouldBe(originalSlip.ProrationFactor);
		refreshedSlip.Earnings.ShouldContain(x => x.Name == originalBasic.Name && x.Amount == originalSlip.Earnings.Single(y => y.Name == originalBasic.Name).Amount);
		refreshedSlip.Deductions.ShouldContain(x => x.Name == originalDeduction.Name && x.Amount == originalSlip.Deductions.Single(y => y.Name == originalDeduction.Name).Amount);
		refreshedSlip.Earnings.ShouldNotContain(x => x.Name == changedBasic.Name);
		refreshedSlip.Deductions.ShouldNotContain(x => x.Name == changedDeduction.Name);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Return_Finalized_Current_Slip_Without_Regenerating_From_Snapshot()
	{
		using var schoolTenant = _currentTenant.Change(Guid.NewGuid(), "Synthetic Payslip School");
		await ConfigurePayslipSchoolAsync();
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var draft = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		await ReviewForFinalizationAsync(employee.Id, monthYear, context.AcademicYearId);
		var finalized = await _salarySlipAppService.FinalizeAsync(draft.Id);
		finalized.Status.ShouldBe(SalarySlipStatus.Finalized);

		var fetched = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		fetched.Id.ShouldBe(finalized.Id);
		fetched.Status.ShouldBe(SalarySlipStatus.Finalized);
		fetched.NetSalary.ShouldBe(finalized.NetSalary);
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
		var snapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		snapshot.ShouldNotBeNull();
		snapshot.MarkLocked();
		await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: true);

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
		var snapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		snapshot.ShouldNotBeNull();
		snapshot.MarkLocked();
		await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: true);

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
	public async Task GetSalarySlipAsync_Should_Refresh_Historical_Draft_From_Authoritative_Unlocked_Snapshot()
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
		var refreshedPreview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var payrollRow = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);

		var refreshedSlip = await _salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear);

		// Drafts are projections of the authoritative snapshot regardless of period.
		// Finalized and paid slips are the immutable accounting boundary.
		refreshedSlip.PayrollDayCalculationMode.ShouldBe("working-days");
		refreshedSlip.PayrollBasisDays.ShouldBe((decimal) payrollRow.BasisDays);
		refreshedSlip.PayableDays.ShouldBe((decimal) payrollRow.PayableDays);
		refreshedSlip.NetSalary.ShouldBe((decimal) payrollRow.NetPay);
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Fail_Closed_When_Historical_Draft_Snapshot_Is_Missing()
	{
		var monthYear = HistoricalPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});

		var snapshot = await GetPayrollSnapshotAsync(employee.Id, monthYear);
		snapshot.ShouldNotBeNull();
		await _payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: true);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_salarySlipAppService.GetSalarySlipAsync(employee.Id, monthYear));

		exception.Code.ShouldBe(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip);
	}

	[Fact]
	public async Task FinalizeAsync_Should_Reconcile_Draft_With_Authoritative_Snapshot()
	{
		using var schoolTenant = _currentTenant.Change(Guid.NewGuid(), "Synthetic Payslip School");
		await ConfigurePayslipSchoolAsync();
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var draft = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		draft.PayrollDayCalculationMode.ShouldBe("total-month-days");

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		var refreshedPreview = await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = monthYear,
			AcademicYearId = context.AcademicYearId,
			Page = 0,
			PageSize = 50,
			ForceRefresh = true
		});
		var payrollRow = refreshedPreview.Items.Single(x => x.EmployeeId == employee.Id);

		await ReviewForFinalizationAsync(employee.Id, monthYear, context.AcademicYearId);
		var finalized = await _salarySlipAppService.FinalizeAsync(draft.Id);

		finalized.Status.ShouldBe(SalarySlipStatus.Finalized);
		finalized.PayrollDayCalculationMode.ShouldBe("working-days");
		finalized.PayrollBasisDays.ShouldBe((decimal) payrollRow.BasisDays);
		finalized.PayableDays.ShouldBe((decimal) payrollRow.PayableDays);
		finalized.NetSalary.ShouldBe((decimal) payrollRow.NetPay);
	}

	[Fact]
	public async Task Paid_Salary_Slip_Should_Not_Be_Finalized_Or_Deleted()
	{
		using var schoolTenant = _currentTenant.Change(Guid.NewGuid(), "Synthetic Payslip School");
		await ConfigurePayslipSchoolAsync();
		var monthYear = CurrentPayrollMonth();
		var context = await CreatePayrollPolicyContextAsync(PayrollYear(monthYear));
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SeedAttendanceScopeAsync(employee.Id, context.OrganizationId, monthYear);
		await SaveSalaryComponentsAsync(employee.Id);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.TotalMonthPayroll.Id);
		await EnsurePayrollSnapshotAsync(employee.Id, monthYear);

		using var adminPrincipal = await ChangeToAdminPrincipalAsync();
		var draft = await _salarySlipAppService.GenerateSalarySlipAsync(new GenerateSalarySlipInput
		{
			EmployeeId = employee.Id,
			MonthYear = monthYear
		});
		await ReviewForFinalizationAsync(employee.Id, monthYear, context.AcademicYearId);
		await _salarySlipAppService.FinalizeAsync(draft.Id);
		var salarySlip = await _salarySlipRepository.GetAsync(draft.Id);
		salarySlip.RecordPayment(DateTimeOffset.UtcNow.AddMinutes(-1), "Synthetic payment",
			Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d"), Guid.NewGuid());
		await _salarySlipRepository.UpdateAsync(salarySlip, autoSave: true);

		var finalizeException = await Should.ThrowAsync<BusinessException>(() =>
			_salarySlipAppService.FinalizeAsync(draft.Id));
		finalizeException.Code.ShouldBe(HRErrorCodes.SalarySlipAlreadyFinalized);
		var deleteException = await Should.ThrowAsync<BusinessException>(() =>
			_salarySlipAppService.DeleteAsync(draft.Id));

		deleteException.Code.ShouldBe(HRErrorCodes.SalarySlipAlreadyFinalized);
		(await _salarySlipRepository.FindAsync(draft.Id)).ShouldNotBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GetSalarySlipAsync_Should_Include_Overtime_Earning_From_Attendance(bool esiEnabled)
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

		if (esiEnabled)
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var treatments = (await GetRequiredService<IEarningComponentRepository>().GetListAsync())
					.ToDictionary(x => x.Id, _ => StatutoryWageTreatment.Included);
				await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(new(
					Guid.NewGuid(), _currentTenant.Id, FirstDayOfMonth(monthYear).ToDateTime(TimeOnly.MinValue), true, null, null, Guid.NewGuid().ToString(), treatments: treatments));
				await GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertAsync(new(
					Guid.NewGuid(), _currentTenant.Id, "ESI", "IN-GJ", 1, FirstDayOfMonth(monthYear).ToDateTime(TimeOnly.MinValue), null,
					10, 21000m, 25000m, 0.75m, 3.25m, 50m, 15m, StatutoryRoundingPolicy.NextHigherRupee,
					"https://www.labour.gov.in/", DateTime.UtcNow));
			});
		}

		var payrollRow = await EnsurePayrollSnapshotAsync(employee.Id, monthYear);
		payrollRow.IssueCodes.ShouldNotContain(EsiStatutoryCalculator.WageClassificationIssue);
		var overtime = payrollRow.EarningComponents.Single(x => x.Name == "Overtime Pay");
		var regularSalary = Math.Round(31000m / (decimal) payrollRow.BasisDays, 2, MidpointRounding.AwayFromZero);
		var expectedOvertime = CalculateExpectedOvertime(
			monthlyGrossSalary: 31000m,
			basisDays: (decimal) payrollRow.BasisDays,
			shiftStart: new TimeOnly(7, 0),
			shiftEnd: new TimeOnly(17, 30),
			overtimeMinutes: 90m,
			multiplier: 2m);
		overtime.Amount.ShouldBe((double) expectedOvertime);
		// Overtime is additional earned income, not part of the regular gross calculation base.
		payrollRow.GrossSalary.ShouldBe((double) regularSalary);
		payrollRow.NetPay.ShouldBe((double) (regularSalary + expectedOvertime));

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
		var regularSalary = Math.Round(31000m / (decimal) payrollRow.BasisDays, 2, MidpointRounding.AwayFromZero);

		payrollRow.EarningComponents.ShouldNotContain(x => x.Name == "Overtime Pay");
		payrollRow.GrossSalary.ShouldBe((double) regularSalary);
		payrollRow.NetPay.ShouldBe((double) regularSalary);
	}

	[Fact]
	public async Task SalarySlipManager_Should_Use_WorkCalendar_Units_For_Approved_Leave_Without_Attendance()
	{
		const string monthYear = "2026-06";
		var context = await CreatePayrollPolicyContextAsync(2026, weeklyOffPattern: "sunday");
		var employee = await CreateEmployeeAsync(FirstDayOfMonth(monthYear));
		await SaveSalaryComponentsAsync(employee.Id, isTemp: false);
		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.WorkingDaysPayroll.Id);
		await SeedWorkCalendarHolidayAsync(
			context,
			new DateOnly(2026, 6, 19),
			new DateOnly(2026, 6, 20));
		await SeedApprovedPaidLeaveAsync(
			context,
			employee.Id,
			new DateTimeOffset(new DateTime(2026, 6, 19)),
			new DateTimeOffset(new DateTime(2026, 6, 30, 23, 59, 59)),
			approvedUnits: 8m);

		var salarySlip = await WithUnitOfWorkAsync(async () =>
		{
			var salarySlipManager = GetRequiredService<SalarySlipManager>();
			return await salarySlipManager.GenerateOrGetSalarySlipAsync(employee.Id, monthYear);
		});

		salarySlip.PayrollDayCalculationMode.ShouldBe(DayCalculationType.WorkingDays);
		salarySlip.PayrollBasisDays.ShouldBe(24m);
		salarySlip.PayableDays.ShouldBe(8m);
	}

	private async Task<PayrollPolicyContext> CreatePayrollPolicyContextAsync(
		int year = 2026,
		bool overtimeEnabled = false,
		string? overtimeRate = null,
		int? minOvertimeMinutes = null,
		string weeklyOffPattern = "saturday_sunday")
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
			WeeklyOffPattern = weeklyOffPattern,
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
		var leavePolicy = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"SL Leave Policy {Guid.NewGuid():N}",
			Status = "Active",
			EffectiveFrom = new DateTime(year, 1, 1),
			EffectiveTo = new DateTime(year, 12, 31),
			IsDefault = true,
			Rules =
			[
				new LeavePolicyRuleRequest
				{
					PaidType = "Paid",
					EntitlementMode = "Limited",
					AccrualPolicyType = "Monthly",
					LeaveCount = 12,
					CarryForward = false,
					EncashmentAllowed = false,
					RequiresApproval = true,
					MinNoticeDays = 0,
					MaxDaysPerRequest = 30,
					AllowHalfDay = true,
					AllowNegativeBalance = false,
					IsActive = true
				},
				new LeavePolicyRuleRequest
				{
					PaidType = "Unpaid",
					EntitlementMode = "Unlimited",
					AccrualPolicyType = "Yearly",
					LeaveCount = null,
					CarryForward = false,
					EncashmentAllowed = false,
					RequiresApproval = true,
					MinNoticeDays = 0,
					MaxDaysPerRequest = 30,
					AllowHalfDay = true,
					AllowNegativeBalance = true,
					IsActive = true
				}
			]
		});
		var paidLeaveRuleId = leavePolicy.Rules
			.Single(x => string.Equals(x.PaidType, "Paid", StringComparison.OrdinalIgnoreCase))
			.Id;

		return new PayrollPolicyContext(
			year,
			academicYearId,
			organizationId,
			workingDays,
			totalMonth,
			attendance,
			calendar.Id,
			leavePolicy.Id,
			paidLeaveRuleId);
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
		var employee = await _employeePersonalInfo.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = "Payslip",
			LastName = "Policy",
			MobileNo = "9876543210",
			Email = $"payslip.policy.{_guidGenerator.Create():N}@test.com",
			PanNo = UniqueEmployeePan(),
			AdharNo = UniqueEmployeeAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
			GenderType = GenderType.Male,
			CastType = CastType.General,
			FamilyCaste = "General",
			DateOfJoining = dateOfJoining ?? DateOnly.FromDateTime(new DateTime(2026, 1, 1)),
			EmployeeStatusType = EmployeeStatusType.Active,
			EmployeeGroup = EmployeeGroupType.NonTeaching,
			RoleId = roleId,
			ShiftId = resolvedShiftId
		});
		await _employeeOrganizationAssignmentRepository.InsertAsync(
			new EmployeeOrganizationAssignment(
				_guidGenerator.Create(),
				_currentTenant.Id,
				employee.Id,
				resolvedOrganizationId),
			autoSave: true);
		await SaveSalaryComponentsAsync(employee.Id);
		using (await ChangeToAdminPrincipalAsync())
		{
			await GetRequiredService<IEmployeeStatusAppService>().ChangeStatusAsync(employee.Id,
				new ChangeEmployeeStatusDto { EmployeeStatusType = EmployeeStatusType.Active, Remarks = "Fixture compensation ready" });
		}
		return employee;
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

	private Task SeedWorkCalendarHolidayAsync(
		PayrollPolicyContext context,
		DateOnly fromDate,
		DateOnly toDate) =>
		_workCalendarHolidayRepository.InsertAsync(
			new WorkCalendarHoliday(
				_guidGenerator.Create(),
				_currentTenant.Id,
				context.AcademicYearId,
				context.WorkCalendarPolicyId,
				fromDate,
				toDate,
				"Salary slip approved leave holiday",
				WorkCalendarHolidaySource.Manual,
				calendarHolidayId: null,
				isIncluded: true,
				notes: null),
			autoSave: true);

	private Task SeedApprovedPaidLeaveAsync(
		PayrollPolicyContext context,
		Guid employeeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		decimal approvedUnits)
	{
		var leave = EmployeeLeave.Create(
			_guidGenerator.Create(),
			_currentTenant.Id,
			employeeId,
			context.LeavePolicyId,
			context.PaidLeavePolicyRuleId,
			LeavePaidType.Paid,
			startDate,
			endDate,
			"Salary slip approved leave regression",
			halfDay: false);
		leave.SetApprovalPayrollSnapshot(LeavePaidType.Paid, approvedUnits);
		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, rejectionReason: null);
		return _employeeLeaveRepository.InsertAsync(leave, autoSave: true);
	}

	private Task SaveSalaryComponentsAsync(Guid employeeId, bool isTemp = false) =>
		SaveApprovedPayrollContractAsync(employeeId, new PayrollContract
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
		SaveApprovedPayrollContractAsync(employeeId, new PayrollContract
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

	private async Task SaveApprovedPayrollContractAsync(Guid employeeId, PayrollContract contract)
	{
		using var principal = await ChangeToAdminPrincipalAsync();
		var rules = new List<SalaryTemplateRuleDto>();
		foreach (var line in contract.EarningComponents)
		{
			var componentId = line.ComponentId ?? (await CreateEarningComponentAsync($"Basic {Guid.NewGuid():N}", line.Amount!.Value)).Id;
			rules.Add(new SalaryTemplateRuleDto
			{
				ComponentId = componentId,
				LineKind = "EARNING",
				SemanticType = rules.Count == 0 ? "BASIC" : "ORDINARY",
				FormulaType = "FIXED_AMOUNT",
				Value = line.Amount,
				DisplayOrder = rules.Count
			});
		}
		foreach (var line in contract.DeductionComponents ?? [])
			rules.Add(new SalaryTemplateRuleDto
			{
				ComponentId = line.ComponentId!.Value,
				LineKind = "DEDUCTION",
				FormulaType = "FIXED_AMOUNT",
				Value = line.Amount,
				DisplayOrder = rules.Count
			});
		var gross = contract.EarningComponents.Sum(x => x.Amount!.Value);
		var employee = await GetRequiredService<IEmployeeRepository>().GetAsync(employeeId);
		var joining = employee.DateOfJoining ?? new DateOnly(2026, 1, 1);
		var month = new DateTime(joining.Year, joining.Month, 1);
		// These legacy salary fixtures deliberately have no PF formula or registration.
		var profiles = GetRequiredService<IRepository<EmployeeStatutoryProfileVersion, Guid>>();
		if (await profiles.FindAsync(x => x.EmployeeId == employeeId) == null)
			await profiles.InsertAsync(new EmployeeStatutoryProfileVersion(Guid.NewGuid(), employee.TenantId, employeeId, month,
				new PayrollStatutoryProfile { ApplyPf = false, PfDisabledReason = "Non-PF payslip fixture" }, Guid.NewGuid(), "FIXTURE"), autoSave: true);
		// A later salary revision must respect existing immutable payroll.
		var locked = await _payrollRunItemSnapshotRepository.GetListAsync(x => x.EmployeeId == employeeId && x.IsLocked);
		if (locked.Count > 0)
			month = DateTime.ParseExact(locked.Max(x => x.MonthYear)!, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture).AddMonths(1);
		var templates = GetRequiredService<ISalaryTemplateAppService>();
		var preview = await templates.PreviewAsync(new PreviewSalaryTemplateRequestDto
		{ Rules = rules, SalaryBasis = "GROSS", MonthlyAmount = gross, EffectiveMonth = month });
		var template = await templates.CreateAsync(new SaveSalaryTemplateDto
		{
			Name = $"Payslip fixture {Guid.NewGuid():N}",
			DefaultSalaryBasis = "GROSS",
			Rules = rules,
			SampleMonthlyAmount = gross,
			SampleEffectiveMonth = month,
			PreviewToken = preview.PreviewToken,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		var compensation = GetRequiredService<ICompensationRevisionAppService>();
		var current = (await compensation.GetListAsync(employeeId)).Items
			.Where(x => x.EffectiveMonth == month && x.Status == "EFFECTIVE").OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
		var saved = await compensation.SaveAsync(employeeId, new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id,
			SalaryBasis = "GROSS",
			MonthlyAmount = gross,
			EffectiveMonth = month,
			ExpectedRevision = current?.RevisionNumber,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		saved.GrossSalary.ShouldBe(gross);
		saved.CalculationSnapshotJson.ShouldNotBe("{}");
	}

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

	private async Task ConfigurePayslipSchoolAsync()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var school = new PayslipSettings(_currentTenant.Id!.Value);
			school.Configure("Synthetic Payslip School", "Synthetic address, Ahmedabad, Gujarat", false, Guid.NewGuid());
			await GetRequiredService<IRepository<PayslipSettings, Guid>>().InsertAsync(school, autoSave: true);
			// These finalization fixtures exercise non-statutory payroll; ESIC scenarios have their own coverage.
			await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(
				new EsiPayrollConfiguration(Guid.NewGuid(), _currentTenant.Id, new DateTime(2000, 1, 1), false,
					null, null, "Synthetic non-ESIC payslip fixture"), autoSave: true);
		});
	}

	private async Task ReviewForFinalizationAsync(Guid employeeId, string monthYear, Guid academicYearId)
	{
		var previews = GetRequiredService<IPayrollPreviewRunAppService>();
		var run = await previews.StartAsync(new StartPayrollPreviewRequest { Month = monthYear, AcademicYearId = academicYearId, Refresh = true });
		await GetRequiredService<PayrollPreviewBackgroundJob>().ExecuteAsync(
			new PayrollPreviewJobArgs { TenantId = _currentTenant.Id, RunId = run.Id });
		var completed = await previews.GetAsync(run.Id);
		completed.Status.ShouldBe("SUCCEEDED", completed.ErrorCode);
		completed.IsCurrent.ShouldBeTrue();
		var snapshot = await GetPayrollSnapshotAsync(employeeId, monthYear);
		PayrollLockPreconditionGuard.HasBlockingLockIssue(snapshot.IssueCodesJson).ShouldBeFalse(snapshot.IssueCodesJson);
		var review = await _runPayrollAppService.MarkReviewedAsync(new PayrollRunReviewRequest {
			PreviewRunId = run.Id, Month = monthYear, AcademicYearId = run.AcademicYearId, EmployeeIds = [employeeId] });
		review.EmployeesProcessed.ShouldBe(1);
		review.EmployeesFailed.ShouldBe(0);
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
			LeavePolicyId = context.LeavePolicyId,
			EffectiveFrom = effectiveFrom,
			AssignmentReason = reason
		};

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction,
			AssignmentId = assignmentId,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = targetKey,
				AttendancePolicyId = context.AttendancePolicy.Id,
				PayrollPolicyId = payrollPolicyId,
				LeavePolicyId = context.LeavePolicyId,
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
			var user = new IdentityUser(userId, $"payroll-scope-{userId:N}", $"payroll-scope-{userId:N}@test.local", _currentTenant.Id);
			(await _userManager.CreateAsync(user, "Test@1234")).Succeeded.ShouldBeTrue();
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
		return await Employees.EmployeeRoleTestHelper.EnsureAssignableRoleAsync(
			_roleRepository,
			_guidGenerator,
			_currentTenant.Id,
			"SalarySlipRole");
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
		var shiftHours = (decimal) (shiftEnd.ToTimeSpan() - shiftStart.ToTimeSpan()).TotalHours;
		var overtimeHours = overtimeMinutes / 60m;
		return Math.Round(monthlyGrossSalary / basisDays / shiftHours * overtimeHours * multiplier, 2, MidpointRounding.AwayFromZero);
	}

	private sealed record PayrollPolicyContext(
		int Year,
		Guid AcademicYearId,
		Guid OrganizationId,
		PayrollPolicyDto WorkingDaysPayroll,
		PayrollPolicyDto TotalMonthPayroll,
		AttendancePolicyDto AttendancePolicy,
		Guid WorkCalendarPolicyId,
		Guid LeavePolicyId,
		Guid PaidLeavePolicyRuleId);
}
