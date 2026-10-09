using System;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionDocumentContext(AdmissionApplication Application, AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition, AdmissionDocumentPolicy? Policy, AdmissionWorkflowInstanceStatus WorkflowStatus)
{
	public bool CanCollectDuringForm { get; init; }
	public bool IsOnboarding { get; init; }
	public bool IsActive => IsOnboarding || WorkflowStatus == AdmissionWorkflowInstanceStatus.Active && Application.CurrentStageInstanceId == Stage.Id &&
		Stage.Status == AdmissionRuntimeStageStatus.Active && Application.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted or
		AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Waitlisted or
		AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
}

[RemoteService(false)]
public class AdmissionDocumentAccessService(IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages, IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles, IRepository<Division, Guid> divisions, IUserScopeAccessService scopes,
	IRepository<DocumentType, Guid> documentTypes, IRepository<Standard, Guid> standards,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions,
	IRepository<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingCase, Guid> onboardingCases) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionDocumentContext> GetContextAsync(Guid applicationId, Guid stageId, bool requireActive = true, bool onboarding = false)
	{
		var application = await applications.FindAsync(applicationId) ?? throw new EntityNotFoundException(typeof(AdmissionApplication), applicationId);
		var offeringQuery = AdmissionOfferingScopeQuery.Apply(await offerings.GetQueryableAsync(), await cycles.GetQueryableAsync(),
			await divisions.GetQueryableAsync(), await scopes.GetCurrentUserScopeAsync());
		if (!await AsyncExecuter.AnyAsync(offeringQuery.Where(item => item.Id == application.AdmissionOfferingId)))
			throw new EntityNotFoundException(typeof(AdmissionOffering), application.AdmissionOfferingId);
		var stage = await stages.FindAsync(stageId) ?? throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId);
		if (workflow?.ApplicationId != applicationId || workflow.WorkflowDefinitionId != application.WorkflowDefinitionId || stage.StageType != AdmissionWorkflowStageType.DocumentVerification)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId || definition.StageType != AdmissionWorkflowStageType.DocumentVerification)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var context = new AdmissionDocumentContext(application, stage, definition, ReadPolicy(definition), workflow.Status);
		if (onboarding)
		{
			var item = application.OnboardingCaseId.HasValue ? await onboardingCases.FindAsync(application.OnboardingCaseId.Value) : null;
			if (item == null || item.ApplicationId != application.Id || item.StudentId != application.StudentId ||
				application.Status != AdmissionApplicationStatus.AdmissionFinalized || item.Status == ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingStatus.Cancelled)
				throw Invalid("Document actions are available only before Onboarding activation.");
			if (requireActive)
			{
				await AuthorizationService.CheckAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
				if (item.StudentEnrollmentId.HasValue) throw Invalid("Document changes are available only before activation.");
			}
			var snapshot = ShikshaMitra.Students.StudentEnrollments.Onboarding.OnboardingRequirementSnapshotService.Read(item).SingleOrDefault(p => p.StageId == stageId);
			if (snapshot == null) throw Invalid("This stage is not part of the saved onboarding document requirements.");
			context = context with { Policy = snapshot.Policy, IsOnboarding = !item.StudentEnrollmentId.HasValue };
		}
		if (context.Policy?.CollectionFormStageCode != null && workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
			stage.Status == AdmissionRuntimeStageStatus.Pending && application.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted &&
			application.CurrentStageInstanceId.HasValue)
		{
			var current = await stages.FindAsync(application.CurrentStageInstanceId.Value);
			if (current?.WorkflowInstanceId == workflow.Id && current.Status == AdmissionRuntimeStageStatus.Active && current.StageType == AdmissionWorkflowStageType.Form)
				context = context with { CanCollectDuringForm = AdmissionDocumentCollectionRules.IsLinkedForm(
					await definitions.FindAsync(current.DefinitionStageId), definition, context.Policy,
					await transitions.GetListAsync(item => item.WorkflowDefinitionId == workflow.WorkflowDefinitionId)) };
		}
		if (requireActive && (!context.IsActive || context.Policy == null)) throw Invalid("The managed document verification stage must be active.");
		return context;
	}
	public static AdmissionDocumentRequirement GetRequirement(AdmissionDocumentContext context, string code) =>
		context.Policy?.Requirements.SingleOrDefault(item => item.Code == code) ?? throw Invalid("This document requirement does not belong to the application stage.");
	public async Task<DocumentType?> ValidateMappingAsync(AdmissionDocumentRequirement requirement)
	{
		if (!requirement.DocumentTypeId.HasValue) return null;
		var documentType = await documentTypes.FindAsync(requirement.DocumentTypeId.Value);
		if (documentType == null || documentType.TenantId != CurrentTenant.Id) throw Invalid("The mapped document type is unavailable for this school.");
		if (requirement.CollectionMode != "original" && (!double.IsFinite(documentType.MaxSizeMB) || documentType.MaxSizeMB <= 0 ||
			!requirement.AllowedExtensions.Any(extension => documentType.GetAcceptedFormats().Any(format => format.ToString().Equals(extension, StringComparison.OrdinalIgnoreCase)))))
			throw Invalid("The mapped document type must allow at least one configured safe file format and a positive size limit.");
		return documentType;
	}
	public async Task ValidateClassesAsync(AdmissionDocumentRequirement requirement)
	{
		if (requirement.StandardIds == null) return;
		var ids = requirement.StandardIds.ToArray();
		var count = await standards.CountAsync(item => item.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, item.Id));
		if (count != ids.Length) throw Invalid("One or more selected classes are unavailable for this school. Update the document requirement.");
	}
	public static AdmissionDocumentPolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionDocumentPolicy.Read(AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.DocumentVerification, definition.ConfigurationJson)); }
		catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionDocumentErrorCodes.InvalidPolicy, exception.Message); }
	}
	public static void EnsureConcurrency(IHasConcurrencyStamp entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || entity.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionDocumentErrorCodes.ConcurrencyConflict, "This application changed. Refresh the document workspace before saving.");
	}
	private static BusinessException Invalid(string message) => new(AdmissionDocumentErrorCodes.InvalidState, message);
}
