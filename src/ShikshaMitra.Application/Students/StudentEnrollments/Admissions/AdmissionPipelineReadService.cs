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
public class AdmissionPipelineReadService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionInquiry, Guid> inquiries,
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
	IUserScopeAccessService userScopes) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionPipelineItemDto>> GetListAsync(AdmissionPipelineListInput input)
	{
		var query = await GetQueryAsync(input);
		var myCases = input.MyCases ?? await DefaultMyCasesAsync();
		if (myCases) query = query.Where(row => CurrentUser.Id.HasValue && row.AssignedCounsellorId == CurrentUser.Id);
		if (input.AssignedCounsellorId.HasValue) query = query.Where(row => row.AssignedCounsellorId == input.AssignedCounsellorId);
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
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(Sort(query, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ownerNames = await people.GetNamesAsync(rows.Where(row => row.AssignedCounsellorId.HasValue).Select(row => row.AssignedCounsellorId!.Value));
		var inquiryIds = rows.Select(row => row.InquiryId).ToArray();
		var pending = await followUps.GetListAsync(item => Enumerable.Contains(inquiryIds, item.InquiryId) && item.Status == AdmissionInquiryFollowUpStatus.Scheduled);
		var stageIds = rows.Where(row => row.CurrentStageInstanceId.HasValue).Select(row => row.CurrentStageInstanceId!.Value).ToArray();
		var stageRows = await stages.GetListAsync(item => Enumerable.Contains(stageIds, item.Id));
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
			var open = pendingByInquiry[row.InquiryId].ToList();
			row.OpenFollowUpCount = open.Count;
			row.NextFollowUpAt = open.Count > 0 ? AdmissionOfferUtcTime.Normalize(open.Min(item => item.ScheduledAt)) : null;
			row.HasOverdueFollowUp = row.NextFollowUpAt < utcNow;
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
		var ids = await AsyncExecuter.ToListAsync(query.Where(row => row.AssignedCounsellorId.HasValue).Select(row => row.AssignedCounsellorId!.Value).Distinct());
		var names = await people.GetNamesAsync(ids);
		var stageOptions = await AsyncExecuter.ToListAsync(query.Where(row => row.CurrentStageCode != null).Select(row => new AdmissionPipelineStageOptionDto { Code = row.CurrentStageCode!, Name = row.CurrentStageName! }).Distinct());
		return new()
		{
			CurrentUserId = CurrentUser.Id, DefaultMyCases = await DefaultMyCasesAsync(),
			Counsellors = names.OrderBy(item => item.Value).Select(item => new AdmissionInquiryCounsellorDto { Id = item.Key, Name = item.Value, UserName = item.Value }).ToList(),
			Stages = stageOptions.GroupBy(item => item.Code, StringComparer.Ordinal).Select(group => new AdmissionPipelineStageOptionDto
			{
				Code = group.Key,
				Name = group.Select(item => item.Name).Distinct(StringComparer.Ordinal).Take(2).Count() == 1 ? group.First().Name : group.Key
			}).OrderBy(item => item.Name).ToList()
		};
	}

	private async Task<bool> DefaultMyCasesAsync()
	{
		if (!CurrentUser.Id.HasValue) return true;
		if ((await userScopes.GetCurrentUserScopeAsync()).IsAdmin) return false;
		var offerings = await access.GetScopedOfferingsAsync();
		var profileQuery = await profiles.GetQueryableAsync();
		var configurations = await AsyncExecuter.ToListAsync(profileQuery.Where(profile => profile.Kind == AdmissionIntakeProfileKind.CounsellorAssignment && offerings.Any(offering => offering.CounsellorAssignmentProfileId == profile.Id)).Select(profile => profile.ConfigurationJson));
		foreach (var json in configurations)
		{
			try
			{
				var rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
				if (rules == null) continue;
				rules.Validate();
				if (rules.FallbackManagerUserId == CurrentUser.Id) return false;
			}
			catch (Exception exception) when (exception is JsonException or ArgumentException) { /* Invalid configuration never widens the default queue. */ }
		}
		return true;
	}

	private async Task<IQueryable<AdmissionPipelineItemDto>> GetQueryAsync(AdmissionPipelineListInput input)
	{
		var applicationQuery = await applications.GetQueryableAsync();
		applicationQuery = (await workspace.ProjectAsync(applicationQuery)).Where(row => !row.HasReachedDecision).Select(row => row.Application);
		var scopedOfferings = await access.GetScopedOfferingsAsync();
		var applicantQuery = await applicants.GetQueryableAsync();
		var inquiryQuery = await inquiries.GetQueryableAsync();
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
			join standard in standardQuery on offering.StandardId equals standard.Id into linkedStandards
			from standard in linkedStandards.DefaultIfEmpty()
			join stage in stageQuery on application.CurrentStageInstanceId equals (Guid?)stage.Id into currentStages
			from stage in currentStages.DefaultIfEmpty()
			where application.InquiryId.HasValue && application.Status != AdmissionApplicationStatus.Withdrawn && application.Status != AdmissionApplicationStatus.Rejected &&
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
				ApplicantName = applicant == null ? (inquiry == null ? "Applicant unavailable" : inquiry.ApplicantName) : applicant.FirstName + " " + (applicant.LastName ?? string.Empty), InquiryId = application.InquiryId!.Value, InquiryNumber = inquiry == null ? null : inquiry.InquiryNumber,
				SourceInquiryAvailable = inquiry != null,
				ContactPhone = inquiry == null ? null : inquiry.NormalizedPhone, ContactEmail = inquiry == null ? null : inquiry.Email, OfferingCode = offering.Code, StandardName = standard == null ? "Class unavailable" : standard.Name,
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
