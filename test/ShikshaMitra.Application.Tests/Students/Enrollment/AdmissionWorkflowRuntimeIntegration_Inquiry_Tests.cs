using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_inquiry_should_require_qualification_before_starting_application()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Managed inquiry test"), autoSave: true);
			var workflow = await CreateAndPublishManagedInquiryWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));

			var state = await management.GetAsync(inquiry.Id);
			state.Managed.ShouldBeTrue();
			state.ContactCount.ShouldBe(0);
			state.CanStartApplication.ShouldBeFalse();
			state.Blockers.ShouldContain(item => item.Contains("qualified", StringComparison.OrdinalIgnoreCase));
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id));
			blocked.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ContactRequired);

			state = await management.RecordContactAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp,
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
	public async Task Basic_inquiry_workflow_should_keep_the_existing_application_start_behavior()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Basic inquiry compatibility"), autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var input = ManagedInquiryInput(offering);
			var inquiry = await inquiries.CreateAsync(input);
			(await GetRequiredService<IAdmissionInquiryManagementAppService>().GetAsync(inquiry.Id)).Managed.ShouldBeFalse();
			input.CreateSeparateFamily = true;
			var separate = await inquiries.CreateAsync(input);
			separate.Id.ShouldNotBe(inquiry.Id);
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationRepository.GetAsync(application.Id)).InquiryId.ShouldBe(inquiry.Id);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateAndPublishManagedInquiryWorkflowAsync()
	{
		var entryId = Guid.NewGuid();
		var terminalId = Guid.NewGuid();
		var configuration = "{\"inquiry\":{\"version\":1,\"enabled\":true,\"applicationStartRequirement\":2," +
			"\"qualificationChecklist\":[{\"code\":\"AGE\",\"name\":\"Age confirmed\",\"required\":true}]}}";
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Managed Inquiry",
			Code = $"INQUIRY_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = entryId, Code = "INQUIRY", Name = "Inquiry", StageType = AdmissionWorkflowStageType.Inquiry,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true, ConfigurationJson = configuration },
				new() { Id = terminalId, Code = "ADMISSION", Name = "Admission", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = entryId, ToStageId = terminalId, Priority = 1, IsDefault = true }]
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}

	private static CreateAdmissionInquiryDto ManagedInquiryInput(AdmissionOffering offering) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId,
		AdmissionOfferingId = offering.Id,
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
