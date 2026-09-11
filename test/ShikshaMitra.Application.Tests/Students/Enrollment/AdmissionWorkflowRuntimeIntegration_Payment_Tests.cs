using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_Payment_Should_Hold_Workflow_Until_Independently_Verified()
	{
		var verifierId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildPaymentPrincipal(TestUserId)))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Payment collector"), true);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: verifierId, description: "Payment verifier"), true);
			var basicWorkflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basicWorkflow.Id);
			var managedWorkflow = await CreateManagedPaymentWorkflowAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managedWorkflow.Id, stored.InquiryFormDefinitionId,
					stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var application = await _applicationAppService.StartAsync(
				(await CreateApplicantAsync("Payment Applicant")).Id, offering.Id);
			var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
			runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
			{
				ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
				IdempotencyKey = "enter-payment", ConcurrencyStamp = application.ConcurrencyStamp
			});
			var stage = runtime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Payment);
			stage.PaymentStageEnabled.ShouldBeTrue();
			var service = GetRequiredService<IAdmissionPaymentAppService>();
			var initial = await service.GetAsync(application.Id, stage.Id);
			var input = new RecordAdmissionPaymentDto
			{
				Amount = 500m, Method = "upi", ExternalReference = "UPI-ADMISSION-1",
				IdempotencyKey = "payment-1", ConcurrencyStamp = initial.ConcurrencyStamp
			};
			var pending = await service.RecordAsync(application.Id, stage.Id, input);
			pending.Status.ShouldBe("pendingVerification");
			pending.CanComplete.ShouldBeFalse();
			(await service.RecordAsync(application.Id, stage.Id, input)).Transactions.Count.ShouldBe(1);
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.AdvanceWorkflowAsync(application.Id, new()
				{
					ExpectedCurrentStageInstanceId = stage.Id, IdempotencyKey = "blocked-payment",
					ConcurrencyStamp = pending.ConcurrencyStamp
				}));
			blocked.Code.ShouldBe(AdmissionPaymentStageErrorCodes.VerificationRequired);
			AdmissionPaymentStageDto verified;
			using (_principalAccessor.Change(BuildPaymentPrincipal(verifierId)))
			{
				var transaction = pending.Transactions.Single();
				var review = new ReviewAdmissionPaymentDto
				{
					Approved = true, Reason = "Matched settlement report.", IdempotencyKey = "verify-payment-1",
					ConcurrencyStamp = pending.ConcurrencyStamp,
					TransactionConcurrencyStamp = transaction.ConcurrencyStamp
				};
				verified = await service.ReviewAsync(transaction.Id, review);
				verified.Status.ShouldBe("settled");
				verified.CanComplete.ShouldBeTrue();
				(await service.ReviewAsync(transaction.Id, review)).Transactions.Count.ShouldBe(1);
			}
			runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
			{
				ExpectedCurrentStageInstanceId = stage.Id, IdempotencyKey = "complete-payment",
				ConcurrencyStamp = verified.ConcurrencyStamp
			});
			runtime.Stages.Single(item => item.Code == "DONE").Status
				.ShouldBe(AdmissionRuntimeStageStatus.Active);

			var waivedApplication = await _applicationAppService.StartAsync(
				(await CreateApplicantAsync("Waived Applicant")).Id, offering.Id);
			var waivedRuntime = await _applicationAppService.GetWorkflowAsync(waivedApplication.Id);
			waivedRuntime = await _applicationAppService.AdvanceWorkflowAsync(waivedApplication.Id, new()
			{
				ExpectedCurrentStageInstanceId = waivedRuntime.CurrentStageInstanceId!.Value,
				IdempotencyKey = "enter-waiver", ConcurrencyStamp = waivedApplication.ConcurrencyStamp
			});
			var waivedStage = waivedRuntime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Payment);
			var waiverState = await service.GetAsync(waivedApplication.Id, waivedStage.Id);
			var waiver = new AdmissionPaymentReasonDto
			{
				Reason = "Principal-approved hardship support.", IdempotencyKey = "waiver-1",
				ConcurrencyStamp = waiverState.ConcurrencyStamp
			};
			var waived = await service.WaiveAsync(waivedApplication.Id, waivedStage.Id, waiver);
			waived.Status.ShouldBe("waived");
			waived.CanComplete.ShouldBeTrue();
			(await service.WaiveAsync(waivedApplication.Id, waivedStage.Id, waiver))
				.Transactions.Count.ShouldBe(1);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateManagedPaymentWorkflowAsync()
	{
		var startId = Guid.NewGuid();
		var paymentId = Guid.NewGuid();
		var doneId = Guid.NewGuid();
		var policy = new AdmissionPaymentStagePolicy
		{
			Version = 1, Enabled = true, PurposeCode = "APPLICATION_FEE",
			PurposeName = "Application fee", Currency = "INR", Amount = 500m,
			CollectionMode = "staff", Payer = "either", AcceptedMethods = ["cash", "upi"],
			AllowLatePayment = true, RequireIndependentVerification = true, AllowWaiver = true
		};
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Managed payment", Code = $"PAY_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = startId, Code = "START", Name = "Ready for payment",
					StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 0,
					IsEntryStage = true, IsRequired = true },
				new() { Id = paymentId, Code = "PAYMENT", Name = "Application payment",
					StageType = AdmissionWorkflowStageType.Payment, DisplayOrder = 1, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { payment = policy }) },
				new() { Id = doneId, Code = "DONE", Name = "Payment complete",
					StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 2,
					IsTerminalStage = true, IsRequired = true }
			],
			Transitions =
			[
				new() { FromStageId = startId, ToStageId = paymentId, Priority = 1, IsDefault = true },
				new() { FromStageId = paymentId, ToStageId = doneId, Priority = 1, IsDefault = true }
			]
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}

	private static ClaimsPrincipal BuildPaymentPrincipal(Guid userId) =>
		new(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.UserName, $"payment-admin-{userId:N}"),
			new Claim(AbpClaimTypes.Role, "admin")
		], "Test"));
}
