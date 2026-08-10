using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMyWorkWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmApplicantProgressReadService progressRead) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmMyWorkDto> GetMyWorkAsync(
		Guid academicYearId,
		GetAdmissionCrmMyWorkInput input)
	{
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetMyWorkAsync));
		var now = Clock.Now;
		var today = now.ToUniversalTime().Date;
		var tomorrow = today.AddDays(1);
		var dueSoonEnd = today.AddDays(4);
		var tenantId = CurrentTenant.Id;
		var currentUser = await userRepository.GetAsync(userId);
		var currentUserDisplayName = AdmissionCrmOperationsFormat.GetUserDisplayName(currentUser);
		var leadQueryable = await leadRepository.GetQueryableAsync();
		var scopedLeads = leadQueryable.Where(lead =>
			lead.TenantId == tenantId &&
			lead.AcademicYearId == academicYearId);
		scopedLeads = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeads);
		var taskQueryable = await taskRepository.GetQueryableAsync();
		var assignedTasks = taskQueryable.Where(task =>
			task.TenantId == tenantId &&
			task.AcademicYearId == academicYearId &&
			task.Status == AdmissionCrmTaskStatus.Pending &&
			task.AssignedUserId == userId &&
			scopedLeads.Any(lead => lead.Id == task.LeadId));

		var assignedTotal = await AsyncExecuter.CountAsync(assignedTasks);
		var overdueCount = await AsyncExecuter.CountAsync(assignedTasks.Where(task => task.DueAt < today));
		var dueTodayCount = await AsyncExecuter.CountAsync(assignedTasks.Where(task =>
			task.DueAt >= today &&
			task.DueAt < tomorrow));

		IQueryable<AdmissionCrmFollowUpTask> filtered = assignedTasks;
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			var applicantNumber = AdmissionCrmOperationsFormat.ParseApplicantNumber(input.Search);
			filtered = filtered.Where(task =>
				task.Title.ToLower().Contains(search) ||
				(task.Notes != null && task.Notes.ToLower().Contains(search)) ||
				scopedLeads.Any(lead =>
					lead.Id == task.LeadId &&
					((applicantNumber.HasValue && lead.ApplicantNumber == applicantNumber.Value) ||
					 (lead.FirstName != null && lead.FirstName.ToLower().Contains(search)) ||
					 (lead.MiddleName != null && lead.MiddleName.ToLower().Contains(search)) ||
					 (lead.LastName != null && lead.LastName.ToLower().Contains(search)) ||
					 (lead.ParentGuardianName != null && lead.ParentGuardianName.ToLower().Contains(search)) ||
					 (lead.PrimaryContactNo != null && lead.PrimaryContactNo.Contains(search)) ||
					 (lead.Email != null && lead.Email.ToLower().Contains(search)))));
		}
		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var search = input.ApplicantSearch.Trim().ToLowerInvariant();
			var applicantNumber = AdmissionCrmOperationsFormat.ParseApplicantNumber(input.ApplicantSearch);
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				((applicantNumber.HasValue && lead.ApplicantNumber == applicantNumber.Value) ||
				 (lead.FirstName != null && lead.FirstName.ToLower().Contains(search)) ||
				 (lead.MiddleName != null && lead.MiddleName.ToLower().Contains(search)) ||
				 (lead.LastName != null && lead.LastName.ToLower().Contains(search)))));
		}
		if (!string.IsNullOrWhiteSpace(input.TargetClassSearch))
		{
			var search = input.TargetClassSearch.Trim().ToLowerInvariant();
			var standardQueryable = await standardRepository.GetQueryableAsync();
			var matchingStandardIds = await AsyncExecuter.ToListAsync(standardQueryable
				.Where(standard => standard.Name.ToLower().Contains(search))
				.Select(standard => standard.Id));
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				((lead.DesiredClassName != null && lead.DesiredClassName.ToLower().Contains(search)) ||
				 (lead.TargetStandardId.HasValue &&
				  matchingStandardIds.Contains(lead.TargetStandardId.Value)))));
		}
		if (!string.IsNullOrWhiteSpace(input.AssignedSearch) &&
			!currentUserDisplayName.Contains(input.AssignedSearch.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.WorkType.HasValue && input.WorkType != AdmissionCrmMyWorkType.Task)
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.QueueType.HasValue)
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.Stage.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.Stage == input.Stage.Value));
		}
		if (input.TargetStandardId.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.TargetStandardId == input.TargetStandardId.Value));
		}
		if (input.AcademicMediumId.HasValue)
		{
			await scopeAccess.EnsureCanAccessMediumAsync(
				input.AcademicMediumId.Value,
				nameof(GetMyWorkAsync));
			var stageQueryable = await mediumStageRepository.GetQueryableAsync();
			var stageIds = await AsyncExecuter.ToListAsync(stageQueryable
				.Where(x => x.EducationMediumId == input.AcademicMediumId.Value)
				.Select(x => x.Id));
			var standardQueryable = await standardRepository.GetQueryableAsync();
			var standardIds = await AsyncExecuter.ToListAsync(standardQueryable
				.Where(x => stageIds.Contains(x.EducationMediumStageId))
				.Select(x => x.Id));
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				(!lead.TargetStandardId.HasValue ||
				 standardIds.Contains(lead.TargetStandardId.Value))));
		}
		if (input.Priority.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.Priority == input.Priority.Value));
		}
		if (input.Urgency.HasValue)
		{
			filtered = input.Urgency.Value switch
			{
				AdmissionCrmWorkUrgency.Overdue =>
					filtered.Where(task => task.DueAt < today),
				AdmissionCrmWorkUrgency.DueToday =>
					filtered.Where(task => task.DueAt >= today && task.DueAt < tomorrow),
				AdmissionCrmWorkUrgency.DueSoon =>
					filtered.Where(task => task.DueAt >= tomorrow && task.DueAt < dueSoonEnd),
				_ =>
					filtered.Where(task => task.DueAt >= dueSoonEnd)
			};
		}

		var descending = input.Sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var sortField = input.Sorting?
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()?
			.ToLowerInvariant();
		filtered = sortField switch
		{
			"applicantnumber" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.ApplicantNumber)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.ApplicantNumber)
					.FirstOrDefault()),
			"applicantname" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.FirstName)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.FirstName)
					.FirstOrDefault()),
			"priority" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.Priority)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.Priority)
					.FirstOrDefault()),
			"title" => descending
				? filtered.OrderByDescending(task => task.Title)
				: filtered.OrderBy(task => task.Title),
			"dueat" => descending
				? filtered.OrderByDescending(task => task.DueAt)
				: filtered.OrderBy(task => task.DueAt),
			_ => filtered.OrderBy(task => task.DueAt)
				.ThenBy(task => task.CreationTime)
		};
		var totalCount = await AsyncExecuter.CountAsync(filtered);
		var tasks = await AsyncExecuter.ToListAsync(filtered
			.Skip(Math.Max(0, input.SkipCount))
			.Take(Math.Clamp(input.MaxResultCount, 1, 200)));
		// Keep this as a List so EF translates Enumerable.Contains. With newer runtimes,
		// a captured Guid[] can bind to the ReadOnlySpan overload and fail during query evaluation.
		var leadIds = tasks.Select(task => task.LeadId).Distinct().ToList();
		var leads = leadIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync(scopedLeads.Where(lead => leadIds.Contains(lead.Id)));
		var leadById = leads.ToDictionary(lead => lead.Id);
		var progressByLead = await progressRead.GetForLeadsAsync(leads);
		var targetStandardIds = leads
			.Where(lead => lead.TargetStandardId.HasValue)
			.Select(lead => lead.TargetStandardId!.Value)
			.Distinct()
			.ToList();
		var standards = targetStandardIds.Count == 0
			? []
			: await standardRepository.GetListAsync(standard => targetStandardIds.Contains(standard.Id));
		var standardNames = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
		var ownerUserIds = leads
			.Where(lead => lead.OwnerUserId.HasValue)
			.Select(lead => lead.OwnerUserId!.Value)
			.Distinct()
			.ToList();
		var owners = ownerUserIds.Count == 0
			? []
			: await userRepository.GetListAsync(user => ownerUserIds.Contains(user.Id));
		var ownerNames = owners.ToDictionary(user => user.Id, AdmissionCrmOperationsFormat.GetUserDisplayName);
		var workItems = new List<AdmissionCrmMyWorkItemDto>(tasks.Count);
		foreach (var task in tasks)
		{
			if (!leadById.TryGetValue(task.LeadId, out var lead))
			{
				continue;
			}

			var item = AdmissionCrmOperationsFormat.CreateTaskWorkItem(task, lead, userId, now);
			item.AssignedDisplayName = currentUserDisplayName;
			item.OwnerDisplayName = lead.OwnerUserId.HasValue
				? ownerNames.GetValueOrDefault(lead.OwnerUserId.Value)
				: null;
			item.TargetClassName = lead.DesiredClassName ??
				(lead.TargetStandardId.HasValue
					? standardNames.GetValueOrDefault(lead.TargetStandardId.Value)
					: null);
			if (progressByLead.TryGetValue(lead.Id, out var progress))
			{
				item.PipelineColumn = progress.PipelineColumn;
				item.CurrentStep = progress.CurrentStep;
				item.NextAction = progress.NextAction;
				item.NextActionCode = progress.NextActionCode;
			}

			workItems.Add(item);
		}

		return new AdmissionCrmMyWorkDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = now,
			AssignedTotal = assignedTotal,
			OverdueCount = overdueCount,
			DueTodayCount = dueTodayCount,
			UrgentBlockerCount = 0,
			TotalCount = totalCount,
			Items = workItems
		};
	}
}