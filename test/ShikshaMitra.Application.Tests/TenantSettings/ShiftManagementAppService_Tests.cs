using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IShiftManagementAppService _shiftManagementAppService;
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IStandardAppService _standardAppService;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public ShiftManagementAppService_Tests()
	{
		_shiftManagementAppService = GetRequiredService<IShiftManagementAppService>();
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_standardAppService = GetRequiredService<IStandardAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Create_Should_Allow_Normal_Same_Day_Shift()
	{
		var shift = await _shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto
		{
			ShiftName = $"Normal Shift {Guid.NewGuid():N}",
			StartTime = new TimeOnly(9, 0),
			EndTime = new TimeOnly(17, 0)
		});

		shift.Id.ShouldNotBe(Guid.Empty);
		shift.StartTime.ShouldBe(new TimeOnly(9, 0));
		shift.EndTime.ShouldBe(new TimeOnly(17, 0));
	}

	[Fact]
	public async Task Create_Should_Reject_Equal_Start_And_End_Time()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto
			{
				ShiftName = $"Equal Shift {Guid.NewGuid():N}",
				StartTime = new TimeOnly(9, 0),
				EndTime = new TimeOnly(9, 0)
			}));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
	}

	[Fact]
	public async Task Create_Should_Reject_Overnight_Shift()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto
			{
				ShiftName = $"Overnight Shift {Guid.NewGuid():N}",
				StartTime = new TimeOnly(22, 0),
				EndTime = new TimeOnly(6, 0)
			}));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
	}

	[Fact]
	public async Task Delete_Should_Allow_Unreferenced_Shift()
	{
		var shift = await CreateShiftAsync("Delete Free");

		await _shiftManagementAppService.DeleteAsync(shift.Id);

		await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() =>
			_shiftManagementAppService.GetAsync(shift.Id));
	}

	[Fact]
	public async Task Delete_Should_Reject_Shift_Assigned_To_Employee()
	{
		var shift = await CreateShiftAsync("Employee Ref");
		await CreateEmployeeAsync(shift.Id);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_shiftManagementAppService.DeleteAsync(shift.Id));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
		exception.Message.ShouldContain("assigned to employees or classes");
	}

	[Fact]
	public async Task Delete_Should_Reject_Shift_Assigned_To_Standard()
	{
		var shift = await CreateShiftAsync("Standard Ref");
		await CreateStandardAsync(shift.Id);

		BusinessException exception;
		using (_currentTenant.Change(TestTenantId))
		{
			exception = await Should.ThrowAsync<BusinessException>(() =>
				_shiftManagementAppService.DeleteAsync(shift.Id));
		}

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
		exception.Message.ShouldContain("assigned to employees or classes");
	}

	private Task<ShiftManagementDto> CreateShiftAsync(string prefix) =>
		_shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto
		{
			ShiftName = $"{prefix} {Guid.NewGuid():N}",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(16, 0)
		});

	private async Task CreateEmployeeAsync(Guid shiftId)
	{
		var roleId = await EnsureRoleExistsAsync();
		var uniq = Guid.NewGuid().ToString("N")[..8];

		await _personalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"ShiftRef{uniq}",
			LastName = "Employee",
			MobileNo = $"9{DateTime.UtcNow.Ticks % 1000000000:D9}",
			Email = $"shift.ref.{uniq}@test.local",
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Male,
			CastType = CastType.General,
			FamilyCaste = "General",
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			EmployeeGroup = EmployeeGroupType.NonTeaching,
			RoleId = roleId,
			ShiftId = shiftId
		});
	}

	private async Task CreateStandardAsync(Guid shiftId)
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		using (_currentTenant.Change(TestTenantId))
		{
			await _standardAppService.CreateAsync(new CreateUpdateStandardDto
			{
				Name = $"ShiftRefStd{Guid.NewGuid():N}"[..30],
				AgeLowerLimit = 6,
				EducationMediumStageId = stageId,
				ShiftId = shiftId
			});
		}
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		return await HR.Employees.EmployeeRoleTestHelper.EnsureAssignableRoleAsync(
			_roleRepository,
			_guidGenerator,
			_currentTenant.Id,
			"ShiftManagementRole");
	}

}
