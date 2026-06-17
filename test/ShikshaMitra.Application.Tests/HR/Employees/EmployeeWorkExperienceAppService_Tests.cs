using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.WorkExperienceContracts;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeWorkExperienceAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeeWorkExperienceAppService _workExperienceAppService;
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IRepository<EducationMediumStage, Guid> _mediumStageRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public EmployeeWorkExperienceAppService_Tests()
	{
		_workExperienceAppService = GetRequiredService<IEmployeeWorkExperienceAppService>();
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_mediumStageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task UpsertAsync_Then_GetAsync_Should_Return_Persisted_Rows()
	{
		var employeeId = await CreateEmployeeAsync();

		var input = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "La Net Solution",
				Designation = "Software engineer trainee",
				Department = "Frontend",
				Country = new ShikshaMitra.HR.Employees.WorkExperienceContracts.CountryInfo { Name = "India", IsoCode = "IN" },
				State = new ShikshaMitra.HR.Employees.WorkExperienceContracts.StateInfo { Name = "Gujarat", IsoCode = "GJ" },
				City = "Surat",
				StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified),
				EndDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Unspecified),
			}
		};

		await _workExperienceAppService.UpsertAsync(employeeId, input);

		var loaded = await _workExperienceAppService.GetAsync(employeeId);
		loaded.Count.ShouldBe(1);
		loaded[0].MediumStageName.ShouldBe("La Net Solution");
		loaded[0].Designation.ShouldBe("Software engineer trainee");
		loaded[0].Department.ShouldBe("Frontend");
		loaded[0].City.ShouldBe("Surat");
		loaded[0].Country.ShouldNotBeNull();
		loaded[0].Country!.Name.ShouldBe("India");
		loaded[0].Country!.IsoCode.ShouldBe("IN");
		loaded[0].State.ShouldNotBeNull();
		loaded[0].State!.Name.ShouldBe("Gujarat");
		loaded[0].State!.IsoCode.ShouldBe("GJ");
	}

	[Fact]
	public async Task UpsertAsync_Replace_List_Should_Persist_Latest_Count()
	{
		var employeeId = await CreateEmployeeAsync();

		await _workExperienceAppService.UpsertAsync(
			employeeId,
			new List<WorkExperienceContract>
			{
				new()
				{
					MediumStageName = "First Org",
					Designation = "Dev",
					StartDate = DateTime.UtcNow.AddYears(-2),
					EndDate = DateTime.UtcNow.AddYears(-1),
				},
				new()
				{
					MediumStageName = "Second Org",
					Designation = "Lead",
					StartDate = DateTime.UtcNow.AddYears(-1),
				},
			});

		(await _workExperienceAppService.GetAsync(employeeId)).Count.ShouldBe(2);

		await _workExperienceAppService.UpsertAsync(
			employeeId,
			new List<WorkExperienceContract>
			{
				new()
				{
					MediumStageName = "Only Org",
					Designation = "Architect",
					StartDate = DateTime.UtcNow.AddMonths(-6),
				},
			});

		var final = await _workExperienceAppService.GetAsync(employeeId);
		final.Count.ShouldBe(1);
		final[0].MediumStageName.ShouldBe("Only Org");
	}

	[Fact]
	public async Task Reload_From_Repository_After_Upsert_Should_Have_WorkExperiences()
	{
		var employeeId = await CreateEmployeeAsync();

		await _workExperienceAppService.UpsertAsync(
			employeeId,
			new List<WorkExperienceContract>
			{
				new()
				{
					MediumStageName = "DB Check Org",
					Designation = "QA",
					Department = "Quality",
					StartDate = DateTime.UtcNow.AddYears(-1),
				},
			});

		var fromDb = await _employeeRepository.GetAsync(employeeId);
		fromDb.WorkExperiences.Count.ShouldBe(1);
		fromDb.WorkExperiences[0].OrganizationName.ShouldBe("DB Check Org");
		fromDb.WorkExperiences[0].Designation.ShouldBe("QA");
	}

	[Fact]
	public async Task UpsertAsync_Should_Reject_Non_Hr_User()
	{
		var employeeId = await CreateEmployeeAsync();

		using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid())))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				_workExperienceAppService.UpsertAsync(
					employeeId,
					new List<WorkExperienceContract>
					{
						new()
						{
							MediumStageName = "Blocked Org",
							Designation = "Blocked"
						}
					}));
		}
	}

	private async Task<Guid> CreateEmployeeAsync()
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var dto = new CreateEmployeePersonalInfoDto
		{
			FirstName = "Wx",
			LastName = "Test",
			MobileNo = "9876543210",
			Email = $"wx.{_guidGenerator.Create():N}@test.com",
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId
		};

		var created = await _personalInfoAppService.CreateAsync(dto);
		created.ShouldNotBeNull();
		return created!.Id;
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		var existing = roles.FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var id = _guidGenerator.Create();
		var role = new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id);
		await _roleRepository.InsertAsync(role, autoSave: true);
		return id;
	}

	private async Task<Guid> EnsureShiftExistsAsync()
	{
		var shifts = await _shiftRepository.GetListAsync();
		var existing = shifts.FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = "Test Shift",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(16, 0)
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private ClaimsPrincipal BuildPrincipal(Guid userId)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, $"user-{userId:N}"),
			new(AbpClaimTypes.Email, $"user-{userId:N}@test.local"),
			new(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
