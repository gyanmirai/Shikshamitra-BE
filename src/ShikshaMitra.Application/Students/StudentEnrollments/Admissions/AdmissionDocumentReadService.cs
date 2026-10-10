using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDocumentReadService(AdmissionDocumentAccessService access, AdmissionDocumentEvaluationService evaluator,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions, AdmissionWorkflowTransitionSelector selector, IPermissionChecker permissions, ShikshaMitra.Students.StudentEnrollments.Onboarding.OnboardingRequirementSnapshotService onboardingRequirements)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionDocumentVerificationDto> GetAsync(Guid applicationId, Guid stageId, bool onboarding = false)
	{
		var context = await access.GetContextAsync(applicationId, stageId, false, onboarding);
		var result = new AdmissionDocumentVerificationDto
		{
			ApplicationId = applicationId, StageInstanceId = stageId, Policy = context.Policy,
			DocumentVerificationEnabled = context.Policy != null, IsActive = context.IsActive,
			CanCollectDuringForm = context.CanCollectDuringForm,
			ConcurrencyStamp = context.Application.ConcurrencyStamp
		};
		if (context.Policy == null) return result;
		var evaluation = await evaluator.EvaluateAsync(context.Application, stageId, context.Policy);
		result.Blockers = evaluation.Blockers;
		if (!context.IsActive) result.Blockers.Add(context.CanPrepare
			? "Documents can be prepared now. Continue admission from the current journey stage."
			: "This document verification stage is read-only.");
		else if (context.Application.Status == AdmissionApplicationStatus.Draft) result.Blockers.Add("Submit the application before completing document verification.");
		var canDefer = !onboarding && context.IsActive && context.Application.Status != AdmissionApplicationStatus.Draft && await onboardingRequirements.CanDeferAsync(context.Application);
        var routeReady = false;
		if (!onboarding && (result.Blockers.Count == 0 || canDefer))
		{
			var values = new Dictionary<string, string>(evaluation.Values, StringComparer.OrdinalIgnoreCase);
			foreach (var fact in evaluation.Facts) values[fact.Key] = fact.Value;
			try { selector.Select(await transitions.GetListAsync(item => item.FromStageId == context.Definition.Id), values, null, false); routeReady = true; }
			catch (BusinessException) { result.Blockers.Add("Configure an eligible next-stage route before completing document verification."); }
		}
		result.CanComplete = !onboarding && routeReady && (result.Blockers.Count == 0 || canDefer);
		var grants = new Dictionary<string, bool>();
		foreach (var permission in Permissions.All) grants[permission] = await permissions.IsGrantedAsync(permission);
		if (onboarding && !await permissions.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit))
			foreach (var permission in new[] { Permissions.Upload, Permissions.RecordOriginal, Permissions.Review, Permissions.Waive }) grants[permission] = false;
		result.Requirements = evaluation.Requirements.Select(item => MapRequirement(item, context, grants)).ToList();
		foreach (var row in result.Requirements)
		{
			var requirement = context.Policy.Requirements.Single(item => item.Code == row.Code);
			row.AllowedExtensions = requirement.AllowedExtensions.ToArray();
			row.MaxFileSizeMb = requirement.MaxFileSizeMb;
			try
			{
				var mapped = await access.ValidateMappingAsync(requirement);
				row.AllowedExtensions = requirement.AllowedExtensions.Where(extension => mapped == null || mapped.GetAcceptedFormats().Any(format => format.ToString().Equals(extension, StringComparison.OrdinalIgnoreCase))).ToArray();
				row.MaxFileSizeMb = Math.Min(requirement.MaxFileSizeMb, mapped?.MaxSizeMB ?? requirement.MaxFileSizeMb);
			}
			catch (BusinessException)
			{
				row.CanUpload = false; row.CanRecordOriginal = false;
				row.Blockers.Add("The mapped document type is unavailable. Existing evidence history remains available.");
			}
		}
		return result;
	}

	private AdmissionDocumentRequirementDto MapRequirement(AdmissionDocumentRequirementEvaluation item, AdmissionDocumentContext context,
		Dictionary<string, bool> grants)
	{
		var latest = item.Latest;
		var active = context.CanPrepare && item.ConditionStatus is not "notMatched" and not "undetermined";
		var canCollect = active;
		var reviewerAllowed = !context.Policy!.RequireIndependentReview || latest != null &&
			latest.SubmittedBy != CurrentUser.Id && latest.FileSubmittedBy != CurrentUser.Id && latest.OriginalSeenBy != CurrentUser.Id;
		return new()
		{
			Code = item.Requirement.Code, Name = item.Requirement.Name, Description = item.Requirement.Description,
			Requirement = item.Requirement.Requirement, CollectionMode = item.Requirement.CollectionMode, IsRequired = item.IsRequired,
			ConditionStatus = item.ConditionStatus, Status = item.Status, Blockers = item.Blockers,
			CanUpload = canCollect && grants[Permissions.Upload] && item.Requirement.CollectionMode is "upload" or "either" or "both",
			CanRecordOriginal = active && grants[Permissions.RecordOriginal] && item.Requirement.CollectionMode is "original" or "either" or "both",
			CanWaive = active && grants[Permissions.Waive] && context.Policy.AllowWaivers && item.Status != "waived",
			CanReview = active && grants[Permissions.Review] && reviewerAllowed && item.Status == "pendingReview" && latest != null,
			CanAccept = active && grants[Permissions.Review] && reviewerAllowed && item.Status == "pendingReview" && latest != null &&
				AdmissionDocumentRules.HasCollectionEvidence(latest, item.Requirement.CollectionMode),
			Evidence = item.Evidence.Select(evidence => MapEvidence(evidence, item.Reviews, grants[Permissions.Download])).ToList()
		};
	}
	public static AdmissionDocumentEvidenceDto MapEvidence(AdmissionDocumentEvidence evidence, IEnumerable<AdmissionDocumentReview> reviews, bool canDownload) => new()
	{
		Id = evidence.Id, VersionNumber = evidence.VersionNumber, Kind = evidence.Kind, FileName = evidence.FileName,
		ContentType = evidence.ContentType, Size = evidence.Size, Sha256Hash = evidence.Sha256Hash, OriginalSeen = evidence.OriginalSeen,
		OriginalSeenBy = evidence.OriginalSeenBy, OriginalSeenAt = evidence.OriginalSeenAt, SubmittedBy = evidence.SubmittedBy,
		SubmittedAt = evidence.SubmittedAt, Notes = evidence.Notes, Status = AdmissionDocumentRules.Status(evidence, reviews),
		CanDownload = canDownload && evidence.BlobName != null,
		Reviews = reviews.Where(review => review.EvidenceId == evidence.Id).OrderBy(review => review.ReviewedAt).Select(review => new AdmissionDocumentReviewDto
		{
			Id = review.Id, Decision = review.Decision, Reason = review.Reason, ReviewedBy = review.ReviewedBy, ReviewedAt = review.ReviewedAt
		}).ToList()
	};
}
