using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionWorkflowGovernanceService(
	IRepository<AdmissionWorkflowGovernanceSetting, Guid> settings,
	IRepository<AdmissionWorkflowGovernanceHistory, Guid> histories,
	IRepository<AdmissionWorkflowNotification, Guid> notifications,
	IRepository<IdentityUser, Guid> users,
	IRepository<IdentityRole, Guid> roles,
	IRepository<PermissionGrant, Guid> permissionGrants,
	IEmailSender emailSender,
	ILogger<AdmissionWorkflowGovernanceService> logger) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowGovernanceSettingDto> GetSettingsAsync()
	{
		var setting = await FindSettingsAsync();
		return setting == null ? Defaults() : Map(setting);
	}

	public async Task<AdmissionWorkflowGovernanceSettingDto> UpdateSettingsAsync(UpdateAdmissionWorkflowGovernanceSettingDto input)
	{
		var setting = await FindSettingsAsync();
		if (setting == null)
		{
			setting = new AdmissionWorkflowGovernanceSetting(GuidGenerator.Create(), CurrentTenant.Id);
			setting.Update(input.MakerCheckerEnabled, input.InAppNotificationsEnabled, input.EmailNotificationsEnabled);
			await settings.InsertAsync(setting, autoSave: true);
		}
		else
		{
			setting.Update(input.MakerCheckerEnabled, input.InAppNotificationsEnabled, input.EmailNotificationsEnabled);
			await settings.UpdateAsync(setting, autoSave: true);
		}
		return Map(setting);
	}

	public async Task<List<AdmissionWorkflowApproverDto>> GetApproversAsync(string? filter)
	{
		if (filter?.Length > 128)
			throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable, "Search text is too long.");
		var eligibleUserIds = await GetApproverUserIdsAsync();
		if (eligibleUserIds.Count == 0) return [];
		var currentUserId = CurrentUser.Id;
		var query = (await users.GetQueryableAsync()).Where(user =>
			user.IsActive && eligibleUserIds.Contains(user.Id) &&
			(!currentUserId.HasValue || user.Id != currentUserId.Value));
		if (!string.IsNullOrWhiteSpace(filter))
			query = query.Where(user => user.UserName.Contains(filter) || user.Name.Contains(filter) || user.Surname.Contains(filter));
		return (await AsyncExecuter.ToListAsync(query.OrderBy(user => user.UserName).Take(50)))
			.Select(user => new AdmissionWorkflowApproverDto { Id = user.Id, UserName = user.UserName, Name = Display(user) })
			.ToList();
	}

	public async Task EnsureApproverAsync(Guid approverUserId)
	{
		var user = await users.FindAsync(approverUserId, includeDetails: true);
		if (user?.IsActive != true || !(await GetApproverUserIdsAsync()).Contains(approverUserId))
			throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable,
				"Select an active user who has workflow approval access for this school.");
	}

	public async Task RecordAsync(AdmissionWorkflowDefinition workflow, AdmissionWorkflowGovernanceAction action,
		AdmissionWorkflowStatus? fromStatus, Guid? actorUserId, string? reason = null,
		Guid? assignedApproverId = null, DateTime? effectiveAt = null)
	{
		await histories.InsertAsync(new AdmissionWorkflowGovernanceHistory(
			GuidGenerator.Create(), CurrentTenant.Id, workflow.Id, action, fromStatus, workflow.Status,
			actorUserId, reason, Clock.Now, assignedApproverId, effectiveAt), autoSave: false);
	}

	public async Task NotifyAsync(AdmissionWorkflowDefinition workflow, Guid recipientUserId,
		AdmissionWorkflowNotificationType type, string title, string message)
	{
		var setting = await GetSettingsAsync();
		if (setting.InAppNotificationsEnabled)
			await notifications.InsertAsync(new AdmissionWorkflowNotification(GuidGenerator.Create(), CurrentTenant.Id,
				workflow.Id, recipientUserId, type, title, message), autoSave: false);

		if (!setting.EmailNotificationsEnabled) return;
		var recipient = await users.FindAsync(recipientUserId);
		if (string.IsNullOrWhiteSpace(recipient?.Email)) return;
		try
		{
			await emailSender.SendAsync(recipient.Email, title,
				$"<p>{HtmlEncoder.Default.Encode(message)}</p>", isBodyHtml: true);
		}
		catch (Exception exception)
		{
			logger.LogError(exception,
				"Workflow governance email delivery failed for workflow {WorkflowId} and recipient {RecipientUserId}.",
				workflow.Id, recipientUserId);
		}
	}

	public async Task<List<AdmissionWorkflowGovernanceHistoryDto>> GetHistoryAsync(Guid workflowId)
	{
		var rows = (await histories.GetListAsync(item => item.WorkflowDefinitionId == workflowId))
			.OrderByDescending(item => item.OccurredAt).ToList();
		var userIds = rows.SelectMany(item => new[] { item.ActorUserId, item.AssignedApproverId })
			.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
		var people = userIds.Count == 0
			? new Dictionary<Guid, IdentityUser>()
			: (await users.GetListAsync(user => userIds.Contains(user.Id))).ToDictionary(user => user.Id);
		return rows.Select(item => new AdmissionWorkflowGovernanceHistoryDto
		{
			Id = item.Id,
			Action = item.Action,
			FromStatus = item.FromStatus,
			ToStatus = item.ToStatus,
			ActorUserId = item.ActorUserId,
			ActorName = Name(people, item.ActorUserId),
			AssignedApproverId = item.AssignedApproverId,
			AssignedApproverName = Name(people, item.AssignedApproverId),
			Reason = item.Reason,
			EffectiveAt = item.EffectiveAt,
			OccurredAt = item.OccurredAt
		}).ToList();
	}

	public async Task<List<AdmissionWorkflowNotificationDto>> GetNotificationsAsync(bool unreadOnly)
	{
		var currentUserId = CurrentUser.Id ?? throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		var query = (await notifications.GetQueryableAsync()).Where(item => item.RecipientUserId == currentUserId);
		if (unreadOnly) query = query.Where(item => !item.IsRead);
		return (await AsyncExecuter.ToListAsync(query.OrderByDescending(item => item.CreationTime).Take(50)))
			.Select(item => new AdmissionWorkflowNotificationDto
			{
				Id = item.Id,
				WorkflowDefinitionId = item.WorkflowDefinitionId,
				Type = item.Type,
				Title = item.Title,
				Message = item.Message,
				IsRead = item.IsRead,
				ReadAt = item.ReadAt,
				CreationTime = item.CreationTime
			}).ToList();
	}

	public async Task MarkNotificationReadAsync(Guid notificationId)
	{
		var currentUserId = CurrentUser.Id ?? throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		var notification = await notifications.FindAsync(item => item.Id == notificationId && item.RecipientUserId == currentUserId)
			?? throw new EntityNotFoundException(typeof(AdmissionWorkflowNotification), notificationId);
		notification.MarkRead(Clock.Now);
		await notifications.UpdateAsync(notification, autoSave: true);
	}

	private async Task<AdmissionWorkflowGovernanceSetting?> FindSettingsAsync() =>
		await settings.FindAsync(item => item.TenantId == CurrentTenant.Id);

	private async Task<HashSet<Guid>> GetApproverUserIdsAsync()
	{
		var grants = await permissionGrants.GetListAsync(item =>
			item.Name == ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve &&
			(item.ProviderName == "U" || item.ProviderName == "R"));
		var userIds = grants
			.Where(item => item.ProviderName == "U")
			.Select(item => Guid.TryParse(item.ProviderKey, out var id) ? id : Guid.Empty)
			.Where(id => id != Guid.Empty)
			.ToHashSet();
		var roleNames = grants
			.Where(item => item.ProviderName == "R" && !string.IsNullOrWhiteSpace(item.ProviderKey))
			.Select(item => item.ProviderKey!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (roleNames.Count == 0) return userIds;
		var roleIds = (await roles.GetListAsync(role => roleNames.Contains(role.Name)))
			.Select(role => role.Id)
			.ToList();
		if (roleIds.Count == 0) return userIds;
		var query = await users.GetQueryableAsync();
		var roleUserIds = await AsyncExecuter.ToListAsync(query
			.Where(user => user.Roles.Any(role => roleIds.Contains(role.RoleId)))
			.Select(user => user.Id));
		foreach (var roleUserId in roleUserIds) userIds.Add(roleUserId);
		return userIds;
	}

	private static AdmissionWorkflowGovernanceSettingDto Defaults() => new()
	{
		MakerCheckerEnabled = false,
		InAppNotificationsEnabled = true,
		EmailNotificationsEnabled = true
	};

	private static AdmissionWorkflowGovernanceSettingDto Map(AdmissionWorkflowGovernanceSetting setting) => new()
	{
		MakerCheckerEnabled = setting.MakerCheckerEnabled,
		InAppNotificationsEnabled = setting.InAppNotificationsEnabled,
		EmailNotificationsEnabled = setting.EmailNotificationsEnabled
	};

	private static string Display(IdentityUser user) => string.IsNullOrWhiteSpace(user.Name) && string.IsNullOrWhiteSpace(user.Surname)
		? user.UserName : $"{user.Name} {user.Surname}".Trim();

	private static string? Name(IReadOnlyDictionary<Guid, IdentityUser> usersById, Guid? id) =>
		id.HasValue && usersById.TryGetValue(id.Value, out var user) ? Display(user) : null;
}
