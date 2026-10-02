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
	[Fact]
	public async Task Managed_Offer_Should_Require_Audited_Response_And_Support_Exceptions()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Offer runtime integration test"), true);
			var basic = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basic.Id);
			var managed = await CreateManagedOfferWorkflowAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managed.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var application = await StartApprovedOfferApplicationAsync(offering.Id, "Offer Applicant");
			var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
			var stage = runtime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Offer);
			stage.OfferStageEnabled.ShouldBeTrue();
			var service = GetRequiredService<IAdmissionOfferAppService>();
			var initial = await service.GetAsync(application.Id, stage.Id);
			initial.Status.ShouldBe("ready");
			var invalidCounsellor = await Should.ThrowAsync<BusinessException>(() => service.IssueAsync(
				application.Id, stage.Id, new IssueAdmissionOfferDto
				{
					IdempotencyKey = "offer-invalid-counsellor", ConcurrencyStamp = initial.ConcurrencyStamp,
					CounsellorId = Guid.NewGuid()
				}));
			invalidCounsellor.Code.ShouldBe(AdmissionOfferStageErrorCodes.InvalidState);
			await _applicationAppService.AssignCounsellorAsync(application.Id,
				new AssignAdmissionApplicationCounsellorDto
				{
					CounsellorId = TestUserId, ConcurrencyStamp = initial.ConcurrencyStamp
				});
			var applicant360 = await GetRequiredService<IAdmissionApplicantAppService>()
				.GetAsync(application.ApplicantId);
			applicant360.Applications.Single(item => item.Id == application.Id)
				.AssignedCounsellorId.ShouldBe(TestUserId);
			applicant360.Applications.Single(item => item.Id == application.Id)
				.AssignedCounsellorName.ShouldNotBeNullOrWhiteSpace();
			initial = await service.GetAsync(application.Id, stage.Id);
			var implicitHandover = await Should.ThrowAsync<BusinessException>(() => service.IssueAsync(
				application.Id, stage.Id, new IssueAdmissionOfferDto
				{
					IdempotencyKey = "offer-owner-change", ConcurrencyStamp = initial.ConcurrencyStamp,
					CounsellorId = Guid.NewGuid()
				}));
			implicitHandover.Code.ShouldBe(AdmissionOfferStageErrorCodes.InvalidState);
			var issue = new IssueAdmissionOfferDto { Reason = "Approved admission.", IdempotencyKey = "offer-issue-1",
				ConcurrencyStamp = initial.ConcurrencyStamp, CounsellorId = TestUserId };
			var issued = await service.IssueAsync(application.Id, stage.Id, issue);
			issued.Status.ShouldBe("issued");
			issued.AssignedCounsellorId.ShouldBe(TestUserId);
			(await _applicationAppService.GetAsync(application.Id)).AssignedCounsellorId.ShouldBe(TestUserId);
			var assignedApplications = await _applicationAppService.GetListAsync(new AdmissionApplicationListInput
			{
				AssignedCounsellorId = TestUserId
			});
			assignedApplications.Items.ShouldContain(item => item.Id == application.Id);
			(await _applicationAppService.GetAssignableCounsellorsAsync())
				.ShouldContain(item => item.Id == TestUserId);
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AssignCounsellorAsync(
				application.Id, new AssignAdmissionApplicationCounsellorDto
				{
					CounsellorId = TestUserId, ConcurrencyStamp = initial.ConcurrencyStamp
				}))).Code.ShouldBe(AdmissionOfferStageErrorCodes.ConcurrencyConflict);
			issued.VersionNumber.ShouldBe(1);
			(await service.IssueAsync(application.Id, stage.Id, issue)).Events.Count.ShouldBe(1);
			var reusedKey = await Should.ThrowAsync<BusinessException>(() => service.IssueAsync(application.Id, stage.Id,
				new IssueAdmissionOfferDto { Reason = "Changed request.", IdempotencyKey = issue.IdempotencyKey,
					ConcurrencyStamp = issued.ConcurrencyStamp }));
			reusedKey.Code.ShouldBe(AdmissionOfferStageErrorCodes.IdempotencyConflict);
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.AdvanceWorkflowAsync(application.Id, new()
				{
					ExpectedCurrentStageInstanceId = stage.Id, IdempotencyKey = "offer-blocked",
					ConcurrencyStamp = issued.ConcurrencyStamp
				}));
			blocked.Code.ShouldBe(AdmissionOfferStageErrorCodes.ResponseRequired);
			var extended = await service.ExtendAsync(application.Id, stage.Id, new ExtendAdmissionOfferDto
			{
				ExpiresAt = DateTime.UtcNow.AddDays(30), Reason = "Family requested more time.",
				IdempotencyKey = "offer-extend-1", ConcurrencyStamp = issued.ConcurrencyStamp
			});
			extended.ExtensionCount.ShouldBe(1);
			(await Should.ThrowAsync<BusinessException>(() => service.RespondAsync(application.Id, stage.Id,
				new RespondAdmissionOfferDto { Accepted = true, IdempotencyKey = "accept-without-terms",
					ConcurrencyStamp = extended.ConcurrencyStamp }))).Code.ShouldBe(AdmissionOfferStageErrorCodes.ConfirmationRequired);
			var acceptedInput = new RespondAdmissionOfferDto { Accepted = true, ConfirmTermsAccepted = true,
				IdempotencyKey = "offer-accept-1", ConcurrencyStamp = extended.ConcurrencyStamp };
			var accepted = await service.RespondAsync(application.Id, stage.Id, acceptedInput);
			accepted.Status.ShouldBe("accepted");
			accepted.CanComplete.ShouldBeTrue();
			(await service.RespondAsync(application.Id, stage.Id, acceptedInput)).Events.Count.ShouldBe(3);
			runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
			{
				ExpectedCurrentStageInstanceId = stage.Id, IdempotencyKey = "offer-complete",
				ConcurrencyStamp = accepted.ConcurrencyStamp
			});
			runtime.Stages.Single(item => item.Code == "DONE").Status.ShouldBe(AdmissionRuntimeStageStatus.Active);

			var second = await StartApprovedOfferApplicationAsync(offering.Id, "Reissue Applicant");
			var secondRuntime = await _applicationAppService.GetWorkflowAsync(second.Id);
			var secondStage = secondRuntime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Offer);
			var secondState = await service.GetAsync(second.Id, secondStage.Id);
			var secondIssued = await service.IssueAsync(second.Id, secondStage.Id, new()
			{
				IdempotencyKey = "second-issue", ConcurrencyStamp = secondState.ConcurrencyStamp
			});
			var declined = await service.RespondAsync(second.Id, secondStage.Id, new()
			{
				Accepted = false, Reason = "Family needs a corrected start date.", IdempotencyKey = "second-decline",
				ConcurrencyStamp = secondIssued.ConcurrencyStamp
			});
			(await Should.ThrowAsync<BusinessException>(() => service.ReissueAsync(second.Id, secondStage.Id, new()
			{
				CounsellorId = TestUserId, IdempotencyKey = "second-owner-change",
				ConcurrencyStamp = declined.ConcurrencyStamp
			}))).Code.ShouldBe(AdmissionOfferStageErrorCodes.InvalidState);
			var reissued = await service.ReissueAsync(second.Id, secondStage.Id, new()
			{
				Reason = "Corrected offer.", IdempotencyKey = "second-reissue", ConcurrencyStamp = declined.ConcurrencyStamp
			});
			reissued.Status.ShouldBe("issued");
			reissued.VersionNumber.ShouldBe(2);
		}
	}

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
