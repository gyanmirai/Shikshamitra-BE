using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Retired_ownership_preserves_inquiry_followups_and_server_recorded_staff_names() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiryService.CreateAsync(ManagedInquiryInput(offering));
			inquiry.AssignedCounsellorId.ShouldBeNull();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			var scheduled = await service.ScheduleFollowUpAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(2),
				Purpose = "Staff handles this reminder without a case owner"
			});
			scheduled.FollowUps.ShouldContain(item => item.Purpose.Contains("without a case owner"));
			var timeline = await inquiryService.GetTimelineAsync(inquiry.Id);
			timeline.ShouldContain(item => item.ActivityType == "InquiryCreated" && item.CreatorId == TestUserId && !string.IsNullOrWhiteSpace(item.ActorName));
			timeline.ShouldContain(item => item.ActivityType == "InquiryFollowUpScheduled" && !string.IsNullOrWhiteSpace(item.ActorName));
		});

	[Fact]
	public Task Retired_ownership_rejects_old_assignment_commands_without_creating_records() =>
		RunCertificationScenarioAsync(async () =>
		{
			var setupError = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionIntakeSetupAppService>().CreateAsync(ValidSetupInput("Retired setup")));
			setupError.Code.ShouldBe("Admissions.StaffAssignmentRetired");
			var assignmentError = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionInquiryAppService>().AssignCounsellorAsync(Guid.NewGuid(),
					new() { Mode = AdmissionCounsellorAssignmentMode.Manual, CounsellorId = TestUserId, Reason = "Retired" }));
			assignmentError.Code.ShouldBe("Admissions.StaffAssignmentRetired");
			var ownerError = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IStudentOnboardingAppService>().AssignOwnerAsync(Guid.NewGuid(), new() { UserId = TestUserId, ConcurrencyStamp = "retired" }));
			ownerError.Code.ShouldBe("Admissions.StaffAssignmentRetired");
		});

	[Fact]
	public Task Retired_ownership_public_link_uses_form_without_an_intake_setup() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var link = await service.CreateLinkAsync(new()
			{
				AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
				Name = "Public inquiry without staff assignment", Source = AdmissionInquirySource.Website
			});
			link.CounsellorAssignmentProfileId.ShouldBeNull();
			var context = await service.GetPublicAsync(link.Token, offering.Id);
			context.Form.ShouldNotBeNull();
		});
	[Fact]
	public Task Retired_ownership_keeps_finalization_and_onboarding_handler_history() =>
		RunCertificationScenarioAsync(async () =>
		{
			var (fixture, item) = await PrepareCurrentCompletionCaseAsync("HandlingHistory");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			item.AssignedToUserId.ShouldBeNull();
			item = await ConfirmFixtureFeeStructureAsync(fixture.Offering, item);
			item = await service.ActivateAsync(item.Id, new()
			{
				ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "handling-history-activation"
			});
			item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
			item.AssignedToUserId.ShouldBeNull();
			item.Events.ShouldContain(entry => entry.ActorUserId == TestUserId && !string.IsNullOrWhiteSpace(entry.ActorName));
			var history = await GetRequiredService<IAdmissionActivityAppService>().GetForApplicantAsync(fixture.Application.ApplicantId);
			history.ShouldContain(entry => entry.CreatorId == TestUserId && !string.IsNullOrWhiteSpace(entry.ActorName));
		});

	[Fact]
	public Task Retired_ownership_preserves_legacy_unmatched_read_access_without_expanding_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var handler = Guid.NewGuid();
			var otherStaff = Guid.NewGuid();
			foreach (var user in new[] { handler, otherStaff })
			{
				await EnsureTestIdentityUserAsync(user, $"legacy-reader-{user:N}");
				await _scopeRepository.InsertAsync(new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), TestTenantId,
					userId: user, academicYearId: year, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
					educationMediumStageId: offering.StageId, standardId: offering.StandardId), true);
			}
			var inquiry = await GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(ManagedInquiryInput(offering));
			var repository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiry, Guid>>();
			var legacy = await repository.GetAsync(inquiry.Id);
			typeof(AdmissionInquiry).GetProperty(nameof(AdmissionInquiry.AdmissionOfferingId))!.SetValue(legacy, null);
			legacy.AssignCounsellor(handler);
			await repository.UpdateAsync(legacy, true);
			var access = GetRequiredService<AdmissionInquiryScopeAccessService>();
			using (_principalAccessor.Change(ProfilePrincipal(handler)))
				(await WithUnitOfWorkAsync(() => access.GetAsync(inquiry.Id))).Id.ShouldBe(inquiry.Id);
			using (_principalAccessor.Change(ProfilePrincipal(otherStaff)))
				await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => WithUnitOfWorkAsync(() => access.GetAsync(inquiry.Id)));
		});

	// Compatibility fixture: these rows represent configuration saved before retirement.
	private async Task<AdmissionIntakeSetupDto> CreateHistoricalIntakeSetupAsync(CreateAdmissionIntakeSetupDto input)
	{
		var profiles = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionIntakeProfile, Guid>>();
		var inquiry = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId, AdmissionIntakeProfileKind.Inquiry,
			$"OLD_I_{Guid.NewGuid():N}"[..30], input.Name, System.Text.Json.JsonSerializer.Serialize(input.InquiryRules));
		var assignment = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId, AdmissionIntakeProfileKind.CounsellorAssignment,
			$"OLD_C_{Guid.NewGuid():N}"[..30], input.Name, System.Text.Json.JsonSerializer.Serialize(input.CounsellorAssignmentRules));
		await profiles.InsertAsync(inquiry, true);
		await profiles.InsertAsync(assignment, true);
		var row = await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionIntakeSetup, Guid>>().InsertAsync(
			new AdmissionIntakeSetup(Guid.NewGuid(), TestTenantId, input.Name, inquiry.Id, assignment.Id), true);
		return new AdmissionIntakeSetupDto { Id = row.Id, Name = row.Name, InquiryIntakeProfileId = inquiry.Id,
			CounsellorAssignmentProfileId = assignment.Id, InquiryRules = input.InquiryRules,
			CounsellorAssignmentRules = input.CounsellorAssignmentRules, ConcurrencyStamp = row.ConcurrencyStamp };
	}

}
