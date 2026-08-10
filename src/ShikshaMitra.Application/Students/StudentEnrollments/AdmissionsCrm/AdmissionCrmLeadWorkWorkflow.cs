using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadWorkWorkflow(
	IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
	IRepository<AdmissionCrmApplicantChecklistProgress, Guid> checklistProgressRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmLeadAccess access,
	AdmissionCrmStaffEligibilityService staffEligibility,
	AdmissionCrmApplicantProgressReadService progressRead,
	AdmissionCrmLeadTimelineWriter timelineWriter) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmFollowUpTaskDto> CreateTaskAsync(
		Guid leadId,
		CreateAdmissionCrmFollowUpTaskDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(CreateTaskAsync));
		var assignedUserId = input.AssignedUserId ?? lead.OwnerUserId;
		await staffEligibility.EnsureTaskAssigneeEligibleAsync(assignedUserId);
		var task = new AdmissionCrmFollowUpTask(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.Id,
			lead.AcademicYearId,
			input.Title,
			input.DueAt,
			assignedUserId,
			input.Notes);

		await taskRepository.InsertAsync(task, autoSave: true);
		await timelineWriter.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.TaskCreated,
			"Follow-up task created",
			task.Title,
			relatedEntityId: task.Id);
		return AdmissionCrmLeadMapper.ToDto(task);
	}

	public async Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetTasksAsync(
		Guid leadId,
		PagedAndSortedResultRequestDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(GetTasksAsync));
		var queryable = await taskRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var tasks = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Status)
				.ThenBy(x => x.DueAt)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmFollowUpTaskDto>(
			totalCount,
			tasks.Select(AdmissionCrmLeadMapper.ToDto).ToList());
	}

	public async Task<IReadOnlyList<AdmissionCrmApplicantChecklistProgressDto>> GetApplicantChecklistAsync(
		Guid leadId)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(GetApplicantChecklistAsync));
		return await BuildApplicantChecklistProgressDtosAsync(lead);
	}

	public async Task<AdmissionCrmApplicantChecklistProgressDto> UpdateApplicantChecklistAsync(
		Guid leadId,
		string key,
		UpdateAdmissionCrmApplicantChecklistProgressDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(UpdateApplicantChecklistAsync));
		var configuration = await access.GetCycleConfigurationOrDefaultAsync(lead.AcademicYearId);
		var item = AdmissionCrmApplicantChecklistSerializer
			.DeserializeItems(configuration.ApplicantChecklistJson)
			.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
		if (item == null)
		{
			throw new BusinessException("AdmissionCrm:ApplicantChecklistItemNotConfigured")
				.WithData("LeadId", lead.Id)
				.WithData("ChecklistKey", key);
		}

		var progressQueryable = await checklistProgressRepository.GetQueryableAsync();
		var progress = await AsyncExecuter.FirstOrDefaultAsync(progressQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id &&
			x.ChecklistKey == item.Key));
		var now = new DateTimeOffset(Clock.Now);
		if (progress == null)
		{
			progress = new AdmissionCrmApplicantChecklistProgress(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				lead.Id,
				lead.AcademicYearId,
				item.Key,
				input.Status,
				input.Notes,
				now);
			await checklistProgressRepository.InsertAsync(progress, autoSave: false);
		}
		else
		{
			progress.UpdateStatus(input.Status, input.Notes, now);
			await checklistProgressRepository.UpdateAsync(progress, autoSave: false);
		}

		lead.RecordStaffNoteActivity();
		await leadRepository.UpdateAsync(lead, autoSave: false);
		await timelineWriter.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.ApplicantChecklistUpdated,
			"Applicant checklist updated",
			AdmissionCrmLeadMapper.BuildApplicantChecklistTimelineDetails(item, progress),
			relatedEntityId: progress.Id,
			autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return AdmissionCrmLeadMapper.ToApplicantChecklistProgressDto(item, progress, lead);
	}

	public async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetTimelineAsync(
		Guid leadId,
		PagedAndSortedResultRequestDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(GetTimelineAsync));
		var queryable = await timelineRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var events = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.OccurredAtUtc)
				.ThenByDescending(x => x.Id)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmTimelineEventDto>(
			totalCount,
			events.Select(AdmissionCrmLeadMapper.ToDto).ToList());
	}

	public async Task<AdmissionCrmTimelineEventDto> AddNoteAsync(Guid leadId, CreateAdmissionCrmLeadNoteDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(AddNoteAsync));
		var note = Check.NotNullOrWhiteSpace(
			input.Note,
			nameof(input.Note),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		lead.RecordStaffNoteActivity();
		await leadRepository.UpdateAsync(lead, autoSave: false);
		var timelineEvent = new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.Id,
			lead.AcademicYearId,
			AdmissionCrmTimelineEventType.NoteAdded,
			input.IsCallNote ? "Call note added" : "Note added",
			note,
			lead.Id,
			CurrentUser.Id,
			new DateTimeOffset(Clock.Now));
		await timelineRepository.InsertAsync(timelineEvent, autoSave: true);
		return AdmissionCrmLeadMapper.ToDto(timelineEvent);
	}

	public async Task<AdmissionCrmFollowUpTaskDto> CompleteTaskAsync(
		Guid taskId,
		CompleteAdmissionCrmFollowUpTaskDto input)
	{
		var task = await access.GetTaskForCurrentTenantAsync(taskId, nameof(CompleteTaskAsync));
		var currentUserId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		var canSupervise = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues);
		if (task.AssignedUserId != currentUserId && !canSupervise)
		{
			throw new AbpAuthorizationException("Only the assigned staff member or an operations supervisor can complete this task.");
		}
		task.Complete(Clock.Now, input.CompletionNotes);
		await taskRepository.UpdateAsync(task, autoSave: true);
		await timelineWriter.AddTimelineEventAsync(
			task.LeadId,
			task.AcademicYearId,
			AdmissionCrmTimelineEventType.TaskCompleted,
			"Follow-up task completed",
			input.CompletionNotes ?? task.Title,
			relatedEntityId: task.Id);
		return AdmissionCrmLeadMapper.ToDto(task);
	}

	private async Task<IReadOnlyList<AdmissionCrmApplicantChecklistProgressDto>> BuildApplicantChecklistProgressDtosAsync(
		AdmissionInquiryLead lead)
	{
		var configuration = await access.GetCycleConfigurationOrDefaultAsync(lead.AcademicYearId);
		var checklistItems = AdmissionCrmApplicantChecklistSerializer
			.DeserializeItems(configuration.ApplicantChecklistJson)
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Label)
			.ToList();
		if (checklistItems.Count == 0)
		{
			return Array.Empty<AdmissionCrmApplicantChecklistProgressDto>();
		}

		var progressQueryable = await checklistProgressRepository.GetQueryableAsync();
		var progressItems = await AsyncExecuter.ToListAsync(progressQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id));
		var progressByKey = progressItems
			.GroupBy(x => x.ChecklistKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.StatusChangedAt).First(), StringComparer.OrdinalIgnoreCase);
		return checklistItems
			.Select(item =>
			{
				progressByKey.TryGetValue(item.Key, out var progress);
				return AdmissionCrmLeadMapper.ToApplicantChecklistProgressDto(item, progress, lead);
			})
			.ToList();
	}
}