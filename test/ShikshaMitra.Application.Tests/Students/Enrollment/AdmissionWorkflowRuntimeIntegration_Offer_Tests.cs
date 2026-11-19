using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{

	private async Task<AdmissionApplicationDto> StartApprovedOfferApplicationAsync(Guid offeringId, string name)
	{
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync(name)).Id, offeringId);
		var submitted = await _applicationAppService.SubmitAsync(application.Id);
		submitted.ValidationErrors.ShouldBeEmpty();
		var approved = await _applicationAppService.ApproveAsync(application.Id, new AdmissionApplicationDecisionDto
		{
			Reason = "Admission criteria satisfied.", ConcurrencyStamp = submitted.Application.ConcurrencyStamp
		});
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		await _applicationAppService.AdvanceWorkflowAsync(application.Id, new AdvanceAdmissionWorkflowDto
		{
			ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
			ConcurrencyStamp = approved.ConcurrencyStamp,
			IdempotencyKey = $"enter-offer-{application.Id:N}"
		});
		return await _applicationAppService.GetAsync(application.Id);
	}

	private async Task<AdmissionWorkflowDto> CreateManagedOfferWorkflowAsync()
	{
		var startId = Guid.NewGuid(); var offerId = Guid.NewGuid(); var doneId = Guid.NewGuid();
		var policy = new AdmissionOfferStagePolicy { Version = 1, Enabled = true, OfferType = "unconditional",
			Title = "Admission offer", Message = "We are pleased to offer admission.", ValidityDays = 14,
			MinimumValidityDays = 1, MaximumValidityDays = 90, AllowExpiryOverride = true,
			RequireTermsAcceptance = true, RequireDeclineReason = true, AllowExtension = true,
			MaximumExtensions = 2, AllowReissue = true, MaximumReissues = 1,
			AllowedCompletionOutcomes = ["accepted", "declined"] };
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Managed offer", Code = $"OFFER_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = startId, Code = "START", Name = "Offer preparation", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true },
				new() { Id = offerId, Code = "OFFER", Name = "Admission offer", StageType = AdmissionWorkflowStageType.Offer,
					DisplayOrder = 1, IsRequired = true, ConfigurationJson = JsonSerializer.Serialize(new { offer = policy }) },
				new() { Id = doneId, Code = "DONE", Name = "Offer complete", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 2, IsTerminalStage = true, IsRequired = true }
			],
			Transitions =
			[
				new() { FromStageId = startId, ToStageId = offerId, Priority = 1, IsDefault = true },
				new() { FromStageId = offerId, ToStageId = doneId, Priority = 1, IsDefault = true }
			]
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}
}
