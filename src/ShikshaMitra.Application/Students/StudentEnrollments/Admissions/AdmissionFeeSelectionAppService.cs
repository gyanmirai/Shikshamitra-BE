using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionFeeSelectionAppService(
	AdmissionFeeSelectionAccessService access, AdmissionFeeStructureReader structures,
	AdmissionFeeSelectionOnboardingReader onboarding,
	IPermissionChecker permissionChecker,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities) : ApplicationService, IAdmissionFeeSelectionAppService
{
	public async Task<AdmissionFeeSelectionDto> GetAsync(Guid applicationId)
	{
		await EnsureReadPermissionAsync();
		return await ReadAsync(await GetReadApplicationAsync(applicationId));
	}

	public async Task<List<AdmissionFeeStructureCandidateDto>> GetCandidatesAsync(Guid applicationId)
	{
		await EnsureReadPermissionAsync();
		var application = await GetReadApplicationAsync(applicationId);
		var context = await access.GetContextAsync(application);
		if (!context.IsEditable || !await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit))
			return [];
		return await structures.GetCandidatesAsync(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	[UnitOfWork(true)]
	public async Task<AdmissionFeeSelectionDto> SaveAsync(Guid applicationId, SaveAdmissionFeeSelectionDto input)
	{
		var application = await access.GetApplicationAsync(applicationId);
		EnsureConcurrency(application, input.ConcurrencyStamp);
		var context = await access.GetContextAsync(application);
		if (context.HasPaymentEvidence) throw new BusinessException(AdmissionFeeSelectionErrorCodes.Locked,
			"Payment evidence already exists. Ask Finance to review a correction or refund.");
		if (!context.IsEditable) throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidState,
			"Confirm the fee structure in an unfinished Fee structure confirmation stage while the admission journey is open.");
		var snapshot = await structures.CreateSnapshotAsync(application, context.Owner!.Id, context.Currency, input, Actor());
		var previous = application.GetFeeSelection();
		application.SaveFeeSelection(snapshot, context.HasPaymentEvidence);
		await SaveAsync(application, "AdmissionFeeSelectionSaved", new { Previous = previous, Selection = snapshot });
		return await ReadAsync(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<AdmissionFeeSelectionDto> ConfirmOnboardingAsync(Guid applicationId, ConfirmAdmissionFeeSelectionDto input)
	{
		var application = await access.GetApplicationAsync(applicationId, requireOnboarding: true);
		var item = await access.GetOnboardingCaseAsync(application);
		if (item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidState, "This onboarding case is no longer editable.");
		if (await onboarding.IsConfirmedAsync(application)) return await ReadAsync(application);
		EnsureConcurrency(application, input.ConcurrencyStamp);
		var blocker = await onboarding.GetBlockerAsync(application, requireConfirmation: false);
		if (blocker != null) throw new BusinessException(AdmissionFeeSelectionErrorCodes.AssignmentMismatch, blocker);
		application.ConfirmOnboardingFeeSelection(Actor(), new DateTimeOffset(Clock.Now.ToUniversalTime()));
		await SaveAsync(application, "AdmissionFeeSelectionConfirmedInOnboarding", application.GetFeeSelection()!);
		return await ReadAsync(application);
	}

	private async Task<AdmissionFeeSelectionDto> ReadAsync(AdmissionApplication application)
	{
		var context = await access.GetContextAsync(application);
		var snapshot = application.GetFeeSelection();
		var deferral = application.GetFeeCollectionDeferral();
		var caseEditable = false;
		if (application.OnboardingCaseId.HasValue && await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit))
		{
			var item = await access.FindOnboardingCaseAsync(application);
			caseEditable = item != null && item.Status is not (StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled);
		}
		var blocker = await onboarding.GetBlockerAsync(application);
		var paymentCompletion = await LazyServiceProvider.LazyGetRequiredService<AdmissionPaymentCompletionReader>().ReadAsync(context);
		var confirmed = snapshot != null && blocker == null;
		return new AdmissionFeeSelectionDto
		{
			ApplicationId = application.Id, ConcurrencyStamp = application.ConcurrencyStamp,
			OwnerStageId = context.Owner?.Id, OwnerStageName = context.Owner?.Name, OwnerStageType = context.Owner?.StageType,
			CurrentStageId = application.CurrentStageInstanceId, Currency = context.Currency,
			Available = context.Owner != null || snapshot != null, IsRequired = context.IsRequired,
			IsLocked = context.HasPaymentEvidence, HasRecordedFeePayment = context.HasPaymentEvidence,
			PaymentCompletion = paymentCompletion,
			FeeCollectionDeferral = deferral == null ? null : new AdmissionFeeCollectionDeferralDto
			{
				StageId = deferral.StageId, Reason = deferral.Reason, DeferredBy = deferral.DeferredBy, DeferredAt = deferral.DeferredAt
			},
			CanEdit = context.IsEditable &&
				await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit),
			OnboardingConfirmed = confirmed, AutomaticallyConfirmedFromAdmissions = confirmed && snapshot?.OnboardingConfirmedAt == null,
			OnboardingBlocker = blocker,
			CanConfirmOnboarding = snapshot != null && !confirmed && caseEditable &&
				await onboarding.GetBlockerAsync(application, requireConfirmation: false) == null,
			Selection = snapshot == null ? null : new AdmissionFeeSelectionSnapshotDto
			{
				StructureId = snapshot.StructureId, StructureName = snapshot.StructureName, RevisionNumber = snapshot.RevisionNumber,
				AcademicYearId = snapshot.AcademicYearId, StandardId = snapshot.StandardId, Currency = snapshot.Currency,
				SavedAt = snapshot.SavedAt, SavedBy = snapshot.SavedBy, OnboardingConfirmedAt = snapshot.OnboardingConfirmedAt,
				Heads = snapshot.Heads.Select(h => new AdmissionFeeSelectionHeadDto { FeeHeadId = h.FeeHeadId,
					FeeHeadName = h.FeeHeadName, ConfiguredAmount = h.ConfiguredAmount, IsVariable = h.IsVariable, IsOptional = h.IsOptional,
					IsOneTime = h.IsOneTime, PaymentTermId = h.PaymentTermId, PaymentTermVersionId = h.PaymentTermVersionId }).ToList()
			}
		};
	}

	private async Task EnsureReadPermissionAsync()
	{
		if (!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View) &&
			!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View) &&
			!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View))
			throw new AbpAuthorizationException();
	}

	private async Task<AdmissionApplication> GetReadApplicationAsync(Guid id) => await access.GetApplicationAsync(id,
		requireOnboarding: !await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View) &&
			!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View));

	private async Task SaveAsync(AdmissionApplication application, string eventType, object evidence)
	{
		await applications.UpdateAsync(application, false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(eventType,
			eventType == "AdmissionFeeSelectionSaved" ? "Admissions fee structure confirmed." : "Saved fee structure confirmed in Onboarding.",
			application.ApplicantId, application.Id, metadataJson: JsonSerializer.Serialize(evidence)), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private Guid Actor() => CurrentUser.Id ?? throw new AbpAuthorizationException();
	private static void EnsureConcurrency(AdmissionApplication application, string stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || application.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.ConcurrencyConflict, "The application changed. Refresh before saving the fee selection.");
	}
}
