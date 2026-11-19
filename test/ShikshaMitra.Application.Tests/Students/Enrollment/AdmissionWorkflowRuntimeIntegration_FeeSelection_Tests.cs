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
	public async Task Fee_selection_persists_with_concurrency_and_keeps_collection_owned_by_Finance()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Fee selection test"), true);
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var workflow = await CreateFeeSelectionWorkflowAsync(offering.ApplicationFormDefinitionId!.Value);
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
		(await fees.GetAsync(application.Id)).Available.ShouldBeTrue();
		var submitted = await _applicationAppService.SubmitAsync(application.Id);
		submitted.ValidationErrors.ShouldBeEmpty();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime.Stages.Single(item => item.Id == runtime.CurrentStageInstanceId).StageType.ShouldBe(AdmissionWorkflowStageType.Payment);
		var stage = runtime.CurrentStageInstanceId!.Value;
		var state = await fees.GetAsync(application.Id);
		state.CanEdit.ShouldBeTrue(); state.IsRequired.ShouldBeTrue(); state.OwnerStageId.ShouldBe(stage);
		var paymentService = GetRequiredService<IAdmissionPaymentAppService>();
		(await paymentService.GetAsync(application.Id, stage)).CanRecord.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => paymentService.RecordAsync(application.Id, stage,
			new() { Amount = 500m, Method = "cash", IdempotencyKey = "fee-missing-record", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionPaymentStageErrorCodes.InvalidState);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
			new() { ExpectedCurrentStageInstanceId = stage, ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "missing-fee-prerequisite" })))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.CollectionOrDeferralRequired);
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
		(await Should.ThrowAsync<BusinessException>(() => paymentService.RecordAsync(application.Id, stage,
			new() { Amount = 500m, Method = "cash", IdempotencyKey = "fee-paid", ConcurrencyStamp = state.ConcurrencyStamp })))
			.Code.ShouldBe(AdmissionPaymentStageErrorCodes.InvalidState);
		(await GetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>().GetListAsync(p => p.ApplicationId == application.Id)).ShouldBeEmpty();
		var unchanged = await fees.GetAsync(application.Id);
		unchanged.Selection!.StructureId.ShouldBe(structure.Id);
		unchanged.Selection.RevisionNumber.ShouldBe(state.Selection.RevisionNumber);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => fees.GetAsync(application.Id));
		var limitedId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(limitedId, "fee-no-scope");
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
