using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
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
		=> (await GetManyAsync(applicationId, [stageId], onboarding)).Single();

	internal async Task<List<AdmissionDocumentVerificationDto>> GetManyAsync(Guid applicationId, IReadOnlyCollection<Guid> stageIds, bool onboarding = false)
	{
		var contexts = await access.GetReadContextsAsync(applicationId, stageIds, onboarding);
		var policyContexts = contexts.Where(context => context.Policy != null).ToList();
		var grants = new Dictionary<string, bool>();
		var mappings = new Dictionary<Guid, DocumentType>();
		ILookup<Guid, AdmissionWorkflowTransitionDefinition> routes = Array.Empty<AdmissionWorkflowTransitionDefinition>().ToLookup(route => route.FromStageId);
		var evaluations = new List<AdmissionDocumentEvaluation>();
		var canDefer = false;
		if (policyContexts.Count > 0)
		{
			evaluations = await evaluator.EvaluateManyAsync(policyContexts[0].Application,
				policyContexts.Select(context => (context.Stage.Id, context.Policy!)).ToList());
			foreach (var permission in Permissions.All) grants[permission] = await permissions.IsGrantedAsync(permission);
			if (onboarding && !await permissions.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit))
				foreach (var permission in new[] { Permissions.Upload, Permissions.RecordOriginal, Permissions.Review, Permissions.Waive }) grants[permission] = false;
			mappings = await access.ReadMappingTypesAsync(policyContexts.SelectMany(context => context.Policy!.Requirements));
			if (!onboarding && policyContexts.Any(context => context.IsActive && context.Application.Status != AdmissionApplicationStatus.Draft))
				canDefer = await onboardingRequirements.CanDeferAsync(policyContexts[0].Application);
			var definitionIds = policyContexts.Where((context, index) => !onboarding && context.IsActive &&
				context.Application.Status != AdmissionApplicationStatus.Draft && (evaluations[index].Blockers.Count == 0 || canDefer))
				.Select(context => context.Definition.Id).Distinct().ToArray();
			if (definitionIds.Length > 0)
				routes = (await transitions.GetListAsync(route => Enumerable.Contains(definitionIds, route.FromStageId))).ToLookup(route => route.FromStageId);
		}
		var result = new List<AdmissionDocumentVerificationDto>();
		var evaluationIndex = 0;
		foreach (var context in contexts)
			result.Add(Build(context, context.Policy == null ? null : evaluations[evaluationIndex++], onboarding, grants, mappings, routes, canDefer));
		return result;
	}

	private AdmissionDocumentVerificationDto Build(AdmissionDocumentContext context, AdmissionDocumentEvaluation? suppliedEvaluation,
		bool onboarding, Dictionary<string, bool> grants, IReadOnlyDictionary<Guid, DocumentType> mappings,
		ILookup<Guid, AdmissionWorkflowTransitionDefinition> routes, bool sharedCanDefer)
	{
		var result = new AdmissionDocumentVerificationDto
		{
			ApplicationId = context.Application.Id, StageInstanceId = context.Stage.Id, Policy = context.Policy,
			DocumentVerificationEnabled = context.Policy != null, IsActive = context.IsActive,
			CanCollectDuringForm = context.CanCollectDuringForm,
			ConcurrencyStamp = context.Application.ConcurrencyStamp
		};
		if (context.Policy == null) return result;
		var evaluation = suppliedEvaluation!;
		result.Blockers = evaluation.Blockers;
		if (!context.IsActive) result.Blockers.Add(context.CanPrepare
			? "Documents can be prepared now. Continue admission from the current journey stage."
			: "This document verification stage is read-only.");
		else if (context.Application.Status == AdmissionApplicationStatus.Draft) result.Blockers.Add("Submit the application before completing document verification.");
		var canDefer = !onboarding && context.IsActive && context.Application.Status != AdmissionApplicationStatus.Draft && sharedCanDefer;
        var routeReady = false;
		if (!onboarding && (result.Blockers.Count == 0 || canDefer))
		{
			var values = new Dictionary<string, string>(evaluation.Values, StringComparer.OrdinalIgnoreCase);
			foreach (var fact in evaluation.Facts) values[fact.Key] = fact.Value;
			try { selector.Select(routes[context.Definition.Id].ToList(), values, null, false); routeReady = true; }
			catch (BusinessException) { result.Blockers.Add("Configure an eligible next-stage route before completing document verification."); }
		}
		result.CanComplete = !onboarding && routeReady && (result.Blockers.Count == 0 || canDefer);
		result.Requirements = evaluation.Requirements.Select(item => MapRequirement(item, context, grants)).ToList();
		foreach (var row in result.Requirements)
		{
			var requirement = context.Policy.Requirements.Single(item => item.Code == row.Code);
			row.AllowedExtensions = requirement.AllowedExtensions.ToArray();
			row.MaxFileSizeMb = requirement.MaxFileSizeMb;
			try
			{
				var mapped = access.ValidateMapping(requirement, requirement.DocumentTypeId.HasValue ? mappings.GetValueOrDefault(requirement.DocumentTypeId.Value) : null);
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
		OriginalSeenBy = evidence.OriginalSeenBy, OriginalSeenAt = evidence.OriginalSeenAt, SubmittedBy = evidence.SubmittedBy, ParentRegistrationId = evidence.ParentRegistrationId, ParentContactJson = evidence.ParentContactJson,
		SubmittedAt = evidence.SubmittedAt, Notes = evidence.Notes, Status = AdmissionDocumentRules.Status(evidence, reviews),
		CanDownload = canDownload && evidence.BlobName != null,
		Reviews = reviews.Where(review => review.EvidenceId == evidence.Id).OrderBy(review => review.ReviewedAt).Select(review => new AdmissionDocumentReviewDto
		{
			Id = review.Id, Decision = review.Decision, Reason = review.Reason, ReviewedBy = review.ReviewedBy, ReviewedAt = review.ReviewedAt
		}).ToList()
	};
}
