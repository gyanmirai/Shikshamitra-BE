using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
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
	[Fact]
	public Task Unlimited_intake_does_not_allow_placement_in_a_full_division() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var applicant = await CreateApplicantAsync("PlacementCandidate");
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
			var candidate = await CreateExistingStudentAsync(offering, "Placement", "Candidate");
			var enrolled = await CreateExistingStudentAsync(offering, "Already", "Placed");
			var placement = await WithUnitOfWorkAsync(async () =>
			{
				var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
				var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					await GetRequiredService<DivisionManager>().CreateAsync(offering.StandardId, "Full division", 1), true);
				await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
					enrolled.Id, year, offering.StandardId, division.Id), true);
				var item = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id,
					candidate.Id, offering.Id, year, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
					"full-division-placement", null, DateTime.UtcNow.AddDays(7));
				await _onboardingCaseRepository.InsertAsync(item, true);
				return (Case: item, DivisionId: division.Id);
			});
			var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentOnboardingAppService>()
				.SetPlacementAsync(placement.Case.Id, new() { DivisionId = placement.DivisionId, ConcurrencyStamp = placement.Case.ConcurrencyStamp }));
			error.Code.ShouldBe(StudentOnboardingErrorCodes.DivisionFull);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(placement.Case.Id))).DivisionId.ShouldBeNull();
		});

	[Fact]
	public Task Full_legacy_room_and_seat_pool_allow_multiple_direct_registrations() =>
		RunCertificationScenarioAsync(async () =>
		{
			var draft = await CreateDirectWorkflowDraftAsync();
			var offering = await CreateAvailableOfferingAsync(draft.Id, assertValid: false);
			var workflowInput = ToUpdateInput(draft);
			workflowInput.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form).ConfigurationJson =
				JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId,
					form = new { version = 1, enabled = true, responsibility = "staff" } });
			var updated = await _workflowAppService.UpdateAsync(draft.Id, workflowInput);
			var workflow = await _workflowAppService.PublishAsync(updated.Id, updated.ConcurrencyStamp);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration, identityLocked: false);
				stored.ConfigureReferences(workflow.Id, null, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			offering = await WithUnitOfWorkAsync(() => _offeringRepository.GetAsync(offering.Id));
			await SeedFullLegacyIntakeCapacityAsync(offering);
			for (var index = 0; index < 3; index++)
			{
				var input = new StartAdmissionDirectRegistrationDto
				{
					RequestId = Guid.NewGuid(), AdmissionOfferingId = offering.Id,
					ApplicantFirstName = $"DirectIntake{index}", ApplicantLastName = "Patel",
					ApplicantDateOfBirth = new DateTime(2018, 5, 15), ApplicantGender = "Female",
					GuardianFirstName = "Ravi", GuardianLastName = "Patel",
					GuardianMobile = $"987654320{index}", GuardianRelationship = AdmissionGuardianRelationship.Parent
				};
				var registration = await _applicationAppService.StartDirectRegistrationAsync(input);
				registration.Id.ShouldNotBe(Guid.Empty);
				(await _applicationAppService.StartDirectRegistrationAsync(input)).Id.ShouldBe(registration.Id);
			}
		});

	[Fact]
	public Task Full_legacy_room_and_seat_pool_allow_multiple_staff_inquiries_and_registration_handoff() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			await SeedFullLegacyIntakeCapacityAsync(offering);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			for (var index = 0; index < 3; index++)
			{
				var input = ManagedInquiryInput(offering);
				input.ApplicantFirstName = $"Intake{index}";
				input.ApplicantName = $"Intake{index} Patel";
				input.FormValues.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME").ValueJson = $"\"Intake{index}\"";
				var inquiry = await inquiries.CreateAsync(input);
				inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
				var registration = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
				registration.Id.ShouldNotBe(Guid.Empty);
				(await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, offering.Id, inquiry.Id)).Id.ShouldBe(registration.Id);
			}
		});

	[Fact]
	public Task Full_legacy_room_and_seat_pool_allow_public_inquiry_and_idempotent_retries() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			await SeedFullLegacyIntakeCapacityAsync(offering);
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var link = await service.CreateLinkAsync(new()
			{
				AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
				Name = "Unlimited intake", Source = AdmissionInquirySource.Website
			});
			var input = new SubmitPublicAdmissionInquiryDto
			{
				AdmissionOfferingId = offering.Id, BoardId = offering.BoardId,
				MediumId = offering.MediumId, StandardId = offering.StandardId,
				ApplicantFirstName = "PublicIntake", GuardianFirstName = "Meera", GuardianPhone = "9876543210",
				ConsentToContact = true,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"PublicIntake\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2018-04-15T00:00:00Z\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Male\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876543210\"" }
				]
			};
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				(await service.GetPublicAsync(link.Token, offering.Id)).Offerings.Single().CanCreateInquiry.ShouldBeTrue();
				await service.SubmitPublicAsync(link.Token, input);
				await service.SubmitPublicAsync(link.Token, input);
			}
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item =>
				item.AdmissionOfferingId == offering.Id && item.NormalizedPhone == input.GuardianPhone))).Count.ShouldBe(1);
		});

	private async Task SeedFullLegacyIntakeCapacityAsync(AdmissionOffering offering)
	{
		var room = await CreateLegacyAdmissionRoomAsync(1);
		var student = await CreateExistingStudentAsync(offering, "Existing", "Enrollment");
		await WithUnitOfWorkAsync(async () =>
		{
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			await GetRequiredService<IRepository<AdmissionClassRoomAllocation, Guid>>().InsertAsync(
				new AdmissionClassRoomAllocation(Guid.NewGuid(), TestTenantId, year, offering.StandardId, room.Id), true);
			await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
				student.Id, year, offering.StandardId, null), true);
			var pool = new AdmissionSeatPool(Guid.NewGuid(), TestTenantId, offering.Id, 1);
			pool.Reserve(false);
			await GetRequiredService<IRepository<AdmissionSeatPool, Guid>>().InsertAsync(pool, true);
		});
	}
}
