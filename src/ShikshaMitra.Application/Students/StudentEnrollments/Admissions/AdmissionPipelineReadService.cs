using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Batch projection of existing admission progress; never mutates workflow state.</summary>
[RemoteService(false)]
public class AdmissionPipelineReadService : ApplicationService, ITransientDependency
{
	private readonly IRepository<AdmissionApplicant, Guid> applicants;
	private readonly IRepository<AdmissionInquiry, Guid> inquiries;
	private readonly IRepository<AdmissionFamily, Guid> families;
	private readonly IRepository<AdmissionGuardian, Guid> guardians;
	private readonly IRepository<AdmissionCycle, Guid> cycles;
	private readonly IRepository<AdmissionApplicationStageInstance, Guid> stages;
	private readonly IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows;
	private readonly IRepository<AdmissionWorkflowStageDefinition, Guid> definitions;
	private readonly IRepository<AdmissionInquiryFollowUp, Guid> followUps;
	private readonly IRepository<Standard, Guid> standards;
	private readonly AdmissionApplicationScopeAccessService access;
	private readonly AdmissionApplicationWorkspaceQueryService workspace;
	private readonly AdmissionInquiryPeopleService people;

	public AdmissionPipelineReadService(
		IRepository<AdmissionApplicant, Guid> applicants,
		IRepository<AdmissionInquiry, Guid> inquiries,
		IRepository<AdmissionFamily, Guid> families,
		IRepository<AdmissionGuardian, Guid> guardians,
		IRepository<AdmissionCycle, Guid> cycles,
		IRepository<AdmissionApplicationStageInstance, Guid> stages,
		IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
		IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
		IRepository<AdmissionInquiryFollowUp, Guid> followUps,
		IRepository<AdmissionIntakeProfile, Guid> profiles,
		IRepository<Standard, Guid> standards,
		AdmissionApplicationScopeAccessService access,
		AdmissionApplicationWorkspaceQueryService workspace,
		AdmissionInquiryPeopleService people,
		IUserScopeAccessService userScopes
	)
	{
		this.applicants = applicants;
		this.inquiries = inquiries;
		this.families = families;
		this.guardians = guardians;
		this.cycles = cycles;
		this.stages = stages;
		this.workflows = workflows;
		this.definitions = definitions;
		this.followUps = followUps;
		_ = profiles;
		this.standards = standards;
		this.access = access;
		this.workspace = workspace;
		this.people = people;
		_ = userScopes;
	}

	public async Task<PagedResultDto<AdmissionPipelineItemDto>> GetListAsync(AdmissionPipelineListInput input)
	{
		var query = await GetQueryAsync(input);
		var myCases = input.MyCases ?? await DefaultMyCasesAsync();
		if (input.Status.HasValue) query = query.Where(row => row.Status == input.Status);
		if (!string.IsNullOrWhiteSpace(input.CurrentStageCode)) query = query.Where(row => row.CurrentStageCode == input.CurrentStageCode);
		if (!string.IsNullOrWhiteSpace(input.CurrentStageSearch)) query = query.Where(row => row.CurrentStageName != null && row.CurrentStageName.Contains(input.CurrentStageSearch));
		if (input.CurrentStageInstanceId.HasValue) query = query.Where(row => row.CurrentStageInstanceId == input.CurrentStageInstanceId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(row => row.ApplicationNumber.ToLower().Contains(search) || row.ApplicantName.ToLower().Contains(search) ||
				row.InquiryNumber != null && row.InquiryNumber.ToLower().Contains(search) ||
				row.ContactPhone != null && row.ContactPhone.Contains(search) || row.ContactEmail != null && row.ContactEmail.ToLower().Contains(search));
		}
		query = AdmissionQueueColumnFilters.Pipeline(query, input.ColumnFilters);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(Sort(query, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ownerNames = await people.GetNamesAsync(rows.Where(row => row.AssignedCounsellorId.HasValue).Select(row => row.AssignedCounsellorId!.Value));
		var inquiryIds = rows.Where(row => row.InquiryId.HasValue).Select(row => row.InquiryId!.Value).ToArray();
		var pending = await followUps.GetListAsync(item => Enumerable.Contains(inquiryIds, item.InquiryId) && item.Status == AdmissionInquiryFollowUpStatus.Scheduled);
		var stageIds = rows.Where(row => row.CurrentStageInstanceId.HasValue).Select(row => row.CurrentStageInstanceId!.Value).ToArray();
		var applicationIds = rows.Select(row => row.Id).ToArray();
		var stageQuery = await stages.GetQueryableAsync();
		var workflowQuery = await workflows.GetQueryableAsync();
		// One bounded page-level projection, rather than fetching a workflow for every table row.
		var progressRows = await AsyncExecuter.ToListAsync(
			from stage in stageQuery
			join workflow in workflowQuery on stage.WorkflowInstanceId equals workflow.Id
			where Enumerable.Contains(applicationIds, workflow.ApplicationId)
			select new { workflow.ApplicationId, Stage = stage });
		var stageRows = progressRows.Where(item => Enumerable.Contains(stageIds, item.Stage.Id)).Select(item => item.Stage).ToList();
		var progressByApplication = progressRows.ToLookup(item => item.ApplicationId);
		var definitionIds = stageRows.Select(row => row.DefinitionStageId).ToArray();
		var stageDefinitions = (await definitions.GetListAsync(item => Enumerable.Contains(definitionIds, item.Id))).ToDictionary(row => row.Id);
		var pendingByInquiry = pending.ToLookup(item => item.InquiryId);
		var stagesById = stageRows.ToDictionary(row => row.Id);
		var utcNow = Clock.Kind == DateTimeKind.Utc ? AdmissionOfferUtcTime.Normalize(Clock.Now) : Clock.Now.ToUniversalTime();
		foreach (var row in rows)
		{
			// Audit timestamps follow ABP's configured clock; runtime stage/follow-up timestamps are explicitly UTC.
			row.CreationTime = Clock.Kind == DateTimeKind.Utc ? AdmissionOfferUtcTime.Normalize(row.CreationTime) : row.CreationTime.ToUniversalTime();
			row.EnteredAt = row.EnteredAt.HasValue ? AdmissionOfferUtcTime.Normalize(row.EnteredAt.Value) : null;
			row.AssignedCounsellorName = row.AssignedCounsellorId.HasValue ? ownerNames.GetValueOrDefault(row.AssignedCounsellorId.Value) : null;
			var open = row.InquiryId.HasValue ? pendingByInquiry[row.InquiryId.Value].ToList() : [];
			var nextFollowUp = open.OrderBy(item => item.ScheduledAt).ThenBy(item => item.Id).FirstOrDefault();
			row.OpenFollowUpCount = open.Count;
			row.NextFollowUpAt = nextFollowUp == null ? null : AdmissionOfferUtcTime.Normalize(nextFollowUp.ScheduledAt);
			row.NextFollowUpPurpose = nextFollowUp?.Purpose;
			row.OverdueFollowUpCount = open.Count(item => AdmissionOfferUtcTime.Normalize(item.ScheduledAt) < utcNow);
			row.HasOverdueFollowUp = row.OverdueFollowUpCount > 0;
			row.StageProgress = progressByApplication[row.Id].OrderBy(item => item.Stage.DisplayOrder).ThenBy(item => item.Stage.Id)
				.Select(item => new AdmissionPipelineStageProgressDto { Id = item.Stage.Id, Name = item.Stage.Name, Status = item.Stage.Status }).ToList();
			if (row.CurrentStageInstanceId.HasValue && stagesById.TryGetValue(row.CurrentStageInstanceId.Value, out var stage))
			{
				row.NextRequiredAction = AdmissionWorkflowStageTypeRegistry.Find(stage.StageType)?.NextRequiredAction ?? "Review the current step";
				if (stageDefinitions.TryGetValue(stage.DefinitionStageId, out var definition))
				{
					int? dueDays = stage.StageType switch
					{
						AdmissionWorkflowStageType.Form => AdmissionFormStageAccessService.ReadPolicy(definition)?.DueInDays,
						AdmissionWorkflowStageType.Payment => AdmissionPaymentAccessService.ReadPolicy(definition)?.DueInDays,
						_ => null
					};
					row.DueAt = dueDays.HasValue && row.EnteredAt.HasValue ? row.EnteredAt.Value.AddDays(dueDays.Value) : null;
				}
			}
			else row.NextRequiredAction = row.Status is AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.OfferAccepted
				? "Review readiness and finalize admission" : "Open the application to review its workflow";
		}
		return new(count, rows);
	}

	public async Task<AdmissionPipelineOptionsDto> GetOptionsAsync(AdmissionPipelineListInput input)
	{
		// Academic filters apply, but search, owner, stage and pagination never truncate available choices.
		var query = await GetQueryAsync(input);
		var stageOptions = await AsyncExecuter.ToListAsync(query.Where(row => row.CurrentStageCode != null).Select(row => new AdmissionPipelineStageOptionDto { Code = row.CurrentStageCode!, Name = row.CurrentStageName! }).Distinct());
		return new()
		{
			CurrentUserId = CurrentUser.Id, DefaultMyCases = await DefaultMyCasesAsync(),
			Counsellors = [],
			Stages = stageOptions.GroupBy(item => item.Code, StringComparer.Ordinal).Select(group => new AdmissionPipelineStageOptionDto
			{
				Code = group.Key,
				Name = group.Select(item => item.Name).Distinct(StringComparer.Ordinal).Take(2).Count() == 1 ? group.First().Name : group.Key
			}).OrderBy(item => item.Name).ToList()
		};
	}

	private Task<bool> DefaultMyCasesAsync()
	{
		return Task.FromResult(false);
	}

	private async Task<IQueryable<AdmissionPipelineItemDto>> GetQueryAsync(AdmissionPipelineListInput input)
	{
		var applicationQuery = await access.GetScopedApplicationsAsync();
		applicationQuery = (await workspace.ProjectAsync(applicationQuery)).Where(row => !row.HasReachedDecision).Select(row => row.Application);
		var scopedOfferings = await access.GetScopedOfferingsAsync();
		var applicantQuery = await applicants.GetQueryableAsync();
		var inquiryQuery = await inquiries.GetQueryableAsync();
		var familyQuery = await families.GetQueryableAsync();
		var guardianQuery = await guardians.GetQueryableAsync();
		var cycleQuery = await cycles.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var workflowQuery = await workflows.GetQueryableAsync();
		var standardQuery = await standards.GetQueryableAsync();
		return from application in applicationQuery
			join offering in scopedOfferings on application.AdmissionOfferingId equals offering.Id
			join cycle in cycleQuery on application.AdmissionCycleId equals cycle.Id
			join applicant in applicantQuery on application.ApplicantId equals applicant.Id into linkedApplicants
			from applicant in linkedApplicants.DefaultIfEmpty()
			join inquiry in inquiryQuery on application.InquiryId equals (Guid?)inquiry.Id into linkedInquiries
			from inquiry in linkedInquiries.DefaultIfEmpty()
			join family in familyQuery on applicant.FamilyId equals family.Id into linkedFamilies
			from family in linkedFamilies.DefaultIfEmpty()
			join guardian in guardianQuery on family.PrimaryGuardianId equals (Guid?)guardian.Id into linkedGuardians
			from guardian in linkedGuardians.DefaultIfEmpty()
			join standard in standardQuery on offering.StandardId equals standard.Id into linkedStandards
			from standard in linkedStandards.DefaultIfEmpty()
			join stage in stageQuery on application.CurrentStageInstanceId equals (Guid?)stage.Id into currentStages
			from stage in currentStages.DefaultIfEmpty()
			where (application.InquiryId.HasValue ||
				(((offering.Track == AdmissionTrack.Rte && offering.EntryMode == AdmissionEntryMode.DirectRegistration) ||
				  ((offering.Track == AdmissionTrack.Management || offering.Track == AdmissionTrack.Staff) &&
				   (offering.EntryMode == AdmissionEntryMode.DirectRegistration || offering.EntryMode == AdmissionEntryMode.Both))) &&
				 workflowQuery.Any(workflow => workflow.ApplicationId == application.Id))) &&
				application.Status != AdmissionApplicationStatus.Withdrawn && application.Status != AdmissionApplicationStatus.Rejected &&
				application.Status != AdmissionApplicationStatus.OfferDeclined && application.Status != AdmissionApplicationStatus.Enrolled &&
				application.Status != AdmissionApplicationStatus.AdmissionFinalized &&
				!workflowQuery.Any(workflow => workflow.ApplicationId == application.Id && (workflow.Status == AdmissionWorkflowInstanceStatus.Cancelled || workflow.Status == AdmissionWorkflowInstanceStatus.Failed)) &&
				(!input.AcademicYearId.HasValue || cycle.AcademicYearId == input.AcademicYearId) &&
				(!input.BoardId.HasValue || offering.BoardId == input.BoardId) && (!input.MediumId.HasValue || offering.MediumId == input.MediumId) &&
				(!input.StandardId.HasValue || offering.StandardId == input.StandardId) &&
				(input.StandardIds.Count == 0 || Enumerable.Contains(input.StandardIds, offering.StandardId)) &&
				(!input.AdmissionCycleId.HasValue || cycle.Id == input.AdmissionCycleId) && (!input.AdmissionOfferingId.HasValue || offering.Id == input.AdmissionOfferingId)
			select new AdmissionPipelineItemDto
			{
				Id = application.Id, ApplicationNumber = application.ApplicationNumber, ApplicantId = application.ApplicantId,
				ApplicantName = applicant == null ? (inquiry == null ? "Applicant unavailable" : inquiry.ApplicantName) : applicant.FirstName + " " + (applicant.LastName ?? string.Empty), InquiryId = application.InquiryId, InquiryNumber = inquiry == null ? null : inquiry.InquiryNumber,
				SourceInquiryAvailable = inquiry != null,
				ContactPhone = inquiry == null ? (guardian == null ? null : guardian.Mobile) : inquiry.NormalizedPhone,
				ContactEmail = inquiry == null ? (guardian == null ? null : guardian.Email) : inquiry.Email, OfferingCode = offering.Code, StandardName = standard == null ? "Class unavailable" : standard.Name,
				Status = application.Status, CurrentStageInstanceId = application.CurrentStageInstanceId, CurrentStageCode = stage == null ? null : stage.Code,
				CurrentStageName = stage == null ? null : stage.Name, StageType = stage == null ? null : stage.StageType,
				CreationTime = application.CreationTime, EnteredAt = stage == null ? null : stage.EnteredAt, AssignedCounsellorId = application.AssignedCounsellorId
			};
	}

	private static IOrderedQueryable<AdmissionPipelineItemDto> Sort(IQueryable<AdmissionPipelineItemDto> query, string? sorting)
	{
		var parts = (string.IsNullOrWhiteSpace(sorting) ? "creationTime desc" : sorting).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		var sorted = parts[0].ToLowerInvariant() switch
		{
			"inquirynumber" => descending ? query.OrderByDescending(row => row.InquiryNumber) : query.OrderBy(row => row.InquiryNumber),
			"standardname" => descending ? query.OrderByDescending(row => row.StandardName) : query.OrderBy(row => row.StandardName),
			"contactphone" => descending ? query.OrderByDescending(row => row.ContactPhone) : query.OrderBy(row => row.ContactPhone),
			"contactemail" => descending ? query.OrderByDescending(row => row.ContactEmail) : query.OrderBy(row => row.ContactEmail),
			"status" => descending ? query.OrderByDescending(row => row.Status) : query.OrderBy(row => row.Status),
			"applicationnumber" => descending ? query.OrderByDescending(row => row.ApplicationNumber) : query.OrderBy(row => row.ApplicationNumber),
			"applicantname" => descending ? query.OrderByDescending(row => row.ApplicantName) : query.OrderBy(row => row.ApplicantName),
			"currentstagename" => descending ? query.OrderByDescending(row => row.CurrentStageName) : query.OrderBy(row => row.CurrentStageName),
			"enteredat" => descending ? query.OrderByDescending(row => row.EnteredAt) : query.OrderBy(row => row.EnteredAt),
			"creationtime" => descending ? query.OrderByDescending(row => row.CreationTime) : query.OrderBy(row => row.CreationTime),
			_ => query.OrderByDescending(row => row.CreationTime)
		};
		return sorted.ThenBy(row => row.Id);
	}
}
