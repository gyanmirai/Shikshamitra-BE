using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeePersonalInfoAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeStatusAppService _employeeStatusAppService;
	private readonly EmployeePersonalInfoProjection _personalInfoProjection;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IdentityUserManager _userManager;
	private readonly EmployeeManager _employeeManager;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public EmployeePersonalInfoAppService_Tests()
	{
		_personalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeStatusAppService = GetRequiredService<IEmployeeStatusAppService>();
		_personalInfoProjection = GetRequiredService<EmployeePersonalInfoProjection>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_userRepository = GetRequiredService<IIdentityUserRepository>();
		_userManager = GetRequiredService<IdentityUserManager>();
		_employeeManager = GetRequiredService<EmployeeManager>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
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
		result.PanNo.ShouldBe(input.PanNo);
		result.AdharNo.ShouldBe(input.AdharNo);
		result.EmployeeCode.ShouldNotBeNullOrEmpty();
		result.UserId.ShouldNotBeNull();
		result.UserId.Value.ShouldNotBe(Guid.Empty);
		result.ShiftId.ShouldBe(input.ShiftId);
		result.Shift.ShouldNotBeNull();
		result.Shift!.Id.ShouldBe(input.ShiftId!.Value);

		var linkedUser = await _userRepository.FindAsync(result.UserId.Value);
		linkedUser.ShouldNotBeNull();
		linkedUser!.Email.ShouldBe(input.Email);
	}

	[Fact]
	public async Task GetAsync_Should_Return_Selected_Shift_Details()
	{
		var input = await BuildCreateDtoAsync($"shift.read.{Guid.NewGuid():N}@test.com");
		var created = await _personalInfoAppService.CreateAsync(input);

		var result = await _personalInfoAppService.GetAsync(created.Id);

		result.ShiftId.ShouldBe(input.ShiftId);
		result.Shift.ShouldNotBeNull();
		result.Shift!.Id.ShouldBe(input.ShiftId!.Value);
		result.Shift.ShiftName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Projection_Should_Resolve_Shift_When_Navigation_Is_Not_Loaded()
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var employee = new Employee(
			Guid.NewGuid(),
			_currentTenant.Id,
			null,
			$"DET-{Guid.NewGuid():N}"[..12],
			new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Employee",
				FirstName: "Detached",
				MiddleName: null,
				ShortName: null,
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
				FamilyCast: null,
				CastType: null,
				GenderType: GenderType.Male,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: "9876543210",
				SecondaryMobileNo: null,
				Email: $"detached.shift.{Guid.NewGuid():N}@test.com",
				PanNo: UniquePan(),
				AdharNo: UniqueAdhar(),
				Address1: null,
				Address2: null,
				Address3: null,
				Department: null,
				Designation: null,
				DateOfJoining: DateOnly.FromDateTime(DateTime.UtcNow.Date),
				EmployeeGroup: null,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: shiftId),
			bankDetails: null,
			workExperiences: []);

		var result = await _personalInfoProjection.ToPersonalInfoDtoAsync(employee);

		result.ShiftId.ShouldBe(shiftId);
		result.Shift.ShouldNotBeNull();
		result.Shift!.Id.ShouldBe(shiftId);
		result.Shift.ShiftName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task CreateAsync_Should_Create_Login_Credentials_That_Match_Linked_User()
	{
		var input = await BuildCreateDtoAsync($"login.credentials.{Guid.NewGuid():N}@test.com");

		var result = await _personalInfoAppService.CreateAsync(input);

		result.UserId.ShouldNotBeNull();
		var linkedUser = await _userRepository.GetAsync(result.UserId.Value);
		linkedUser.UserName.ShouldBe(input.Email);
		linkedUser.Email.ShouldBe(input.Email);
		linkedUser.EmailConfirmed.ShouldBeTrue();

		var generatedPassword = BuildInitialPassword(linkedUser.UserName!, input.MobileNo);
		var canLoginWithGeneratedPassword = await _userManager.CheckPasswordAsync(linkedUser, generatedPassword);
		canLoginWithGeneratedPassword.ShouldBeTrue();
	}

	[Fact]
	public async Task CreateAsync_Should_Use_Email_Login_For_Duplicate_Employee_Names()
	{
		var firstInput = await BuildCreateDtoAsync($"login.duplicate.first.{Guid.NewGuid():N}@test.com");
		firstInput.FirstName = "Collision";
		firstInput.LastName = "Login";
		firstInput.MobileNo = "9876543211";
		await _personalInfoAppService.CreateAsync(firstInput);

		var secondInput = await BuildCreateDtoAsync($"login.duplicate.second.{Guid.NewGuid():N}@test.com");
		secondInput.FirstName = firstInput.FirstName;
		secondInput.LastName = firstInput.LastName;
		secondInput.MobileNo = "9876543212";

		var result = await _personalInfoAppService.CreateAsync(secondInput);

		result.UserId.ShouldNotBeNull();
		var linkedUser = await _userRepository.GetAsync(result.UserId.Value);
		linkedUser.UserName.ShouldBe(secondInput.Email);
		linkedUser.Email.ShouldBe(secondInput.Email);

		var generatedPassword = BuildInitialPassword(linkedUser.UserName!, secondInput.MobileNo);
		var canLoginWithGeneratedPassword = await _userManager.CheckPasswordAsync(linkedUser, generatedPassword);
		canLoginWithGeneratedPassword.ShouldBeTrue();
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

	[Fact]
	public async Task CreateAsync_Should_Assign_Identity_User_To_Selected_Role()
	{
		var input = await BuildCreateDtoAsync($"role.assign.{Guid.NewGuid():N}@test.com");

		var result = await _personalInfoAppService.CreateAsync(input);

		result.UserId.ShouldNotBeNull();
		var user = await _userRepository.GetAsync(result.UserId.Value);
		var role = await _roleRepository.GetAsync(input.RoleId);

		(role.Name is not null).ShouldBeTrue();
		var inRole = await _userManager.IsInRoleAsync(user, role.Name!);
		inRole.ShouldBeTrue();
	}

	[Theory]
	[InlineData("Student")]
	[InlineData("Parent")]
	[InlineData(" parent ")]
	public async Task CreateAsync_Should_Reject_Reserved_NonEmployee_Roles(string roleName)
	{
		var normalizedRoleName = roleName.Trim();
		var existingRole = (await _roleRepository.GetListAsync()).FirstOrDefault(role =>
			string.Equals(role.Name?.Trim(), normalizedRoleName, StringComparison.OrdinalIgnoreCase));
		var roleId = existingRole?.Id ?? _guidGenerator.Create();
		if (existingRole == null)
		{
			await _roleRepository.InsertAsync(
				new IdentityRole(roleId, roleName, _currentTenant.Id),
				autoSave: true);
		}

		var input = await BuildCreateDtoAsync($"role.reserved.{Guid.NewGuid():N}@test.com");
		input.RoleId = roleId;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleNotAllowed);
		exception.Message.ShouldContain("Employee audience");
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Renamed_NonEmployee_Role_By_Audience()
	{
		var role = new IdentityRole(
			_guidGenerator.Create(),
			$"Learner Support {Guid.NewGuid():N}",
			_currentTenant.Id);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Student);
		await _roleRepository.InsertAsync(role, autoSave: true);

		var input = await BuildCreateDtoAsync($"role.audience.{Guid.NewGuid():N}@test.com");
		input.RoleId = role.Id;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleNotAllowed);
		exception.Data["roleAudience"].ShouldBe(EmployeeRoleAudience.Student);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Employee_Role_Without_Explicit_Onboarding_Approval()
	{
		var role = new IdentityRole(
			_guidGenerator.Create(),
			$"Unapproved Employee Role {Guid.NewGuid():N}",
			_currentTenant.Id);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		await _roleRepository.InsertAsync(role, autoSave: true);
		var input = await BuildCreateDtoAsync($"role.unapproved.{Guid.NewGuid():N}@test.com");
		input.RoleId = role.Id;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleAssignmentNotAllowed);
		exception.Data["roleId"].ShouldBe(role.Id);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Approved_Role_Without_Explicit_Employee_Audience()
	{
		var role = new IdentityRole(
			_guidGenerator.Create(),
			$"Missing Audience Role {Guid.NewGuid():N}",
			_currentTenant.Id);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await _roleRepository.InsertAsync(role, autoSave: true);
		var input = await BuildCreateDtoAsync($"role.missing.audience.{Guid.NewGuid():N}@test.com");
		input.RoleId = role.Id;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleNotAllowed);
	}

	[Theory]
	[InlineData("admin")]
	[InlineData("super-admin")]
	public async Task CreateAsync_Should_Reject_Reserved_System_Role_Even_With_Stale_Approval(
		string roleName)
	{
		var role = (await _roleRepository.GetListAsync()).FirstOrDefault(role =>
			string.Equals(role.Name, roleName, StringComparison.OrdinalIgnoreCase));
		var isNewRole = role == null;
		role ??= new IdentityRole(_guidGenerator.Create(), roleName, _currentTenant.Id);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.NonTeaching);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		if (isNewRole)
		{
			await _roleRepository.InsertAsync(role, autoSave: true);
		}
		else
		{
			await _roleRepository.UpdateAsync(role, autoSave: true);
		}
		var input = await BuildCreateDtoAsync($"role.reserved.system.{Guid.NewGuid():N}@test.com");
		input.RoleId = role.Id;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleAssignmentNotAllowed);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Missing_Employee_Group()
	{
		var input = await BuildCreateDtoAsync($"role.group.required.{Guid.NewGuid():N}@test.com");
		input.EmployeeGroup = null;

		var exception = await Should.ThrowAsync<AbpValidationException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.ValidationErrors.ShouldContain(error =>
			error.MemberNames.Contains(nameof(CreateEmployeePersonalInfoDto.EmployeeGroup)) &&
			error.ErrorMessage != null &&
			error.ErrorMessage.Contains(
				"Employee group is required.",
				StringComparison.Ordinal));
	}

	[Fact]
	public async Task Employee_Manager_Should_Reject_Missing_Employee_Group_When_Validation_Is_Bypassed()
	{
		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_employeeManager.CreateEmployeeAsync(
				userId: Guid.NewGuid(),
				roleId: Guid.NewGuid(),
				firstName: "Missing",
				lastName: "Group",
				middleName: null,
				shortName: "MG",
				employeeStatusType: EmployeeStatusType.Active,
				dateOfBirth: new DateOnly(1990, 1, 1)));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeGroupRequired);
		exception.Message.ShouldBe("Employee group is required.");
	}

	[Fact]
	public void Basic_Info_Create_Contract_Should_Reject_Missing_Employee_Group()
	{
		var validator = new CreateEmployeeBasicInfoDtoValidator(GetRequiredService<IClock>());

		var result = validator.Validate(new CreateEmployeeBasicInfoDto());

		result.Errors.ShouldContain(error =>
			error.PropertyName == nameof(CreateEmployeeBasicInfoDto.EmployeeGroup) &&
			error.ErrorMessage == "Employee group is required.");
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Role_For_A_Different_Employee_Group()
	{
		var teachingRole = await CreateEmployeeRoleAsync(EmployeeGroupAudience.Teaching);
		var input = await BuildCreateDtoAsync($"role.group.create.{Guid.NewGuid():N}@test.com");
		input.RoleId = teachingRole.Id;
		input.EmployeeGroup = EmployeeGroupType.NonTeaching;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleGroupNotAllowed);
		exception.Data["employeeGroup"].ShouldBe(EmployeeGroupType.NonTeaching);
		exception.Data["employeeGroupAudience"].ShouldBe(EmployeeGroupAudience.Teaching);
	}

	[Fact]
	public async Task UpdateAsync_Should_Update_Basic_Info_Without_Requiring_Payroll_Structure()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"basic.update.{Guid.NewGuid():N}@test.com"));

		var result = await _personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
		{
			FirstName = created.FirstName,
			LastName = created.LastName,
			MobileNo = created.MobileNo,
			Email = created.Email,
			PanNo = created.PanNo,
			AdharNo = created.AdharNo,
			DateOfBirth = created.DateOfBirth,
			GenderType = created.GenderType,
			DateOfJoining = created.DateOfJoining,
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = created.RoleId,
			ShiftId = created.ShiftId
		});

		result.Email.ShouldBe(created.Email);
		result.PanNo.ShouldBe(created.PanNo);
		result.AdharNo.ShouldBe(created.AdharNo);
		result.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
		result.EmployeeGroup.ShouldBe(created.EmployeeGroup);
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_Reserved_Role_Before_Removing_Current_Role()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"role.update.reserved.{Guid.NewGuid():N}@test.com"));
		var parentRole = (await _roleRepository.GetListAsync()).FirstOrDefault(role =>
			string.Equals(role.Name?.Trim(), "Parent", StringComparison.OrdinalIgnoreCase));
		if (parentRole == null)
		{
			parentRole = await _roleRepository.InsertAsync(
				new IdentityRole(_guidGenerator.Create(), "Parent", _currentTenant.Id),
				autoSave: true);
		}

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
			{
				FirstName = created.FirstName,
				LastName = created.LastName,
				MobileNo = created.MobileNo,
				Email = created.Email,
				PanNo = created.PanNo,
				AdharNo = created.AdharNo,
				DateOfBirth = created.DateOfBirth,
				GenderType = created.GenderType,
				DateOfJoining = created.DateOfJoining,
				EmployeeStatusType = EmployeeStatusType.Active,
				RoleId = parentRole.Id,
				ShiftId = created.ShiftId
			}));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleNotAllowed);
		var user = await _userRepository.GetAsync(created.UserId!.Value);
		var originalRole = await _roleRepository.GetAsync(created.RoleId);
		(await _userManager.IsInRoleAsync(user, originalRole.Name!)).ShouldBeTrue();
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_A_Group_Incompatible_Role_Before_Removing_Current_Role()
	{
		var currentRole = await CreateEmployeeRoleAsync(EmployeeGroupAudience.Teaching);
		var input = await BuildCreateDtoAsync($"role.group.swap.{Guid.NewGuid():N}@test.com");
		input.RoleId = currentRole.Id;
		input.EmployeeGroup = EmployeeGroupType.Teaching;
		var created = await _personalInfoAppService.CreateAsync(input);
		var nonTeachingRole = await CreateEmployeeRoleAsync(EmployeeGroupAudience.NonTeaching);

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
			{
				FirstName = created.FirstName,
				LastName = created.LastName,
				MobileNo = created.MobileNo,
				Email = created.Email,
				PanNo = created.PanNo,
				AdharNo = created.AdharNo,
				DateOfBirth = created.DateOfBirth,
				GenderType = created.GenderType,
				DateOfJoining = created.DateOfJoining,
				EmployeeGroup = EmployeeGroupType.Teaching,
				EmployeeStatusType = EmployeeStatusType.Active,
				RoleId = nonTeachingRole.Id,
				ShiftId = created.ShiftId
			}));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleGroupNotAllowed);
		var user = await _userRepository.GetAsync(created.UserId!.Value);
		(await _userManager.IsInRoleAsync(user, currentRole.Name!)).ShouldBeTrue();
	}

	[Fact]
	public async Task UpdateAsync_Should_Preserve_An_Unchanged_Legacy_Custom_Role()
	{
		var legacyRole = await CreateEmployeeRoleAsync(EmployeeGroupAudience.Both);
		var input = await BuildCreateDtoAsync($"role.legacy.retention.{Guid.NewGuid():N}@test.com");
		input.RoleId = legacyRole.Id;
		var created = await _personalInfoAppService.CreateAsync(input);
		using (var metadataUow = _unitOfWorkManager.Begin(requiresNew: true))
		{
			var persistedLegacyRole = await _roleRepository.GetAsync(legacyRole.Id);
			persistedLegacyRole.ExtraProperties.Remove(EmployeeRoleAudience.ExtraPropertyName);
			persistedLegacyRole.ExtraProperties.Remove(
				EmployeeOnboardingRoleAssignment.ExtraPropertyName);
			await _roleRepository.UpdateAsync(persistedLegacyRole, autoSave: true);
			await metadataUow.CompleteAsync();
		}

		EmployeePersonalInfoDto updated;
		using (var updateUow = _unitOfWorkManager.Begin(requiresNew: true))
		{
			updated = await _personalInfoAppService.UpdateAsync(
				created.Id,
				new UpdateEmployeePersonalInfoDto
				{
					FirstName = "Legacy",
					LastName = created.LastName,
					MobileNo = created.MobileNo,
					Email = created.Email,
					PanNo = created.PanNo,
					AdharNo = created.AdharNo,
					DateOfBirth = created.DateOfBirth,
					GenderType = created.GenderType,
					DateOfJoining = created.DateOfJoining,
					EmployeeGroup = created.EmployeeGroup,
					EmployeeStatusType = EmployeeStatusType.Active,
					RoleId = legacyRole.Id,
					ShiftId = created.ShiftId
				});
			await updateUow.CompleteAsync();
		}

		updated.FirstName.ShouldBe("Legacy");
		updated.RoleId.ShouldBe(legacyRole.Id);
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_Group_Change_When_The_Current_Role_Is_Incompatible()
	{
		var teachingRole = await CreateEmployeeRoleAsync(EmployeeGroupAudience.Teaching);
		var input = await BuildCreateDtoAsync($"role.group.update.{Guid.NewGuid():N}@test.com");
		input.RoleId = teachingRole.Id;
		input.EmployeeGroup = EmployeeGroupType.Teaching;
		var created = await _personalInfoAppService.CreateAsync(input);

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
			{
				FirstName = created.FirstName,
				LastName = created.LastName,
				MobileNo = created.MobileNo,
				Email = created.Email,
				PanNo = created.PanNo,
				AdharNo = created.AdharNo,
				DateOfBirth = created.DateOfBirth,
				GenderType = created.GenderType,
				DateOfJoining = created.DateOfJoining,
				EmployeeGroup = EmployeeGroupType.NonTeaching,
				EmployeeStatusType = EmployeeStatusType.Active,
				RoleId = teachingRole.Id,
				ShiftId = created.ShiftId
			}));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeRoleGroupNotAllowed);
		var unchanged = await _personalInfoAppService.GetAsync(created.Id);
		unchanged.EmployeeGroup.ShouldBe(EmployeeGroupType.Teaching);
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_Email_Change()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"email.immutable.{Guid.NewGuid():N}@test.com"));

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			_personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
			{
				FirstName = created.FirstName,
				LastName = created.LastName,
				MobileNo = created.MobileNo,
				Email = $"changed.{Guid.NewGuid():N}@test.com",
				PanNo = created.PanNo,
				AdharNo = created.AdharNo,
				DateOfBirth = created.DateOfBirth,
				GenderType = created.GenderType,
				DateOfJoining = created.DateOfJoining,
				EmployeeStatusType = EmployeeStatusType.Active,
				RoleId = created.RoleId,
				ShiftId = created.ShiftId
			}));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeEmailCannotBeChanged);
		exception.Data["field"].ShouldBe(nameof(UpdateEmployeePersonalInfoDto.Email));
		exception.Data.Contains("existingEmail").ShouldBeFalse();
		exception.Data.Contains("requestedEmail").ShouldBeFalse();
	}

	[Fact]
	public async Task UpdateAsync_Should_Update_Pan_And_Adhar()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"identity.update.{Guid.NewGuid():N}@test.com"));
		var panNo = UniquePan();
		var adharNo = UniqueAdhar();

		var result = await _personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
		{
			FirstName = created.FirstName,
			LastName = created.LastName,
			MobileNo = created.MobileNo,
			Email = created.Email,
			PanNo = panNo,
			AdharNo = adharNo,
			DateOfBirth = created.DateOfBirth,
			GenderType = created.GenderType,
			DateOfJoining = created.DateOfJoining,
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = created.RoleId,
			ShiftId = created.ShiftId
		});

		result.PanNo.ShouldBe(panNo);
		result.AdharNo.ShouldBe(adharNo);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Duplicate_Pan()
	{
		var first = await BuildCreateDtoAsync($"duplicate.pan.1.{Guid.NewGuid():N}@test.com");
		await _personalInfoAppService.CreateAsync(first);
		var second = await BuildCreateDtoAsync($"duplicate.pan.2.{Guid.NewGuid():N}@test.com");
		second.PanNo = first.PanNo;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
			await _personalInfoAppService.CreateAsync(second));

		exception.Code.ShouldBe(HRErrorCodes.EmployeePanAlreadyExists);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Duplicate_Adhar()
	{
		var first = await BuildCreateDtoAsync($"duplicate.adhar.1.{Guid.NewGuid():N}@test.com");
		await _personalInfoAppService.CreateAsync(first);
		var second = await BuildCreateDtoAsync($"duplicate.adhar.2.{Guid.NewGuid():N}@test.com");
		second.AdharNo = first.AdharNo;

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
			await _personalInfoAppService.CreateAsync(second));

		exception.Code.ShouldBe(HRErrorCodes.EmployeeAdharAlreadyExists);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Nonexistent_ShiftId()
	{
		var input = await BuildCreateDtoAsync($"missing.shift.create.{Guid.NewGuid():N}@test.com");
		input.ShiftId = Guid.NewGuid();

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
			await _personalInfoAppService.CreateAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_Nonexistent_ShiftId()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"missing.shift.update.{Guid.NewGuid():N}@test.com"));

		var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
			await _personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
			{
				FirstName = created.FirstName,
				LastName = created.LastName,
				MobileNo = created.MobileNo,
				Email = created.Email,
				PanNo = created.PanNo,
				AdharNo = created.AdharNo,
				DateOfBirth = created.DateOfBirth,
				GenderType = created.GenderType,
				DateOfJoining = created.DateOfJoining,
				EmployeeStatusType = EmployeeStatusType.Active,
				RoleId = created.RoleId,
				ShiftId = Guid.NewGuid()
			}));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
	}

	[Fact]
	public async Task UpdateAsync_Should_Preserve_Existing_Inactive_Status()
	{
		var created = await _personalInfoAppService.CreateAsync(
			await BuildCreateDtoAsync($"inactive.update.{Guid.NewGuid():N}@test.com"));
		await _employeeStatusAppService.ChangeStatusAsync(created.Id, new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Inactive,
			Remarks = "Temporarily inactive",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
		});

		var result = await _personalInfoAppService.UpdateAsync(created.Id, new UpdateEmployeePersonalInfoDto
		{
			FirstName = "Updated",
			LastName = created.LastName,
			MobileNo = created.MobileNo,
			Email = created.Email,
			PanNo = created.PanNo,
			AdharNo = created.AdharNo,
			DateOfBirth = created.DateOfBirth,
			GenderType = created.GenderType,
			DateOfJoining = created.DateOfJoining,
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = created.RoleId,
			ShiftId = created.ShiftId
		});

		result.FirstName.ShouldBe("Updated");
		result.EmployeeStatusType.ShouldBe(EmployeeStatusType.Inactive);
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

	private static string BuildInitialPassword(string username, string mobileNo)
	{
		var last4Digits = mobileNo[^4..];
		var capitalizedUsername = char.ToUpper(username[0]) + username[1..];
		return $"{capitalizedUsername}@{last4Digits}";
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		var existing = roles.FirstOrDefault(role =>
			string.Equals(
				role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName),
				EmployeeRoleAudience.Employee,
				StringComparison.Ordinal) &&
			role.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName) &&
			string.Equals(
				EmployeeGroupAudience.ResolveOrDefault(
					role.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName)),
				EmployeeGroupAudience.Both,
				StringComparison.Ordinal));
		if (existing != null) return existing.Id;

		var id = _guidGenerator.Create();
		var role = new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await _roleRepository.InsertAsync(role, autoSave: true);
		return id;
	}

	private async Task<IdentityRole> CreateEmployeeRoleAsync(string employeeGroupAudience)
	{
		var role = new IdentityRole(
			_guidGenerator.Create(),
			$"Employee Role {Guid.NewGuid():N}",
			_currentTenant.Id);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, employeeGroupAudience);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		return await _roleRepository.InsertAsync(role, autoSave: true);
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
