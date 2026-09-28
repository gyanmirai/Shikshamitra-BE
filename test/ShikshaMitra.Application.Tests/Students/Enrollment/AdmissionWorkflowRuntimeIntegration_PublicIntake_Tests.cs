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
	public Task Public_link_uses_published_inquiry_form_and_persists_answers_without_an_offering() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
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
			await WithUnitOfWorkAsync(() => profiles.InsertAsync(profile, autoSave: true));
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
				var context = await service.GetPublicAsync(existingLink.Token);
				context.Form.ShouldNotBeNull();
				context.Form.Id.ShouldBe(offering.InquiryFormDefinitionId!.Value);
				context.Form.Sections.SelectMany(section => section.Fields)
					.ShouldContain(field => field.Code == "LEARNING_NOTES");
				var missing = await Should.ThrowAsync<BusinessException>(() =>
					service.SubmitPublicAsync(existingLink.Token, new SubmitPublicAdmissionInquiryDto
					{
						GuardianFirstName = "Meera", ConsentToContact = true,
						FormValues = [new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav\"" }]
					}));
				missing.Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
				await service.SubmitPublicAsync(existingLink.Token, new SubmitPublicAdmissionInquiryDto
				{
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
			saved.AdmissionOfferingId.ShouldBeNull();
			saved.AssignedCounsellorId.ShouldBe(TestUserId);
			saved.FormResponseDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			saved.FormResponseSnapshotJson.ShouldContain("Enjoys reading");
			var detail = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(saved.Id);
			detail.FormResponseDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			detail.FormAnswers.ShouldContain(answer => answer.FieldCode == "LEARNING_NOTES" &&
				answer.Value == "Enjoys reading");
		});

	[Fact]
	public Task Public_link_distributes_unknown_offering_inquiry_and_hides_duplicate_phone() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
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
			await WithUnitOfWorkAsync(() => profiles.InsertAsync(profile, autoSave: true));
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
				ApplicantFirstName = "Aarav",
				GuardianFirstName = "Meera",
				GuardianPhone = "9876543210",
				ClassInterest = "Grade 5",
				ConsentToContact = true
			};
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				(await service.GetPublicAsync(link.Token)).AdmissionCycleName.ShouldNotBeNullOrWhiteSpace();
				await service.SubmitPublicAsync(link.Token, input);
				await service.SubmitPublicAsync(link.Token, input);
			}
			var matches = await WithUnitOfWorkAsync(() => inquiries.GetListAsync(item =>
				item.AdmissionCycleId == offering.AdmissionCycleId && item.NormalizedPhone == input.GuardianPhone));
			matches.Count.ShouldBe(1);
			matches.Single().AdmissionOfferingId.ShouldBeNull();
			matches.Single().AssignedCounsellorId.ShouldBe(TestUserId);
			matches.Single().Source.ShouldBe(AdmissionInquirySource.SchoolFair);
			matches.Single().SourceDetail.ShouldContain("Grade 5");
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			var confirmed = await inquiryService.ConfirmOfferingAsync(matches.Single().Id,
				new ConfirmAdmissionInquiryOfferingDto
				{
					OfferingId = offering.Id,
					ConcurrencyStamp = matches.Single().ConcurrencyStamp
				});
			confirmed.AdmissionOfferingId.ShouldBe(offering.Id);
			confirmed.AssignedCounsellorId.ShouldBe(TestUserId);
			await service.RevokeLinkAsync(link.Id, link.ConcurrencyStamp);
			var error = await Should.ThrowAsync<BusinessException>(() => service.GetPublicAsync(link.Token));
			error.Code.ShouldBe("Admissions.PublicInquiry.LinkUnavailable");
		});
}
