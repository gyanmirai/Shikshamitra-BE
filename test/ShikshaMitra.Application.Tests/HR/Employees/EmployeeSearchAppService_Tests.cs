using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.Search;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeSearchAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeeSearchAppService _employeeSearchAppService;
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public EmployeeSearchAppService_Tests()
	{
		_employeeSearchAppService = GetRequiredService<IEmployeeSearchAppService>();
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task GetFilteredListAsync_Should_Match_Full_Name_With_Multiple_Tokens()
	{
		var targetInput = await BuildCreateDtoAsync("full.name.target");
		targetInput.FirstName = "Akshay";
		targetInput.MiddleName = "Kumar";
		targetInput.LastName = "Patel";
		targetInput.MobileNo = $"8{DateTime.UtcNow.Ticks % 1000000000:D9}";
		var target = await _personalInfoAppService.CreateAsync(targetInput);

		var outsiderInput = await BuildCreateDtoAsync("full.name.outsider");
		outsiderInput.FirstName = "Ramesh";
		outsiderInput.MiddleName = "Kumar";
		outsiderInput.LastName = "Shah";
		outsiderInput.MobileNo = $"7{DateTime.UtcNow.AddTicks(1).Ticks % 1000000000:D9}";
		var outsider = await _personalInfoAppService.CreateAsync(outsiderInput);

		var fullNameResult = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
		{
			Search = "  aKsHaY    pAtEl  ",
			SkipCount = 0,
			MaxResultCount = 50
		});
		var middleNameResult = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
		{
			Search = "akshay kumar patel",
			SkipCount = 0,
			MaxResultCount = 50
		});
		var firstNameResult = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
		{
			Search = "akshay",
			SkipCount = 0,
			MaxResultCount = 50
		});

		foreach (var query in new[] { "Akshay\u00a0Patel", "Akshay\tPatel", "Akshay\r\nPatel", "Patel Akshay" })
		{
			var result = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
			{
				Search = query,
				SkipCount = 0,
				MaxResultCount = 1
			});
			result.Items.Select(x => x.Id).ShouldBe(new[] { target.Id }, query);
			result.TotalCount.ShouldBe(1, query);
		}

		fullNameResult.Items.Select(x => x.Id).ShouldContain(target.Id);
		fullNameResult.Items.Select(x => x.Id).ShouldNotContain(outsider.Id);
		middleNameResult.Items.Select(x => x.Id).ShouldContain(target.Id);
		firstNameResult.Items.Select(x => x.Id).ShouldContain(target.Id);
		var assignedShift = await _shiftRepository.GetAsync(targetInput.ShiftId!.Value);
		var returnedShift = firstNameResult.Items.Single(x => x.Id == target.Id).Shift;
		returnedShift.ShouldNotBeNull();
		returnedShift.Name.ShouldBe(assignedShift.ShiftName);
		returnedShift.StartTime.ShouldBe(assignedShift.StartTime.ToString("HH:mm:ss"));
		returnedShift.EndTime.ShouldBe(assignedShift.EndTime.ToString("HH:mm:ss"));
	}

	[Fact]
	public async Task GetFilteredListAsync_Should_Return_Reportees_And_Descendants_For_Reporting_Manager()
	{
		var manager = await _personalInfoAppService.CreateAsync(await BuildCreateDtoAsync("mgr.scope"));
		var reporteeA = await _personalInfoAppService.CreateAsync(await BuildCreateDtoAsync("rpta.scope"));
		var reporteeB = await _personalInfoAppService.CreateAsync(await BuildCreateDtoAsync("rptb.scope"));
		var outsider = await _personalInfoAppService.CreateAsync(await BuildCreateDtoAsync("outsider.scope"));

		var employeeA = await _employeeRepository.GetAsync(reporteeA.Id);
		employeeA.AssignReportingManager(manager.Id);
		await _employeeRepository.UpdateAsync(employeeA, autoSave: true);

		var employeeB = await _employeeRepository.GetAsync(reporteeB.Id);
		employeeB.AssignReportingManager(reporteeA.Id);
		await _employeeRepository.UpdateAsync(employeeB, autoSave: true);

		var principal = BuildPrincipal(manager.Id);
		using (_currentPrincipalAccessor.Change(principal))
		{
			var result = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
			{
				SkipCount = 0,
				MaxResultCount = 50
			});

			var ids = result.Items.Select(x => x.Id).ToList();
			ids.ShouldContain(reporteeA.Id);
			ids.ShouldContain(reporteeB.Id);
			ids.ShouldNotContain(outsider.Id);
			ids.ShouldNotContain(manager.Id);
			var options = await _employeeSearchAppService.GetOptionsAsync(new GetEmployeeListInput
			{
				MaxResultCount = 50
			});
			options.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(ids.OrderBy(x => x));
		}
	}

	[Fact]
	public async Task GetFilteredListAsync_Should_Include_Future_Joining_Employees_But_Exclude_From_Active_Filter()
	{
		var futureEmployeeInput = await BuildCreateDtoAsync("future.join");
		futureEmployeeInput.DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
		var futureEmployee = await _personalInfoAppService.CreateAsync(futureEmployeeInput);

		var currentEmployeeInput = await BuildCreateDtoAsync("current.join");
		currentEmployeeInput.DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow);
		var currentEmployee = await _personalInfoAppService.CreateAsync(currentEmployeeInput);
		foreach (var id in new[] { currentEmployee.Id, futureEmployee.Id })
		{
			var employee = await _employeeRepository.GetAsync(id);
			employee.UpdateEmployeeStatus(EmployeeStatusType.Active, "Active joining-date search fixture", null, DateTimeOffset.UtcNow);
			await _employeeRepository.UpdateAsync(employee, autoSave: true);
		}

		var result = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
		{
			SkipCount = 0,
			MaxResultCount = 50
		});

		var ids = result.Items.Select(x => x.Id).ToList();
		ids.ShouldContain(currentEmployee.Id);
		ids.ShouldContain(futureEmployee.Id);

		var activeResult = await _employeeSearchAppService.GetFilteredListAsync(new GetEmployeeListInput
		{
			Status = EmployeeStatusType.Active.ToString(),
			SkipCount = 0,
			MaxResultCount = 50
		});

		var activeIds = activeResult.Items.Select(x => x.Id).ToList();
		activeIds.ShouldContain(currentEmployee.Id);
		activeIds.ShouldNotContain(futureEmployee.Id);
	}

	[Fact]
	public async Task GetOptionsAsync_Should_Not_Return_Another_Tenants_Employees()
	{
		var created = await _personalInfoAppService.CreateAsync(await BuildCreateDtoAsync("option.tenant"));
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var result = await _employeeSearchAppService.GetOptionsAsync(new GetEmployeeListInput
			{
				Search = created.FirstName,
				MaxResultCount = 25
			});
			result.Items.ShouldBeEmpty();
			result.HasMore.ShouldBeFalse();
		}
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

	private async Task<CreateEmployeePersonalInfoDto> BuildCreateDtoAsync(string prefix)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var uniq = Guid.NewGuid().ToString("N")[..8];

		return new CreateEmployeePersonalInfoDto
		{
			FirstName = $"{prefix}-{uniq}",
			LastName = "Employee",
			MobileNo = $"9{DateTime.UtcNow.Ticks % 1000000000:D9}",
			Email = $"{prefix}.{uniq}@test.com",
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
		};
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		return await EmployeeRoleTestHelper.EnsureAssignableRoleAsync(
			_roleRepository,
			_guidGenerator,
			_currentTenant.Id,
			"EmployeeSearchRole");
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
}
