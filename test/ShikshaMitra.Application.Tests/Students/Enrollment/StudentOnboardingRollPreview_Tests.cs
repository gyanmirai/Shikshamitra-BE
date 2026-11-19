using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static StudentOnboardingRollPreviewInput SelectedRollDivision(Guid divisionId) =>
		new() { DivisionId = divisionId, RollAssignmentMode = "automatic" };

	private Task<Division> CreateRollPreviewDivisionAsync(Guid standardId) => WithUnitOfWorkAsync(async () =>
	{
		var division = await GetRequiredService<DivisionManager>().CreateAsync(standardId, $"RollPreview-{Guid.NewGuid():N}"[..24], 40);
		return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
	});

	[Fact]
	public Task Roll_preview_for_unsaved_division_is_read_only_and_does_not_consume_a_number() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOnboardingFlowFixtureAsync("UnsavedRollPreview", null);
		var id = (await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "unsaved-roll-preview")).OnboardingCaseId;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(id);
		item.DivisionId.ShouldBeNull();
		var division = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		var before = await RollPreviewStateAsync(id, division.Id);
		var preview = await service.PreviewRollAsync(id, SelectedRollDivision(division.Id));
		preview.ProposedRollNumber.ShouldBe(1);
		preview.DivisionId.ShouldBe(division.Id);
		preview.RollAssignmentMode.ShouldBe("automatic");
		preview.ConcurrencyStamp.ShouldBe(item.ConcurrencyStamp);
		(await service.PreviewRollAsync(id, SelectedRollDivision(division.Id))).ProposedRollNumber.ShouldBe(1);
		(await RollPreviewStateAsync(id, division.Id)).ShouldBe(before);
	});

	[Fact]
	public Task Roll_preview_uses_selected_division_instead_of_saved_roll_and_preserves_identifier_locks() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOnboardingFlowFixtureAsync("ChangedRollPreview", null);
		var id = (await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "changed-roll-preview")).OnboardingCaseId;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(id);
		var original = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		var target = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _onboardingCaseRepository.GetAsync(id);
			saved.RecordPlacement(original.Id, Guid.NewGuid(), DateTime.UtcNow);
			saved.RecordIdentifier(null, 4, DateTime.UtcNow);
			await _onboardingCaseRepository.UpdateAsync(saved, true);
			await GetRequiredService<StudentOnboardingRollRuleReader>().RecordIssuedAsync(item.AcademicYearId, item.IntendedStandardId, target.Id, 11);
		});
		var before = await RollPreviewStateAsync(id, target.Id);
		(await service.PreviewRollAsync(id, new() { RollAssignmentMode = "automatic" })).ProposedRollNumber.ShouldBe(4);
		(await service.PreviewRollAsync(id, SelectedRollDivision(original.Id))).ProposedRollNumber.ShouldBe(4);
		(await service.PreviewRollAsync(id, SelectedRollDivision(target.Id))).ProposedRollNumber.ShouldBe(12);
		(await RollPreviewStateAsync(id, target.Id)).ShouldBe(before);
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _onboardingCaseRepository.GetAsync(id);
			saved.SetIdentifierLocked("roll", true);
			await _onboardingCaseRepository.UpdateAsync(saved, true);
		});
		(await service.PreviewRollAsync(id, SelectedRollDivision(target.Id))).ProposedRollNumber.ShouldBe(4);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id,
			new() { DivisionId = target.Id, RollAssignmentMode = "automatic", RollNumber = 12 }))).Code.ShouldBe("Students:Onboarding:IdentifierLocked");
		var occupied = await CreateExistingStudentAsync(fixture.Offering, "Occupied", "Preview");
		await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
			occupied.Id, item.AcademicYearId, item.IntendedStandardId, target.Id, 4), true));
		before = await RollPreviewStateAsync(id, target.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(target.Id)))).Code.ShouldBe(StudentOnboardingErrorCodes.RollNumberDuplicate);
		(await RollPreviewStateAsync(id, target.Id)).ShouldBe(before);
	});

	[Fact]
	public Task Roll_preview_rejects_invalid_cross_class_cross_tenant_and_unassigned_divisions() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOnboardingFlowFixtureAsync("ScopedRollPreview", null);
		var id = (await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "scoped-roll-preview")).OnboardingCaseId;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(id);
		var original = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		var target = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, new() { RollAssignmentMode = "automatic" }))).Code.ShouldBe(StudentOnboardingErrorCodes.PlacementInvalid);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(Guid.NewGuid())))).Code.ShouldBe(StudentOnboardingErrorCodes.PlacementInvalid);
		var sibling = await CreateOnboardingSettingsSiblingOfferingAsync(fixture.Offering);
		var otherClass = await CreateRollPreviewDivisionAsync(sibling.StandardId);
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(otherClass.Id)))).Code.ShouldBe(StudentOnboardingErrorCodes.PlacementInvalid);
		var foreign = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		await WithUnitOfWorkAsync(async () =>
		{
			var division = await GetRequiredService<IRepository<Division, Guid>>().GetAsync(foreign.Id);
			typeof(Division).GetField("<TenantId>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(division, Guid.NewGuid());
			await GetRequiredService<IRepository<Division, Guid>>().UpdateAsync(division, true);
			var saved = await _onboardingCaseRepository.GetAsync(id);
			saved.RecordPlacement(original.Id, Guid.NewGuid(), DateTime.UtcNow);
			await _onboardingCaseRepository.UpdateAsync(saved, true);
		});
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(foreign.Id)))).Code.ShouldBe(StudentOnboardingErrorCodes.PlacementInvalid);
		var scopedUser = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(scopedUser, $"roll-preview-{scopedUser:N}");
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: scopedUser,
			academicYearId: item.AcademicYearId, educationBoardId: item.IntendedBoardId, educationMediumId: item.IntendedMediumId,
			educationMediumStageId: item.IntendedStageId, standardId: item.IntendedStandardId, divisionId: original.Id), true));
		using (_principalAccessor.Change(ProfilePrincipal(scopedUser)))
		{
			(await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(target.Id)))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
	});

	[Fact]
	public Task Roll_preview_uses_target_published_rule_and_locked_group_without_mutation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOnboardingFlowFixtureAsync("RuleRollPreview", null);
		var id = (await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "rule-roll-preview")).OnboardingCaseId;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(id);
		var original = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		var target = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _onboardingCaseRepository.GetAsync(id);
			saved.RecordPlacement(original.Id, Guid.NewGuid(), DateTime.UtcNow);
			await _onboardingCaseRepository.UpdateAsync(saved, true);
		});
		var settings = (await service.GetSettingsAsync(item.AcademicYearId)).Single(s => s.AcademicYearId == item.AcademicYearId);
		var configuration = JsonNode.Parse(settings.ConfigurationJson)!;
		configuration["rollNumber"]!["configurations"] = JsonSerializer.SerializeToNode(new[] { new
		{
			id = "target-rule", status = "draft", version = 1, isActive = false, boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all",
			divisionMode = "selected", divisionIds = new[] { target.Id }, startingNumber = 1, preserveExisting = false, assignmentTiming = "after-division"
		} });
		var savedSettings = await service.SaveSettingsAsync(new() { AcademicYearId = item.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
		configuration["rollNumber"]!["configurations"]![0]!["status"] = "published";
		configuration["rollNumber"]!["configurations"]![0]!["isActive"] = true;
		await service.SaveSettingsAsync(new() { AcademicYearId = item.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = savedSettings.ConcurrencyStamp });
		(await service.PreviewRollAsync(id, SelectedRollDivision(original.Id))).ProposedRollNumber.ShouldBe(1);
		var before = await RollPreviewStateAsync(id, target.Id);
		await Should.ThrowAsync<BusinessException>(() => service.PreviewRollAsync(id, SelectedRollDivision(target.Id)));
		(await RollPreviewStateAsync(id, target.Id)).ShouldBe(before);
		configuration["rollNumber"]!["assignmentMode"] = "manual";
		settings = (await service.GetSettingsAsync(item.AcademicYearId)).Single(s => s.AcademicYearId == item.AcademicYearId);
		await service.SaveSettingsAsync(new() { AcademicYearId = item.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
		var lockedGroup = await CreateRollPreviewDivisionAsync(item.IntendedStandardId);
		await service.SetIdentifierLockAsync(new() { AcademicYearId = item.AcademicYearId, DivisionId = lockedGroup.Id, IdentifierType = "roll", IsLocked = true });
		before = await RollPreviewStateAsync(id, lockedGroup.Id);
		var preview = await service.PreviewRollAsync(id, SelectedRollDivision(lockedGroup.Id));
		preview.SchoolAssignmentMode.ShouldBe("automatic");
		preview.ProposedRollNumber.ShouldBe(1);
		(await RollPreviewStateAsync(id, lockedGroup.Id)).ShouldBe(before);
	});

	private Task<string> RollPreviewStateAsync(Guid id, Guid divisionId) => WithUnitOfWorkAsync(async () =>
	{
		var item = await _onboardingCaseRepository.GetAsync(id);
		var division = await GetRequiredService<IRepository<Division, Guid>>().GetAsync(divisionId);
		return JsonSerializer.Serialize(new
		{
			item.DivisionId, item.RollNumber, item.SeatReservationId, item.IdentifierAllocationId, item.ConcurrencyStamp, item.ExtraProperties,
			DivisionProperties = division.ExtraProperties, DivisionStamp = division.ConcurrencyStamp,
			Seats = await GetRequiredService<IRepository<StudentSeatReservation, Guid>>().CountAsync(),
			Identifiers = await GetRequiredService<IRepository<StudentIdentifierAllocation, Guid>>().CountAsync(),
			Events = await GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>().CountAsync(),
			Enrollments = await _studentEnrollmentRepository.CountAsync()
		});
	});
}
