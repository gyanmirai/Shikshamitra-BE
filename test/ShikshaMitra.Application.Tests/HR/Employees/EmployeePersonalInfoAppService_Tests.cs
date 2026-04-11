using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeePersonalInfoAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeePersonalInfoAppService_Tests()
	{
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task CreateAsync_Should_Create_Employee_Without_ProfilePicture()
	{
		var input = await BuildCreateDtoAsync();

		var result = await _personalInfoAppService.CreateAsync(input);

		result.ShouldNotBeNull();
		result.Id.ShouldNotBe(Guid.Empty);
		result.FirstName.ShouldBe(input.FirstName);
		result.LastName.ShouldBe(input.LastName);
		result.EmployeeCode.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task CreateWithProfilePictureAsync_Should_Create_Employee_With_Picture()
	{
		var input = await BuildCreateDtoAsync("pic.employee@test.com");
		var pngBytes = BuildMinimalPng();

		var result = await _personalInfoAppService.CreateWithProfilePictureAsync(
			input, "avatar.png", pngBytes);

		result.ShouldNotBeNull();
		result.Id.ShouldNotBe(Guid.Empty);
		result.FirstName.ShouldBe(input.FirstName);

		var reloaded = await _personalInfoAppService.GetAsync(result.Id);
		reloaded.ShouldNotBeNull();
		reloaded.EmployeeCode.ShouldBe(result.EmployeeCode);
	}

	[Fact]
	public async Task CreateWithProfilePictureAsync_Should_Reject_Invalid_Extension()
	{
		var input = await BuildCreateDtoAsync("bad.ext@test.com");
		var pngBytes = BuildMinimalPng();

		await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
		{
			await _personalInfoAppService.CreateWithProfilePictureAsync(
				input, "avatar.bmp", pngBytes);
		});
	}

	#region Helpers

	private async Task<CreateEmployeePersonalInfoDto> BuildCreateDtoAsync(string? email = null)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		return new CreateEmployeePersonalInfoDto
		{
			FirstName = "Test",
			LastName = "Employee",
			MobileNo = "9876543210",
			Email = email ?? $"test.emp.{Guid.NewGuid():N}@test.com",
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId
		};
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

	/// <summary>
	/// Smallest valid PNG: 1x1 transparent pixel.
	/// Magic bytes 89 50 4E 47 followed by the remaining required chunks.
	/// </summary>
	private static byte[] BuildMinimalPng()
	{
		return
		[
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk length + type
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
			0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, // 8-bit RGBA
			0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, // IDAT chunk
			0x54, 0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02,
			0x00, 0x01, 0xE5, 0x27, 0xDE, 0xFC, 0x00, 0x00, // zlib data
			0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, // IEND
			0x60, 0x82
		];
	}

	#endregion
}
