using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inactive_offering_can_save_an_unscoped_assignment_but_cannot_activate() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var profileRepository = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			var profile = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
				AdmissionIntakeProfileKind.CounsellorAssignment,
				$"UNSCOPED_{Guid.NewGuid():N}"[..30], "Unscoped assignment",
				JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
				{
					CounsellorUserIds = [Guid.NewGuid()],
					FallbackManagerUserId = Guid.NewGuid()
				}));
			await WithUnitOfWorkAsync(async () =>
			{
				await profileRepository.InsertAsync(profile, autoSave: true);
				var storedOffering = await _offeringRepository.GetAsync(offering.Id);
				storedOffering.Deactivate();
				await _offeringRepository.UpdateAsync(storedOffering, autoSave: true);
			});
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var current = await service.GetAsync(offering.Id);
			var updated = await service.UpdateAsync(offering.Id, new UpdateAdmissionOfferingDto
			{
				Name = current.Name,
				Track = current.Track,
				EntryMode = current.EntryMode,
				CampusId = current.CampusId,
				BoardId = current.BoardId,
				MediumId = current.MediumId,
				StageId = current.StageId,
				StandardId = current.StandardId,
				Code = current.Code,
				UseCycleDates = current.UseCycleDates,
				WorkflowDefinitionId = current.WorkflowDefinitionId,
				InquiryFormDefinitionId = current.InquiryFormDefinitionId,
				ApplicationFormDefinitionId = current.ApplicationFormDefinitionId,
				CounsellorAssignmentProfileId = profile.Id,
				IsActive = false,
				ConcurrencyStamp = current.ConcurrencyStamp
			});
			updated.CounsellorAssignmentProfileId.ShouldBe(profile.Id);
			updated.IsActive.ShouldBeFalse();
			updated.Configuration.Errors.Count(error => error.Code ==
				AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid).ShouldBe(2);
			var validation = await WithUnitOfWorkAsync(async () =>
				await _offeringValidator.ValidateStoredAsync(await _offeringRepository.GetAsync(offering.Id)));
			validation.Errors.Count(error => error.Code ==
				AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid).ShouldBe(2);
			var activation = await Should.ThrowAsync<BusinessException>(() =>
				service.ActivateAsync(offering.Id, updated.ConcurrencyStamp));
			activation.Code.ShouldBe(AdmissionErrorCodes.OfferingConfigurationIncomplete);
		});

	[Fact]
	public Task Public_link_checks_selected_offering_before_showing_published_form_and_persists_answers() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var profile = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
				AdmissionIntakeProfileKind.CounsellorAssignment,
				$"FORM_LINK_{Guid.NewGuid():N}"[..30], "Published form counsellor",
				JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
				{
					CounsellorUserIds = [TestUserId], MaxActiveCasesPerCounsellor = 100
				}));
			await ConfigurePublicOfferingFixtureAsync(offering, profile);
			var link = await service.CreateLinkAsync(new CreateAdmissionPublicInquiryLinkDto
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				CounsellorAssignmentProfileId = profile.Id,
				InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
				Name = "Shared form QR", Source = AdmissionInquirySource.QrCode,
				SourceDetail = "Front desk"
			});
			link.InquiryFormDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			var existingLink = await service.CreateLinkAsync(new CreateAdmissionPublicInquiryLinkDto
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				CounsellorAssignmentProfileId = profile.Id,
				Name = "Existing QR", Source = AdmissionInquirySource.QrCode,
				SourceDetail = "Front desk"
			});
			var updatedLink = await service.SetLinkFormAsync(existingLink.Id,
				new SetAdmissionPublicInquiryLinkFormDto
				{
					InquiryFormDefinitionId = offering.InquiryFormDefinitionId!.Value,
					ConcurrencyStamp = existingLink.ConcurrencyStamp
				});
			updatedLink.InquiryFormDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				var catalogue = await service.GetPublicAsync(existingLink.Token);
				catalogue.Form.ShouldBeNull();
				catalogue.Offerings.ShouldContain(item => item.Id == offering.Id && item.CanCreateInquiry);
				var context = await service.GetPublicAsync(existingLink.Token, offering.Id);
				context.Form.ShouldNotBeNull();
				context.Form.Id.ShouldBe(offering.InquiryFormDefinitionId!.Value);
				context.Form.Sections.SelectMany(section => section.Fields)
					.ShouldContain(field => field.Code == "LEARNING_NOTES");
				var missing = await Should.ThrowAsync<BusinessException>(() =>
					service.SubmitPublicAsync(existingLink.Token, new SubmitPublicAdmissionInquiryDto
					{
						AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
						MediumId = offering.MediumId, StandardId = offering.StandardId,
						GuardianFirstName = "Meera", ConsentToContact = true,
						FormValues = [new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav\"" }]
					}));
				missing.Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
				await service.SubmitPublicAsync(existingLink.Token, new SubmitPublicAdmissionInquiryDto
				{
					AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
					MediumId = offering.MediumId, StandardId = offering.StandardId,
					GuardianFirstName = "Meera", ConsentToContact = true,
					FormValues =
					[
						new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav\"" },
						new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2018-04-15T00:00:00Z\"" },
						new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Male\"" },
						new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876543211\"" },
						new() { FieldCode = "LEARNING_NOTES", ValueJson = "\"Enjoys reading\"" }
					]
				});
			}
			var saved = (await inquiries.GetListAsync(item =>
				item.AdmissionCycleId == offering.AdmissionCycleId && item.NormalizedPhone == "9876543211"))
				.Single();
			saved.AdmissionOfferingId.ShouldBe(offering.Id);
			saved.AssignedCounsellorId.ShouldBe(TestUserId);
			saved.FormResponseDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			saved.FormResponseSnapshotJson.ShouldContain("Enjoys reading");
			var detail = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(saved.Id);
			detail.FormResponseDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			detail.FormAnswers.ShouldContain(answer => answer.FieldCode == "LEARNING_NOTES" &&
				answer.Value == "Enjoys reading");
		});

	[Fact]
	public Task Public_link_requires_matching_offering_and_hides_duplicate_phone() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var profile = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
				AdmissionIntakeProfileKind.CounsellorAssignment,
				$"PUBLIC_{Guid.NewGuid():N}"[..30], "School fair counsellors",
				JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
				{
					CounsellorUserIds = [TestUserId],
					MaxActiveCasesPerCounsellor = 100
				}));
			await ConfigurePublicOfferingFixtureAsync(offering, profile);
			var link = await service.CreateLinkAsync(new CreateAdmissionPublicInquiryLinkDto
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				CounsellorAssignmentProfileId = profile.Id,
				Name = "Autumn fair",
				Source = AdmissionInquirySource.SchoolFair,
				SourceDetail = "Main hall"
			});
			link.Token.Length.ShouldBe(64);
			var profileService = GetRequiredService<IAdmissionIntakeProfileAppService>();
			var invalidUpdate = await Should.ThrowAsync<BusinessException>(() =>
				profileService.UpdateAsync(profile.Id, new SaveAdmissionIntakeProfileDto
				{
					Kind = AdmissionIntakeProfileKind.CounsellorAssignment,
					Code = profile.Code,
					Name = profile.Name,
					ConcurrencyStamp = profile.ConcurrencyStamp,
					ConfigurationJson = JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
					{
						CounsellorUserIds = [Guid.NewGuid()]
					})
				}));
			invalidUpdate.Code.ShouldBe(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
			var input = new SubmitPublicAdmissionInquiryDto
			{
				AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
				MediumId = offering.MediumId, StandardId = offering.StandardId,
				ApplicantFirstName = "Aarav",
				GuardianFirstName = "Meera",
				GuardianPhone = "9876543210",
				ClassInterest = "Grade 5",
				ConsentToContact = true,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2018-04-15T00:00:00Z\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Male\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876543210\"" }
				]
			};
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				(await service.GetPublicAsync(link.Token)).AdmissionCycleName.ShouldNotBeNullOrWhiteSpace();
				var wrongClass = new SubmitPublicAdmissionInquiryDto
				{
					AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
					MediumId = offering.MediumId, StandardId = Guid.NewGuid(), ConsentToContact = true
				};
				(await Should.ThrowAsync<BusinessException>(() => service.SubmitPublicAsync(link.Token, wrongClass)))
					.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
				await service.SubmitPublicAsync(link.Token, input);
				await service.SubmitPublicAsync(link.Token, input);
			}
			var matches = await WithUnitOfWorkAsync(() => inquiries.GetListAsync(item =>
				item.AdmissionCycleId == offering.AdmissionCycleId && item.NormalizedPhone == input.GuardianPhone));
			matches.Count.ShouldBe(1);
			matches.Single().AdmissionOfferingId.ShouldBe(offering.Id);
			matches.Single().AssignedCounsellorId.ShouldBe(TestUserId);
			matches.Single().Source.ShouldBe(AdmissionInquirySource.SchoolFair);
			matches.Single().SourceDetail.ShouldContain("Grade 5");
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			var confirmed = await inquiryService.GetAsync(matches.Single().Id);
			confirmed.AdmissionOfferingId.ShouldBe(offering.Id);
			confirmed.AssignedCounsellorId.ShouldBe(TestUserId);
			await service.RevokeLinkAsync(link.Id, link.ConcurrencyStamp);
			var error = await Should.ThrowAsync<BusinessException>(() => service.GetPublicAsync(link.Token));
			error.Code.ShouldBe("Admissions.PublicInquiry.LinkUnavailable");
		});

	private Task ConfigurePublicOfferingFixtureAsync(AdmissionOffering offering, AdmissionIntakeProfile assignmentProfile) =>
		WithUnitOfWorkAsync(async () =>
		{
			var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
			await profiles.InsertAsync(assignmentProfile, true);
			var inquiryProfile = await profiles.InsertAsync(new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
				AdmissionIntakeProfileKind.Inquiry, $"PUBLIC_INQUIRY_{Guid.NewGuid():N}"[..30], "Public inquiry fixture",
				JsonSerializer.Serialize(new AdmissionInquiryIntakeRules())), true);
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureIntake(inquiryProfile.Id, assignmentProfile.Id);
			await _offeringRepository.UpdateAsync(stored, true);
		});
}
