using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeApplicationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeeApplicationAppService _applications;
	private readonly IRepository<HrPolicyAssignment, Guid> _assignmentRepository;
	private readonly IWorkCalendarPolicyAppService _workCalendars;
	private readonly IPayrollPolicyAppService _payrollPolicies;
	private readonly IAttendancePolicyAppService _attendancePolicies;
	private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly LeavePolicyManager _leavePolicyManager;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IUnitOfWorkManager _unitOfWorkManager;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeApplicationAppService_Tests()
	{
		_applications = GetRequiredService<IEmployeeApplicationAppService>();
		_assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		_workCalendars = GetRequiredService<IWorkCalendarPolicyAppService>();
		_payrollPolicies = GetRequiredService<IPayrollPolicyAppService>();
		_attendancePolicies = GetRequiredService<IAttendancePolicyAppService>();
		_leaveTypeRepository = GetRequiredService<IRepository<LeaveType, Guid>>();
		_leavePolicyRepository = GetRequiredService<IRepository<LeavePolicy, Guid>>();
		_leavePolicyManager = GetRequiredService<LeavePolicyManager>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task FinalizeAsync_Should_Create_Default_Policy_Assignment_When_Override_Is_Missing()
	{
		await CreateDefaultPoliciesAsync();

		var invite = await _applications.CreateAsync(new CreateEmployeeApplicationInviteDto
		{
			FirstName = "Onboard",
			LastName = "Default",
			Email = $"onboarding.default.{Guid.NewGuid():N}@test.com",
			MobileNo = "9876500001",
			PositionAppliedFor = "Teacher",
			DepartmentAppliedFor = "Primary"
		});
		var submitted = await _applications.SubmitPublicAsync(
			invite.Token,
			new PublicEmployeeApplicationSubmitDto
			{
				FirstName = "Onboard",
				LastName = "Default",
				Email = $"onboarding.submitted.{Guid.NewGuid():N}@test.com",
				MobileNo = "9876500002",
				PositionAppliedFor = "Teacher",
				DepartmentAppliedFor = "Primary"
			},
			new EmployeeApplicationFileInput
			{
				FileName = "resume.pdf",
				ContentType = "application/pdf",
				Content = BuildMinimalPdf(),
				Size = BuildMinimalPdf().Length
			},
			new EmployeeApplicationFileInput
			{
				FileName = "photo.png",
				ContentType = "image/png",
				Content = BuildMinimalPng(),
				Size = BuildMinimalPng().Length
			});

		var result = await _applications.FinalizeAsync(submitted.Id, new FinalizeEmployeeApplicationDto
		{
			PersonalInfo = await BuildPersonalInfoAsync()
		});

		var assignments = await _assignmentRepository.GetListAsync(x =>
			x.IsActive &&
			x.TargetType == HrPolicyAssignmentTargetType.Employee &&
			x.TargetKey == result.Employee.Id.ToString("D").ToLowerInvariant());

		assignments.ShouldContain(x =>
			x.AssignmentReason == "Employee onboarding default policy assignment" &&
			x.AttendancePolicyId != Guid.Empty &&
			x.PayrollPolicyId != Guid.Empty &&
			x.LeavePolicyId.HasValue &&
			x.LeavePolicyId.Value != Guid.Empty &&
			x.EffectiveFrom == new DateTime(2026, 4, 1));
	}

	private async Task<CreateEmployeePersonalInfoDto> BuildPersonalInfoAsync()
	{
		return new CreateEmployeePersonalInfoDto
		{
			FirstName = "Onboard",
			LastName = "Default",
			MobileNo = "9876500003",
			Email = $"onboarding.finalize.{Guid.NewGuid():N}@test.com",
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Female,
			DateOfJoining = new DateOnly(2026, 4, 1),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = await EnsureRoleExistsAsync(),
			ShiftId = await EnsureShiftExistsAsync(),
			OrganizationIds = [await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider)]
		};
	}

	private async Task CreateDefaultPoliciesAsync()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var calendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"Onboarding Calendar {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});
		await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Onboarding Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day",
			HolidayOverrides = new List<PayrollPolicyHolidayOverrideDto>()
		});
		await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Onboarding Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		var leaveType = await _leaveTypeRepository.InsertAsync(new LeaveType(
			_guidGenerator.Create(),
			_currentTenant.Id,
			academicYearId,
			$"Onboarding Leave Type {Guid.NewGuid():N}",
			"#1A73E8"), autoSave: true);
		var leavePolicy = await _leavePolicyManager.CreateAsync(
			academicYearId,
			$"Onboarding Leave Policy {Guid.NewGuid():N}",
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			HrPolicyStatus.Active,
			true,
			[
				new LeavePolicyRuleInput(
					leaveType.Id,
					LeaveAccrualPolicyType.Monthly,
					12,
					LeavePaidType.Paid,
					false,
					null,
					false,
					null,
					true,
					0,
					30,
					true,
					null,
					false,
					true)
			],
			"Default leave policy for onboarding finalization tests");
		await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: true);
	}

	private async Task<Guid> CreateAcademicYearAsync()
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var created = await _academicYearManager.CreateAsync(
			$"OnboardingAY-{Guid.NewGuid():N}",
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31));
		await _academicYearRepository.InsertAsync(created, autoSave: true);
		await uow.CompleteAsync();
		return created.Id;
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		var existing = roles.FirstOrDefault();
		if (existing != null) return existing.Id;

		var id = _guidGenerator.Create();
		var role = new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id);
		await _roleRepository.InsertAsync(role, autoSave: true);
		return id;
	}

	private async Task<Guid> EnsureShiftExistsAsync()
	{
		var shifts = await _shiftRepository.GetListAsync();
		var existing = shifts.FirstOrDefault();
		if (existing != null) return existing.Id;

		var shift = new ShiftManagement
		{
			ShiftName = "Test Shift",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(16, 0)
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private static byte[] BuildMinimalPdf() =>
		[
			0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34,
			0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A
		];

	private static byte[] BuildMinimalPng() =>
		[
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
			0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
			0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
			0x54, 0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02,
			0x00, 0x01, 0xE5, 0x27, 0xDE, 0xFC, 0x00, 0x00,
			0x00, 0x00, 0x49, 0x45, 0x44, 0xAE, 0x42, 0x60,
			0x82
		];
}
