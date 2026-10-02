using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Text.Json;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Fee_selection_in_Offer_should_gate_issue_and_remain_the_same_readonly_choice_in_later_Payment()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Offer fee selection test"), true);
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var workflow = await CreateOfferThenPaymentFeeWorkflowAsync();
		await WithUnitOfWorkAsync(async () => { var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true); });
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Offer Fee QA")).Id, offering.Id);
		var submitted = await _applicationAppService.SubmitAsync(application.Id);
		var approved = await _applicationAppService.ApproveAsync(application.Id, new() { ConcurrencyStamp = submitted.Application.ConcurrencyStamp });
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() {
			ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value, ConcurrencyStamp = approved.ConcurrencyStamp,
			IdempotencyKey = "fee-offer-entry" });
		var offerStage = runtime.CurrentStageInstanceId!.Value;
		var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
		var state = await fees.GetAsync(application.Id); state.OwnerStageType.ShouldBe(AdmissionWorkflowStageType.Offer);
		var offerService = GetRequiredService<IAdmissionOfferAppService>();
		(await offerService.GetAsync(application.Id, offerStage)).CanIssue.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => offerService.IssueAsync(application.Id, offerStage,
			new() { IdempotencyKey = "fee-offer-without-selection", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Required);
		var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var structure = await CreateFeeSelectionReferenceAsync(offering, cycle.AcademicYearId);
		state = await fees.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = state.ConcurrencyStamp });
		var issued = await offerService.IssueAsync(application.Id, offerStage, new() { IdempotencyKey = "fee-offer-issue", ConcurrencyStamp = state.ConcurrencyStamp });
		var accepted = await offerService.RespondAsync(application.Id, offerStage, new() { Accepted = true, ConfirmTermsAccepted = true,
			IdempotencyKey = "fee-offer-accept", ConcurrencyStamp = issued.ConcurrencyStamp });
		runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() { ExpectedCurrentStageInstanceId = offerStage,
			IdempotencyKey = "fee-enter-later-payment", ConcurrencyStamp = accepted.ConcurrencyStamp });
		var later = await fees.GetAsync(application.Id); later.OwnerStageId.ShouldBe(offerStage); later.CanEdit.ShouldBeFalse();
		later.Selection!.StructureId.ShouldBe(structure.Id); later.IsLocked.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => fees.SaveAsync(application.Id,
			new() { StructureId = structure.Id, ConcurrencyStamp = later.ConcurrencyStamp }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidState);
		(await GetRequiredService<IAdmissionPaymentAppService>().GetAsync(application.Id, runtime.CurrentStageInstanceId!.Value))
			.CanRecord.ShouldBeTrue();
	}

	[Fact]
	public async Task Fee_selection_should_persist_validate_concurrency_and_lock_before_payment_verification()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Fee selection test"), true);
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var workflow = await CreateManagedPaymentWorkflowAsync(requiresFeeStructureSelection: true);
		await WithUnitOfWorkAsync(async () => {
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Fee QA")).Id, offering.Id);
		var permissionChecker = Substitute.For<IPermissionChecker>();
		var access = GetRequiredService<AdmissionFeeSelectionAccessService>();
		var permissionService = new AdmissionFeeSelectionAppService(access, GetRequiredService<AdmissionFeeStructureReader>(),
			GetRequiredService<AdmissionFeeSelectionOnboardingReader>(), permissionChecker, _applicationRepository,
			GetRequiredService<IRepository<AdmissionActivity, Guid>>());
		await Should.ThrowAsync<AbpAuthorizationException>(() => permissionService.GetAsync(application.Id));
		permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View).Returns(true);
		await Should.ThrowAsync<EntityNotFoundException>(() => permissionService.GetAsync(application.Id));
		var fees = GetRequiredService<IAdmissionFeeSelectionAppService>();
		(await fees.GetAsync(application.Id)).Available.ShouldBeFalse();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new() {
			ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
			ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "fee-enter-payment" });
		var stage = runtime.CurrentStageInstanceId!.Value;
		var state = await fees.GetAsync(application.Id);
		state.CanEdit.ShouldBeTrue(); state.IsRequired.ShouldBeTrue(); state.OwnerStageId.ShouldBe(stage);
		var paymentService = GetRequiredService<IAdmissionPaymentAppService>();
		(await paymentService.GetAsync(application.Id, stage)).CanRecord.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => paymentService.RecordAsync(application.Id, stage,
			new() { Amount = 500m, Method = "cash", IdempotencyKey = "fee-missing-record", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Required);
		(await GetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>().GetListAsync(p => p.ApplicationId == application.Id)).ShouldBeEmpty();
		(await Should.ThrowAsync<BusinessException>(() => fees.SaveAsync(application.Id,
			new() { StructureId = Guid.NewGuid(), ConcurrencyStamp = state.ConcurrencyStamp }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidSelection);
		var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var structure = await CreateFeeSelectionReferenceAsync(offering, cycle.AcademicYearId);
		var candidates = await fees.GetCandidatesAsync(application.Id);
		candidates.Select(s => s.StructureId).ShouldContain(structure.Id);
		(await Should.ThrowAsync<BusinessException>(() => fees.SaveAsync(application.Id,
			new() { StructureId = structure.Id, OptionalHeadIds = [Guid.NewGuid()], ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.InvalidSelection);
		state = await fees.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = state.ConcurrencyStamp });
		state.Selection!.Heads.Count.ShouldBe(1); state.Selection.RevisionNumber.ShouldBe(1);
		var reloaded = await fees.GetAsync(application.Id);
		reloaded.Selection!.StructureId.ShouldBe(structure.Id);
		(await WithUnitOfWorkAsync(async () => (await _applicationRepository.GetAsync(application.Id)).GetFeeSelection()))!.StructureId.ShouldBe(structure.Id);
		(await Should.ThrowAsync<BusinessException>(() => fees.SaveAsync(application.Id,
			new() { StructureId = structure.Id, ConcurrencyStamp = "stale" }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.ConcurrencyConflict);
		var pending = await paymentService.RecordAsync(application.Id, stage,
			new() { Amount = 500m, Method = "cash", IdempotencyKey = "fee-paid", ConcurrencyStamp = state.ConcurrencyStamp });
		pending.Status.ShouldBe("pendingVerification");
		var locked = await fees.GetAsync(application.Id); locked.IsLocked.ShouldBeTrue(); locked.CanEdit.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => fees.SaveAsync(application.Id,
			new() { StructureId = structure.Id, ConcurrencyStamp = locked.ConcurrencyStamp }))).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => fees.GetAsync(application.Id));
		var limitedId = Guid.NewGuid();
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, limitedId.ToString()), new Claim(AbpClaimTypes.UserName, "fee-no-scope") ], "Test"))))
			await Should.ThrowAsync<EntityNotFoundException>(() => fees.GetAsync(application.Id));
	}

	private Task<FeeStructure> CreateFeeSelectionReferenceAsync(AdmissionOffering offering, Guid year) => WithUnitOfWorkAsync(async () =>
	{
		var income = new TransactionCategory(Guid.NewGuid(), TestTenantId, year, TransactionType.Income, "QA Form income", null);
		await GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
		var category = new FeeCategory(Guid.NewGuid(), TestTenantId, year, "QA Admission", null);
		await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
		var head = new FeeHead(Guid.NewGuid(), TestTenantId, year, category.Id, "QA Form", null, false, null, income.Id);
		await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, true);
		var metadata = new FeeStructureMetadata(Guid.NewGuid(), TestTenantId, year, offering.BoardId, offering.MediumId, null, "QA Structure metadata");
		await GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, true);
		var structure = new FeeStructure(Guid.NewGuid(), TestTenantId, metadata.Id, offering.StandardId, "QA Published plan");
		structure.FeeStructureDetails.Add(new FeeStructureDetail(Guid.NewGuid(), TestTenantId, structure.Id, head.Id, 200m, true, false, null, null));
		structure.Lock();
		await GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure, true);
		return structure;
	});

	private async Task<AdmissionWorkflowDto> CreateOfferThenPaymentFeeWorkflowAsync()
	{
		var start = Guid.NewGuid(); var offer = Guid.NewGuid(); var payment = Guid.NewGuid(); var done = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new() { Name = "Offer and fee collection", Code = $"FEE_{Guid.NewGuid():N}"[..24],
			Stages = [
				new() { Id = start, Code = "START", Name = "Offer preparation", StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 0, IsEntryStage = true, IsRequired = true },
				new() { Id = offer, Code = "OFFER", Name = "Offer", StageType = AdmissionWorkflowStageType.Offer, DisplayOrder = 1, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { offer = new AdmissionOfferStagePolicy { Version = 1, Enabled = true,
						OfferType = "unconditional", Title = "QA offer", Message = "QA", ValidityDays = 14,
						MinimumValidityDays = 1, MaximumValidityDays = 90, RequireTermsAcceptance = true, AllowedCompletionOutcomes = ["accepted"] } }) },
				new() { Id = payment, Code = "PAYMENT", Name = "Payment", StageType = AdmissionWorkflowStageType.Payment, DisplayOrder = 2, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { payment = new AdmissionPaymentStagePolicy { Version = 1, Enabled = true,
						Amount = 200m, AcceptedMethods = ["cash"], RequiresFeeStructureSelection = true } }) },
				new() { Id = done, Code = "DONE", Name = "Complete", StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 3, IsTerminalStage = true, IsRequired = true }
			], Transitions = [new() { FromStageId = start, ToStageId = offer, Priority = 1, IsDefault = true },
				new() { FromStageId = offer, ToStageId = payment, Priority = 1, IsDefault = true }, new() { FromStageId = payment, ToStageId = done, Priority = 1, IsDefault = true }] });
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}
}
