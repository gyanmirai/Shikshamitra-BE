using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistWorkflow(
	IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> templateRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	AdmissionCrmDocumentAccessReader accessReader,
	AdmissionCrmDocumentTimelineWriter timelineWriter,
	StudentScopeAccessService studentScopeAccessService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(
		Guid registrationApplicationId,
		AssignAdmissionCrmDocumentChecklistDto input)
	{
		var application = await accessReader.GetApplicationAsync(registrationApplicationId, nameof(AssignChecklistAsync));
		var template = await GetTemplateAsync(input.ChecklistTemplateId);
		if (template.AcademicYearId != application.AcademicYearId || template.Status != AdmissionCrmDocumentChecklistTemplateStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedDocumentChecklistRequired");
		var queryable = await assignmentRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == registrationApplicationId)))
			throw new BusinessException("AdmissionCrm:DocumentChecklistAlreadyAssigned");
		var assignment = CreateAssignment(application, template);
		await assignmentRepository.InsertAsync(assignment, autoSave: true);
		await timelineWriter.AddAsync(application.LeadId, application.AcademicYearId,
			AdmissionCrmTimelineEventType.DocumentChecklistAssigned, "Document checklist assigned",
			$"{template.Name} v{template.Version}.", assignment.Id);
		return await BuildDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto?> AssignApplicableChecklistFromAutomationAsync(
		Guid registrationApplicationId,
		AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		var application = await accessReader.GetApplicationAsync(registrationApplicationId, nameof(AssignApplicableChecklistFromAutomationAsync));
		var assignmentQueryable = await assignmentRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(assignmentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == registrationApplicationId));
		if (existing != null) return await BuildDtoAsync(existing, application);
		var templateQueryable = await templateRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(templateQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == application.AcademicYearId &&
			x.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published));
		var selected = published.Select(template => new
			{
				Template = template,
				Scope = AdmissionCrmDocumentModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(template.ApplicabilityJson)
			})
			.Where(x => AdmissionCrmDocumentModelMapper.MatchesApplicability(x.Scope, applicability))
			.OrderByDescending(x => AdmissionCrmDocumentModelMapper.ApplicabilityScore(x.Scope))
			.ThenByDescending(x => x.Template.Version)
			.Select(x => x.Template)
			.FirstOrDefault();
		if (selected == null) return null;
		var assignment = CreateAssignment(application, selected);
		await assignmentRepository.InsertAsync(assignment, autoSave: true);
		await timelineWriter.AddAsync(application.LeadId, application.AcademicYearId,
			AdmissionCrmTimelineEventType.DocumentChecklistAssigned, "Document checklist assigned automatically",
			$"{selected.Name} v{selected.Version}.", assignment.Id);
		return await BuildDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId)
	{
		var application = await accessReader.GetApplicationAsync(registrationApplicationId, nameof(GetChecklistAsync));
		var assignment = await accessReader.GetAssignmentByApplicationAsync(registrationApplicationId);
		return await BuildDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto?> GetChecklistFromApplicantAsync(Guid leadId)
	{
		var assignment = await accessReader.GetAssignmentByLeadOrDefaultAsync(leadId);
		if (assignment == null) return null;
		var application = await applicationRepository.GetAsync(assignment.RegistrationApplicationId);
		return await BuildDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> BuildDtoAsync(
		AdmissionCrmDocumentChecklistAssignment assignment,
		AdmissionCrmRegistrationApplication application)
	{
		var template = AdmissionCrmDocumentModelMapper.Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson);
		var queryable = await documentRepository.GetQueryableAsync();
		var allDocuments = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == assignment.Id));
		var currentByRequirement = allDocuments.Where(x => !x.SupersededAt.HasValue)
			.GroupBy(x => x.RequirementKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Version).First(), StringComparer.OrdinalIgnoreCase);
		var responses = AdmissionCrmDocumentModelMapper.Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson);
		var prefill = AdmissionCrmDocumentModelMapper.Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson);
		var documents = template.Requirements.OrderBy(x => x.DisplayOrder).Select(requirement =>
		{
			var required = AdmissionCrmDocumentModelMapper.IsRequirementCurrentlyRequired(requirement, responses, prefill);
			return AdmissionCrmDocumentModelMapper.ToDocumentDto(requirement, currentByRequirement.GetValueOrDefault(requirement.Key), required);
		}).ToList();
		var blockingReasons = documents.Where(x => x.IsCurrentlyRequired && x.BlocksAdmissionReadiness && !AdmissionCrmDocumentModelMapper.IsDocumentComplete(x))
			.Select(x => $"{x.RequirementName}: {AdmissionCrmDocumentModelMapper.GetIncompleteReason(x)}").ToList();
		var requiredDocuments = documents.Where(x => x.IsCurrentlyRequired).ToList();
		var completed = requiredDocuments.Count(AdmissionCrmDocumentModelMapper.IsDocumentComplete);
		return new AdmissionCrmApplicantDocumentChecklistDto
		{
			AssignmentId = assignment.Id,
			RegistrationApplicationId = assignment.RegistrationApplicationId,
			ChecklistTemplateId = assignment.ChecklistTemplateId,
			ChecklistTemplateVersion = assignment.ChecklistTemplateVersion,
			ChecklistName = template.Name,
			CompletionPercentage = requiredDocuments.Count == 0 ? 100 : Math.Round((decimal)completed * 100 / requiredDocuments.Count, 2),
			AdmissionReady = blockingReasons.Count == 0 && application.Status == AdmissionCrmRegistrationStatus.Verified,
			ReadinessConfirmed = assignment.ReadinessConfirmedAt.HasValue,
			ReadinessOverridden = assignment.ReadinessOverridden,
			RequiredReadinessApprovalCount = template.ReadinessApprovalCount,
			ReceivedReadinessApprovalCount = assignment.ReadinessApprovalCount,
			AwaitingSecondReadinessApproval = template.ReadinessApprovalCount == 2 && assignment.ReadinessApprovalCount == 1,
			BlockingReasons = blockingReasons,
			Documents = documents
		};
	}

	private async Task<AdmissionCrmDocumentChecklistTemplate> GetTemplateAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentChecklistTemplateNotFound");
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, nameof(AssignChecklistAsync));
		return template;
	}

	private AdmissionCrmDocumentChecklistAssignment CreateAssignment(
		AdmissionCrmRegistrationApplication application,
		AdmissionCrmDocumentChecklistTemplate template) => new(
		GuidGenerator.Create(), CurrentTenant.Id, application.AcademicYearId, application.LeadId, application.Id,
		template.Id, template.Version, AdmissionCrmDocumentModelMapper.Serialize(AdmissionCrmDocumentModelMapper.ToTemplateDto(template)), Clock.Now);
}
