using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Legacy_inquiry_policy_should_require_interest_before_application_start()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Managed inquiry test"), autoSave: true);
			var offering = await CreateOptionalIntakeOfferingAsync(legacyPolicy: true);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));

			var state = await management.GetAsync(inquiry.Id);
			state.Managed.ShouldBeTrue();
			state.ContactCount.ShouldBe(0);
			state.CanStartApplication.ShouldBeFalse();
			state.Blockers.ShouldNotBeEmpty();
			state.Policy.ShouldNotBeNull().ApplicationStartRequirement.ShouldBe(AdmissionInquiryApplicationStartRequirement.Captured);
			var immediate = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(immediate.ApplicantId!.Value, offering.Id, immediate.Id)))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.QualificationRequired);
			immediate = await inquiries.UpdateAsync(immediate.Id, InquiryStatusUpdate(immediate, AdmissionInquiryStatus.Interested));
			var immediateApplication = await _applicationAppService.StartAsync(immediate.ApplicantId!.Value, offering.Id, immediate.Id);
			(await _applicationRepository.GetAsync(immediateApplication.Id)).InquiryId.ShouldBe(immediate.Id);
			var immediateState = await management.GetAsync(immediate.Id);
			immediateState.ContactCount.ShouldBe(0);
			immediateState.Outcome.ShouldBe(AdmissionInquiryQualificationOutcome.Qualified);
			immediateState.IsLocked.ShouldBeTrue();

			state = await management.RecordContactAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp,
				Channel = "PHONE",
				Direction = "OUTBOUND",
				Outcome = "CONNECTED",
				Note = "Guardian confirmed interest in a campus visit."
			});
			state.ContactCount.ShouldBe(1);
			state = await management.QualifyAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp,
				Outcome = AdmissionInquiryQualificationOutcome.Qualified,
				Reason = "Age and family interest confirmed.",
				Answers = [new() { Code = "AGE", Passed = true }]
			});
			state.CanStartApplication.ShouldBeTrue();

			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationRepository.GetAsync(application.Id)).InquiryId.ShouldBe(inquiry.Id);
			var completedState = await management.GetAsync(inquiry.Id);
			completedState.Outcome.ShouldBe(AdmissionInquiryQualificationOutcome.Qualified);
			completedState.IsLocked.ShouldBeTrue();
			completedState.CanStartApplication.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task Basic_inquiry_workflow_should_require_interest_and_preserve_duplicate_handling()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Basic inquiry compatibility"), autoSave: true);
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var input = ManagedInquiryInput(offering);
			var inquiry = await inquiries.CreateAsync(input);
			(await GetRequiredService<IAdmissionInquiryManagementAppService>().GetAsync(inquiry.Id)).Managed.ShouldBeTrue();
			input.CreateSeparateFamily = true;
			var reasonRequired = await Should.ThrowAsync<BusinessException>(() => inquiries.CreateAsync(input));
			reasonRequired.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.DuplicateReasonRequired);
			input.DuplicateOverrideReason = "Verified separate family sharing the mobile number.";
			var separate = await inquiries.CreateAsync(input);
			separate.Id.ShouldNotBe(inquiry.Id);
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationRepository.GetAsync(application.Id)).InquiryId.ShouldBe(inquiry.Id);
		}
	}

	[Fact]
	public async Task Inquiry_list_should_ignore_retired_ownership_filters_and_preserve_followup_queues()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inquiry owner queue"), autoSave: true);
			var counsellorId = await CreateReviewUserAsync();
			var offering = await CreateOptionalIntakeOfferingAsync(legacyPolicy: true);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiryInput = ManagedInquiryInput(offering);
			inquiryInput.Email = "family@example.com";
			var inquiry = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>().CreateFromPublicLinkAsync(
				inquiryInput, null, offering.InquiryFormDefinitionId));
			(await inquiries.GetAssignableCounsellorsAsync(offering.Id)).ShouldBeEmpty();

			var unassigned = await inquiries.GetListAsync(new() { IsAssigned = false, MaxResultCount = 50 });
			unassigned.Items.ShouldContain(item => item.Id == inquiry.Id);
			var firstContactQueue = await inquiries.GetListAsync(new()
			{
				WorkQueue = AdmissionInquiryWorkQueue.NeedsFirstContact,
				MaxResultCount = 50
			});
			var queueItem = firstContactQueue.Items.Single(item => item.Id == inquiry.Id);
			queueItem.Managed.ShouldBeTrue();
			queueItem.NeedsFirstContact.ShouldBeTrue();
			queueItem.NeedsQualification.ShouldBeTrue();
			queueItem.AdmissionCycleName.ShouldNotBeNullOrWhiteSpace();
			queueItem.AdmissionOfferingCode.ShouldBe(offering.Code);

			(await Should.ThrowAsync<BusinessException>(() => inquiries.AssignCounsellorAsync(inquiry.Id, new()
			{
				Mode = AdmissionCounsellorAssignmentMode.Manual,
				CounsellorId = counsellorId,
				Reason = "Transfer the case to the covering counsellor."
			}))).Code.ShouldBe("Admissions.StaffAssignmentRetired");
			// Preserve a historical owner fixture to verify queue and follow-up projections independently.
			var inquiryRepository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiry, Guid>>();
			var ownedInquiry = await inquiryRepository.GetAsync(inquiry.Id);
			ownedInquiry.AssignCounsellor(counsellorId);
			await inquiryRepository.UpdateAsync(ownedInquiry, true);

			var assigned = await inquiries.GetListAsync(new()
			{
				IsAssigned = true,
				AssignedCounsellorId = counsellorId,
				MaxResultCount = 50
			});
			assigned.Items.ShouldContain(item => item.Id == inquiry.Id);
			(await inquiries.GetListAsync(new() { IsAssigned = false, MaxResultCount = 50 }))
				.Items.ShouldContain(item => item.Id == inquiry.Id);

			var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await management.GetAsync(inquiry.Id);
			await management.ScheduleFollowUpAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp,
				ScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
				Purpose = "Confirm the campus visit",
				AssignedCounsellorId = counsellorId
			});
			var followUpQueue = await inquiries.GetListAsync(new()
			{
				WorkQueue = AdmissionInquiryWorkQueue.OpenFollowUps,
				MaxResultCount = 50
			});
			var followUpItem = followUpQueue.Items.Single(item => item.Id == inquiry.Id);
			followUpItem.OpenFollowUpCount.ShouldBe(1);
			followUpItem.NextFollowUpAt.ShouldNotBeNull();
			followUpItem.AssignedCounsellorName.ShouldBeNull();
		}
	}

	[Fact]
	public async Task Inquiry_communication_should_create_a_traceable_draft_for_the_family_recipient()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inquiry communication"),
				autoSave: true);
			var workflow = await CreateAndPublishManagedInquiryWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));

			var result = await inquiries.CreateCommunicationAsync(inquiry.Id, new()
			{
				Title = "Campus visit follow-up",
				Body = "Please reply if you would like us to reserve a campus visit slot.",
				Channels = [CommunicationChannelType.Sms],
				ImportanceLevel = CommunicationImportanceLevel.Normal,
				PublishImmediately = false
			});

			result.CampaignId.ShouldNotBe(Guid.Empty);
			result.Status.ShouldBeOneOf(CommunicationCampaignStatus.Draft,
				CommunicationCampaignStatus.PendingApproval);
			result.DeliveryStarted.ShouldBeFalse();
			result.RecipientCount.ShouldBe(1);
			result.DeliveryErrorCode.ShouldBeNull();
			(await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionActivity, Guid>>()
				.GetListAsync(activity => activity.InquiryId == inquiry.Id))
				.ShouldContain(activity => activity.ActivityType == "InquiryCommunicationDraftCreated" &&
					activity.MetadataJson!.Contains(result.CampaignId.ToString(), StringComparison.OrdinalIgnoreCase));

			var invalidEmail = await Should.ThrowAsync<BusinessException>(() =>
				inquiries.CreateCommunicationAsync(inquiry.Id, new()
				{
					Title = "Email follow-up",
					Body = "Email body",
					Channels = [CommunicationChannelType.Email]
				}));
			invalidEmail.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateAndPublishManagedInquiryWorkflowAsync(bool withAdmissionDecision = false)
	{
		var entryId = Guid.NewGuid();
		var terminalId = Guid.NewGuid();
		var configuration = "{\"inquiry\":{\"version\":1,\"enabled\":true,\"applicationStartRequirement\":2," +
			"\"qualificationChecklist\":[{\"code\":\"AGE\",\"name\":\"Age confirmed\",\"required\":true}]}}";
		var workflow = await SeedPublishedHistoricalWorkflowAsync(new()
		{
			Name = "Managed Inquiry",
			Code = $"INQUIRY_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = entryId, Code = "INQUIRY", Name = "Inquiry", StageType = AdmissionWorkflowStageType.Inquiry,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true, ConfigurationJson = configuration },
				new() { Id = terminalId, Code = "ADMISSION", Name = "Admission", StageType = withAdmissionDecision ? AdmissionWorkflowStageType.Approval : AdmissionWorkflowStageType.Custom,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = entryId, ToStageId = terminalId, Priority = 1, IsDefault = true }]
		});
		return workflow;
	}

	private static CreateAdmissionInquiryDto ManagedInquiryInput(AdmissionOffering offering) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId,
		AdmissionOfferingId = offering.Id,
		BoardId = offering.BoardId,
		MediumId = offering.MediumId,
		StandardId = offering.StandardId,
		ApplicantName = "Aanya Patel",
		Phone = $"9{Random.Shared.Next(100000000, 999999999)}",
		Source = AdmissionInquirySource.Website,
		GuardianFirstName = "Meera",
		GuardianPhone = $"8{Random.Shared.Next(100000000, 999999999)}",
		ApplicantFirstName = "Aanya",
		FormValues =
		[
			new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aanya\"" },
			new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2016-04-12T00:00:00+05:30\"" },
			new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" }
		]
	};
}
