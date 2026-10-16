using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionIntakeProfileKind.Inquiry)]
	[InlineData(AdmissionIntakeProfileKind.CounsellorAssignment)]
	public async Task SettingsCode_Profiles_Should_Create_Unique_References_And_Preserve_Legacy_On_Edit(
		AdmissionIntakeProfileKind kind)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var service = GetRequiredService<IAdmissionIntakeProfileAppService>();
		var configuration = kind == AdmissionIntakeProfileKind.Inquiry ? "{}" :
			JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules { CounsellorUserIds = [TestUserId] });
		var first = await service.CreateAsync(new SaveAdmissionIntakeProfileDto
		{
			Kind = kind, Name = "General admission", ConfigurationJson = configuration
		});
		var second = await service.CreateAsync(new SaveAdmissionIntakeProfileDto
		{
			Kind = kind, Name = first.Name, Code = " ", ConfigurationJson = configuration
		});
		first.Code.ShouldNotBe(second.Code);
		first.Code.ShouldStartWith(kind == AdmissionIntakeProfileKind.Inquiry ? "INQ_" : "COUNSELLOR_");
		(await service.GetListAsync(kind)).Single(item => item.Id == first.Id).Code.ShouldBe(first.Code);

		var legacy = await service.CreateAsync(new SaveAdmissionIntakeProfileDto
		{
			Kind = kind, Name = "Legacy profile", Code = "GENERAL_INQUIRY", ConfigurationJson = configuration
		});
		var edited = await service.UpdateAsync(legacy.Id, new SaveAdmissionIntakeProfileDto
		{
			Kind = kind, Name = "Renamed profile", Code = " ", ConfigurationJson = configuration,
			ConcurrencyStamp = legacy.ConcurrencyStamp
		});
		edited.Code.ShouldBe("GENERAL_INQUIRY");
		edited.Name.ShouldBe("Renamed profile");
	}

	[Fact]
	public async Task SettingsCode_Workflows_Should_Create_And_Clone_With_Independent_References()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Fresh admission" });
		var second = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = first.Name });
		var cloneOne = await _workflowAppService.CloneAsync(first.Id, new CloneAdmissionWorkflowDto { Name = "Fresh copy" });
		var cloneTwo = await _workflowAppService.CloneAsync(first.Id, new CloneAdmissionWorkflowDto { Name = "Fresh copy" });
		new[] { first.Code, second.Code, cloneOne.Code, cloneTwo.Code }.Distinct().Count().ShouldBe(4);
		first.Code.ShouldStartWith("WF_");
		(await _workflowAppService.GetAsync(first.Id)).Code.ShouldBe(first.Code);

		var input = ToUpdateInput(first);
		input.Code = " ";
		input.Name = "Fresh admission renamed";
		(await _workflowAppService.UpdateAsync(first.Id, input)).Code.ShouldBe(first.Code);
	}

	[Fact]
	public async Task SettingsCode_Workflow_Version_Edit_Should_Keep_The_Legacy_Version_Family()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var published = await CreateAndPublishCurrentWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(published.Id);
		var input = ToUpdateInput(draft);
		input.Code = string.Empty;
		input.Name = "Updated admission journey";
		var edited = await _workflowAppService.UpdateAsync(draft.Id, input);
		edited.Code.ShouldBe(published.Code);
		edited.Version.ShouldBe(2);
		edited.ParentVersionId.ShouldBe(published.Id);
		(await _workflowAppService.GetVersionHistoryAsync(edited.Id)).Select(item => item.Id)
			.ShouldContain(published.Id);
	}

	[Fact]
	public async Task SettingsCode_Forms_Should_Create_And_Clone_With_Independent_References()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await _formAppService.CreateAsync(new CreateUpdateAdmissionFormDto { Name = "Registration" });
		var second = await _formAppService.CreateAsync(new CreateUpdateAdmissionFormDto { Name = first.Name });
		var cloneOne = await _formAppService.CloneAsync(first.Id, new CloneAdmissionFormDto { Name = "Registration copy" });
		var cloneTwo = await _formAppService.CloneAsync(first.Id, new CloneAdmissionFormDto { Name = "Registration copy" });
		new[] { first.Code, second.Code, cloneOne.Code, cloneTwo.Code }.Distinct().Count().ShouldBe(4);
		first.Code.ShouldStartWith("FORM_");
		(await _formAppService.GetAsync(first.Id)).Code.ShouldBe(first.Code);
		var edited = await _formAppService.UpdateAsync(first.Id, new CreateUpdateAdmissionFormDto
		{
			Name = "Registration renamed", ConcurrencyStamp = first.ConcurrencyStamp
		});
		edited.Code.ShouldBe(first.Code);
	}

	[Fact]
	public async Task SettingsCode_Form_Version_Edit_Should_Keep_The_Legacy_Version_Family()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var legacyForm = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Legacy registration",
			"LEGACY_REGISTRATION", AdmissionFormLayoutType.SinglePage);
		legacyForm.Publish(TestUserId);
		await WithUnitOfWorkAsync(() => _formRepository.InsertAsync(legacyForm, autoSave: true));
		var published = await _formAppService.GetAsync(legacyForm.Id);
		var draft = await _formAppService.CreateNewVersionAsync(published.Id);
		var edited = await _formAppService.UpdateAsync(draft.Id, new CreateUpdateAdmissionFormDto
		{
			Name = "Updated registration", Code = " ", Purpose = draft.Purpose,
			LayoutType = draft.LayoutType, ConcurrencyStamp = draft.ConcurrencyStamp
		});
		edited.Code.ShouldBe(published.Code);
		edited.Version.ShouldBe(2);
		edited.ParentVersionId.ShouldBe(published.Id);
		(await _formAppService.GetVersionsAsync(edited.Id)).Select(item => item.Id)
			.ShouldContain(published.Id);
	}

	[Fact]
	public async Task SettingsCode_Cycles_Should_Create_And_Clone_Unique_References_And_Preserve_Legacy_On_Edit()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
		var board = await _boardRepository.GetAsync(medium.EducationBoardId);
		var service = GetRequiredService<IAdmissionCycleAppService>();
		await EnsureTestIdentityUserAsync(TestUserId, "cycle-fixture-admin");
		await _scopeRepository.InsertAsync(new ShikshaMitra.Security.UserScope.UserScopeAssignment(
			Guid.NewGuid(), TestTenantId, userId: TestUserId, academicYearId: board.AcademicYearId), true);
		await GetRequiredService<ShikshaMitra.Security.UserScope.IUserScopeService>().InvalidateUserScopeCacheAsync(TestUserId);
		var input = new CreateUpdateAdmissionCycleDto
		{
			Name = "Admission 2026-27", AcademicYearId = board.AcademicYearId,
			StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
		};
		var first = await service.CreateAsync(input);
		var second = await service.CreateAsync(input);
		var cloneInput = new CloneAdmissionCycleDto
		{
			SourceCycleId = first.Id, Name = "Admission copy", AcademicYearId = input.AcademicYearId,
			StartDate = input.StartDate, EndDate = input.EndDate
		};
		var cloneOne = await service.CloneAsync(cloneInput);
		var cloneTwo = await service.CloneAsync(cloneInput);
		new[] { first.Code, second.Code, cloneOne.Code, cloneTwo.Code }.Distinct().Count().ShouldBe(4);
		first.Code.ShouldStartWith("CYCLE_");
		(await service.GetAsync(first.Id)).Code.ShouldBe(first.Code);

		input.Code = "ADMISSION_2026";
		var legacy = await service.CreateAsync(input);
		input.Code = string.Empty;
		input.Name = "Admission period renamed";
		input.ConcurrencyStamp = legacy.ConcurrencyStamp;
		(await service.UpdateAsync(legacy.Id, input)).Code.ShouldBe("ADMISSION_2026");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void SettingsCode_Validators_Should_Accept_Omitted_References_And_Reject_Invalid_Supplied_Codes(string? code)
	{
		var workflowValidator = new CreateUpdateAdmissionWorkflowDtoValidator();
		var workflow = new CreateUpdateAdmissionWorkflowDto { Name = "Fresh", Code = code! };
		workflowValidator.Validate(workflow).IsValid.ShouldBeTrue();
		workflow.Code = "invalid code";
		workflowValidator.Validate(workflow).Errors.ShouldContain(error => error.PropertyName == nameof(workflow.Code));
		workflow.Code = new string('A', AdmissionWorkflowConsts.MaxCodeLength + 1);
		workflowValidator.Validate(workflow).IsValid.ShouldBeFalse();

		var profileValidator = new SaveAdmissionIntakeProfileDtoValidator();
		var profile = new SaveAdmissionIntakeProfileDto { Name = "General", Code = code!, ConfigurationJson = "{}" };
		profileValidator.Validate(profile).IsValid.ShouldBeTrue();
		profile.Code = "invalid code";
		profileValidator.Validate(profile).IsValid.ShouldBeFalse();
		profile.Code = new string('A', 65);
		profileValidator.Validate(profile).IsValid.ShouldBeFalse();

		var cycleValidator = new CreateUpdateAdmissionCycleDtoValidator();
		var cycle = new CreateUpdateAdmissionCycleDto
		{
			Name = "Admission", Code = code!, AcademicYearId = Guid.NewGuid(),
			StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
		};
		cycleValidator.Validate(cycle).IsValid.ShouldBeTrue();
		cycle.Code = new string('A', AdmissionCycleConsts.MaxCodeLength + 1);
		cycleValidator.Validate(cycle).IsValid.ShouldBeFalse();
	}
}
