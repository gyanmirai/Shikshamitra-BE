using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Intake_Setup_Should_Create_And_Update_Profiles_Atomically_Without_Legacy_Bypass()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await EnsureSelectableAdmissionUserAsync();
		var service = GetRequiredService<IAdmissionIntakeSetupAppService>();
		var legacyService = GetRequiredService<IAdmissionIntakeProfileAppService>();

		var created = await service.CreateAsync(ValidSetupInput("General admission"));
		created.InquiryIntakeProfileId.ShouldNotBe(Guid.Empty);
		created.CounsellorAssignmentProfileId.ShouldNotBe(Guid.Empty);
		created.CounsellorAssignmentRules.FallbackManagerUserId.ShouldBe(TestUserId);
		(await legacyService.GetListAsync()).Select(item => item.Id)
			.ShouldContain(created.InquiryIntakeProfileId);
		(await legacyService.GetListAsync()).Select(item => item.Id)
			.ShouldContain(created.CounsellorAssignmentProfileId);

		var profileRepository = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
		var managedProfile = await profileRepository.GetAsync(created.InquiryIntakeProfileId);
		var bypass = await Should.ThrowAsync<BusinessException>(() => legacyService.UpdateAsync(managedProfile.Id,
			new SaveAdmissionIntakeProfileDto
			{
				Kind = managedProfile.Kind, Code = managedProfile.Code, Name = "Bypass",
				ConfigurationJson = managedProfile.ConfigurationJson,
				ConcurrencyStamp = managedProfile.ConcurrencyStamp
			}));
		bypass.Code.ShouldBe(AdmissionIntakeProfileErrorCodes.SetupManagedProfile);

		var input = ValidUpdateInput(created, "General admission updated");
		input.InquiryRules.FirstContactDueInHours = 12;
		var updated = await service.UpdateAsync(created.Id, input);
		updated.Name.ShouldBe("General admission updated");
		updated.InquiryRules.FirstContactDueInHours.ShouldBe(12);
		var boundary = await service.CreateAsync(ValidSetupInput(new string('A', 128)));
		boundary.Name.Length.ShouldBe(128);
		(await profileRepository.GetAsync(boundary.InquiryIntakeProfileId)).Name.Length.ShouldBe(128);
	}

	[Fact]
	public async Task Intake_Setup_Usage_Should_Show_History_And_Block_Archive_Only_While_Operational()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await EnsureSelectableAdmissionUserAsync();
		var service = GetRequiredService<IAdmissionIntakeSetupAppService>();
		var created = await service.CreateAsync(ValidSetupInput("Archive policy"));
		var offering = await CreateOptionalIntakeOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var offeringRepository = GetRequiredService<IRepository<AdmissionOffering, Guid>>();
			var currentOffering = await offeringRepository.GetAsync(offering.Id);
			currentOffering.ConfigureIntake(created.InquiryIntakeProfileId, created.CounsellorAssignmentProfileId);
			await offeringRepository.UpdateAsync(currentOffering, autoSave: true);
		});
		offering.ConfigureIntake(created.InquiryIntakeProfileId, created.CounsellorAssignmentProfileId);
		var linkRepository = GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>();
		var link = new AdmissionPublicInquiryLink(Guid.NewGuid(), TestTenantId, offering.AdmissionCycleId,
			created.CounsellorAssignmentProfileId, "Expired campaign", new string('A', 64),
			AdmissionInquirySource.Website, null, DateTime.UtcNow.AddDays(-1));
		await linkRepository.InsertAsync(link, autoSave: true);

		var usage = await service.GetUsageAsync(created.Id);
		usage.Offerings.Single().IsActive.ShouldBeTrue();
		usage.PublicInquiryLinks.Single().IsOperational.ShouldBeFalse();
		usage.ActiveOfferingCount.ShouldBe(1);
		usage.ActivePublicInquiryLinkCount.ShouldBe(0);
		var blocked = await Should.ThrowAsync<BusinessException>(() => service.ArchiveAsync(created.Id,
			new ArchiveAdmissionIntakeSetupDto { ConcurrencyStamp = created.ConcurrencyStamp }));
		blocked.Code.ShouldBe(AdmissionIntakeProfileErrorCodes.SetupInUse);

		offering.Deactivate();
		await WithUnitOfWorkAsync(async () =>
		{
			var offeringRepository = GetRequiredService<IRepository<AdmissionOffering, Guid>>();
			var currentOffering = await offeringRepository.GetAsync(offering.Id);
			currentOffering.Deactivate();
			await offeringRepository.UpdateAsync(currentOffering, autoSave: true);
		});
		created = await service.UpdateAsync(created.Id, ValidUpdateInput(created, "Archive policy updated"));
		created.Name.ShouldBe("Archive policy updated");
		created = (await service.GetListAsync()).Single(item => item.Id == created.Id);
		var archived = await service.ArchiveAsync(created.Id,
			new ArchiveAdmissionIntakeSetupDto { ConcurrencyStamp = created.ConcurrencyStamp });
		archived.IsArchived.ShouldBeTrue();
		(await service.GetListAsync()).Select(item => item.Id).ShouldNotContain(created.Id);
		(await service.GetListAsync(includeArchived: true)).Single(item => item.Id == created.Id)
			.IsArchived.ShouldBeTrue();
		var validation = await WithUnitOfWorkAsync(() =>
			GetRequiredService<AdmissionOfferingConfigurationValidator>().ValidateStoredAsync(offering));
		validation.Errors.ShouldContain(error => error.ActivationOnly &&
			error.Code == AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid);
		var publicLinkService = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var archivedLink = await Should.ThrowAsync<BusinessException>(() => publicLinkService.CreateLinkAsync(
			new CreateAdmissionPublicInquiryLinkDto
			{
					AdmissionCycleId = offering.AdmissionCycleId,
				CounsellorAssignmentProfileId = archived.CounsellorAssignmentProfileId,
				Name = "Archived setup link",
				Source = AdmissionInquirySource.Website
			}));
		archivedLink.Code.ShouldBe(AdmissionIntakeProfileErrorCodes.SetupArchived);
	}

	[Fact]
	public void Intake_Setup_Validator_Should_Require_Fallback_Manager()
	{
		var input = ValidSetupInput("Missing fallback");
		input.CounsellorAssignmentRules.FallbackManagerUserId = Guid.Empty;
		new CreateAdmissionIntakeSetupDtoValidator().Validate(input).Errors
			.ShouldContain(error => error.PropertyName.EndsWith(nameof(AdmissionCounsellorAssignmentRulesDto.FallbackManagerUserId)));
	}

	private async Task EnsureSelectableAdmissionUserAsync()
	{
		await EnsureTestIdentityUserAsync(TestUserId, $"intake-admin-{TestUserId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			if (!await scopeRepository.AnyAsync(item => item.UserId == TestUserId))
				await scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
					userId: TestUserId, description: "Admission intake setup test"), autoSave: true);
		});
	}

	private static CreateAdmissionIntakeSetupDto ValidSetupInput(string name) => new()
	{
		Name = name,
		InquiryRules = new AdmissionInquiryIntakeRulesDto(),
		CounsellorAssignmentRules = new AdmissionCounsellorAssignmentRulesDto
		{
			CounsellorUserIds = [TestUserId],
			FallbackManagerUserId = TestUserId,
			MaxActiveCasesPerCounsellor = 100
		}
	};

	private static UpdateAdmissionIntakeSetupDto ValidUpdateInput(AdmissionIntakeSetupDto setup, string name) => new()
	{
		Name = name,
		InquiryRules = setup.InquiryRules,
		CounsellorAssignmentRules = setup.CounsellorAssignmentRules,
		ConcurrencyStamp = setup.ConcurrencyStamp
	};
}
