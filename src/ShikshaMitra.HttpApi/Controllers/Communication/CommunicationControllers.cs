using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Communication;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Communication;

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/campaigns")]
public class CommunicationCampaignController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationCampaignDto>> GetListAsync([FromQuery] GetCommunicationCampaignListRequestDto input)
	{
		return appService.GetCampaignsAsync(input);
	}

	[HttpGet("{id}")]
	public Task<CommunicationCampaignDto> GetAsync(Guid id)
	{
		return appService.GetCampaignAsync(id);
	}

	[HttpPost]
	public Task<CommunicationCampaignDto> CreateAsync([FromBody] CreateUpdateCommunicationCampaignDto input)
	{
		return appService.CreateCampaignAsync(input);
	}

	[HttpPut("{id}")]
	public Task<CommunicationCampaignDto> UpdateAsync(Guid id, [FromBody] CreateUpdateCommunicationCampaignDto input)
	{
		return appService.UpdateCampaignAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteCampaignAsync(id);
	}

	[HttpPost("audience/preview")]
	public Task<CommunicationAudiencePreviewDto> PreviewAudienceAsync([FromBody] CommunicationAudienceDefinitionDto input)
	{
		return appService.PreviewAudienceAsync(input);
	}

	[HttpPut("{id}/submit-for-approval")]
	public Task<CommunicationCampaignDto> SubmitForApprovalAsync(Guid id)
	{
		return appService.SubmitForApprovalAsync(id);
	}

	[HttpPut("{id}/approve")]
	public Task<CommunicationCampaignDto> ApproveAsync(Guid id, [FromBody] CommunicationApprovalDto input)
	{
		return appService.ApproveCampaignAsync(id, input);
	}

	[HttpPut("{id}/reject")]
	public Task<CommunicationCampaignDto> RejectAsync(Guid id, [FromBody] CommunicationApprovalDto input)
	{
		return appService.RejectCampaignAsync(id, input);
	}

	[HttpPut("{id}/publish")]
	public Task<CommunicationCampaignDto> PublishAsync(Guid id)
	{
		return appService.PublishCampaignAsync(id);
	}

	[HttpPut("{id}/cancel")]
	public Task<CommunicationCampaignDto> CancelAsync(Guid id)
	{
		return appService.CancelCampaignAsync(id);
	}

	[HttpPost("{id}/clone")]
	public Task<CommunicationCampaignDto> CloneAsync(Guid id)
	{
		return appService.CloneCampaignAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/templates")]
public class CommunicationTemplateController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationTemplateDto>> GetListAsync([FromQuery] GetCommunicationTemplateListRequestDto input)
	{
		return appService.GetTemplatesAsync(input);
	}

	[HttpGet("{id}")]
	public Task<CommunicationTemplateDto> GetAsync(Guid id)
	{
		return appService.GetTemplateAsync(id);
	}

	[HttpPost]
	public Task<CommunicationTemplateDto> CreateAsync([FromBody] CreateUpdateCommunicationTemplateDto input)
	{
		return appService.CreateTemplateAsync(input);
	}

	[HttpPut("{id}")]
	public Task<CommunicationTemplateDto> UpdateAsync(Guid id, [FromBody] CreateUpdateCommunicationTemplateDto input)
	{
		return appService.UpdateTemplateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteTemplateAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/provider-settings")]
public class CommunicationProviderSettingsController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<ChannelProviderSettingDto>> GetListAsync([FromQuery] GetChannelProviderSettingListRequestDto input)
	{
		return appService.GetProviderSettingsAsync(input);
	}

	[HttpGet("{id}")]
	public Task<ChannelProviderSettingDto> GetAsync(Guid id)
	{
		return appService.GetProviderSettingAsync(id);
	}

	[HttpPost]
	public Task<ChannelProviderSettingDto> CreateAsync([FromBody] CreateUpdateChannelProviderSettingDto input)
	{
		return appService.CreateProviderSettingAsync(input);
	}

	[HttpPut("{id}")]
	public Task<ChannelProviderSettingDto> UpdateAsync(Guid id, [FromBody] CreateUpdateChannelProviderSettingDto input)
	{
		return appService.UpdateProviderSettingAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteProviderSettingAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/consent-records")]
public class CommunicationConsentController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<ConsentRecordDto>> GetListAsync([FromQuery] GetConsentRecordListRequestDto input)
	{
		return appService.GetConsentRecordsAsync(input);
	}

	[HttpPost]
	public Task<ConsentRecordDto> UpsertAsync([FromBody] CreateUpdateConsentRecordDto input)
	{
		return appService.UpsertConsentRecordAsync(input);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/approval-rules")]
public class CommunicationApprovalRulesController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationApprovalRuleDto>> GetListAsync([FromQuery] GetCommunicationApprovalRuleListRequestDto input)
	{
		return appService.GetApprovalRulesAsync(input);
	}

	[HttpGet("{id}")]
	public Task<CommunicationApprovalRuleDto> GetAsync(Guid id)
	{
		return appService.GetApprovalRuleAsync(id);
	}

	[HttpPost]
	public Task<CommunicationApprovalRuleDto> CreateAsync([FromBody] CreateUpdateCommunicationApprovalRuleDto input)
	{
		return appService.CreateApprovalRuleAsync(input);
	}

	[HttpPut("{id}")]
	public Task<CommunicationApprovalRuleDto> UpdateAsync(Guid id, [FromBody] CreateUpdateCommunicationApprovalRuleDto input)
	{
		return appService.UpdateApprovalRuleAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteApprovalRuleAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/delivery-attempts")]
public class CommunicationDeliveryController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<DeliveryAttemptDto>> GetListAsync([FromQuery] GetDeliveryAttemptListRequestDto input)
	{
		return appService.GetDeliveryAttemptsAsync(input);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/replies")]
public class CommunicationRepliesController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationReplyDto>> GetListAsync([FromQuery] GetCommunicationReplyListRequestDto input)
	{
		return appService.GetRepliesAsync(input);
	}

	[HttpPost]
	public Task<CommunicationReplyDto> CaptureAsync([FromBody] CaptureCommunicationReplyDto input)
	{
		return appService.CaptureReplyAsync(input);
	}

	[HttpPut("{id}/assign")]
	public Task<CommunicationReplyDto> AssignAsync(Guid id, [FromBody] AssignCommunicationReplyDto input)
	{
		return appService.AssignReplyAsync(id, input);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/usage-ledger")]
public class CommunicationUsageController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<UsageLedgerEntryDto>> GetListAsync([FromQuery] GetUsageLedgerListRequestDto input)
	{
		return appService.GetUsageLedgerAsync(input);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/dashboard")]
public class CommunicationDashboardController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<CommunicationDashboardDto> GetAsync()
	{
		return appService.GetDashboardAsync();
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/notifications")]
public class NotificationsController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationCampaignDto>> GetListAsync([FromQuery] LegacyCommunicationListRequest input)
	{
		return appService.GetCampaignsAsync(input.ToCampaignRequest("Notification"));
	}

	[HttpPost]
	public Task<CommunicationCampaignDto> CreateAsync([FromQuery] Guid? academicMediumId, [FromBody] LegacyCampaignDraftDto input)
	{
		return appService.CreateCampaignAsync(input.ToCampaignInput(academicMediumId, "Notification"));
	}

	[HttpGet("{id}")]
	public Task<CommunicationCampaignDto> GetAsync(Guid id)
	{
		return appService.GetCampaignAsync(id);
	}

	[HttpGet("{id}/step-one")]
	public async Task<object> GetStepOneAsync(Guid id)
	{
		var campaign = await appService.GetCampaignAsync(id);
		return new
		{
			campaign.AudienceDefinition,
			members = campaign.AudienceDefinition.ExplicitRecipients,
			campaign.RecipientCount,
			campaign.EligibleRecipientCount,
			campaign.BlockedRecipientCount
		};
	}

	[HttpGet("{id}/step-two")]
	public async Task<object> GetStepTwoAsync(Guid id)
	{
		var campaign = await appService.GetCampaignAsync(id);
		return new
		{
			channels = campaign.Channels.Select(LegacyCommunicationMapper.ToChannelLabel).ToList(),
			fallbackOrder = campaign.FallbackOrder.Select(LegacyCommunicationMapper.ToChannelLabel).ToList()
		};
	}

	[HttpGet("{id}/step-three")]
	public async Task<object> GetStepThreeAsync(Guid id)
	{
		var campaign = await appService.GetCampaignAsync(id);
		return new
		{
			messageDetails = new
			{
				subject = campaign.Subject,
				message = campaign.Body,
				selectedTemplate = new
				{
					id = campaign.TemplateId,
					subject = campaign.Subject,
					message = campaign.Body
				}
			}
		};
	}

	[HttpPut("{id}/channels")]
	public async Task<CommunicationCampaignDto> UpdateChannelsAsync(Guid id, [FromBody] LegacyChannelUpdateDto input)
	{
		var campaign = await appService.GetCampaignAsync(id);
		var update = LegacyCommunicationMapper.ToUpdateInput(campaign);
		update.Channels = LegacyCommunicationMapper.ParseChannels(input.Channels);
		update.FallbackOrder = update.Channels;
		return await appService.UpdateCampaignAsync(id, update);
	}

	[HttpPut("{id}/message")]
	public async Task<CommunicationCampaignDto> ComposeMessageAndPublishAsync(Guid id, [FromBody] LegacyMessageUpdateDto input)
	{
		var campaign = await appService.GetCampaignAsync(id);
		var update = LegacyCommunicationMapper.ToUpdateInput(campaign);
		update.Subject = input.Subject ?? input.SelectedTemplate?.Subject ?? campaign.Subject;
		update.Body = input.Message ?? input.SelectedTemplate?.Message ?? campaign.Body;
		update.TemplateId = input.SelectedTemplate?.ParsedId ?? campaign.TemplateId;

		await appService.UpdateCampaignAsync(id, update);
		return await appService.PublishCampaignAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/notification-templates")]
public class NotificationTemplatesController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationTemplateDto>> GetListAsync([FromQuery] Guid? academicMediumId)
	{
		return appService.GetTemplatesAsync(new GetCommunicationTemplateListRequestDto
		{
			AcademicMediumId = academicMediumId,
			MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
		});
	}

	[HttpGet("{id}")]
	public Task<CommunicationTemplateDto> GetAsync(Guid id)
	{
		return appService.GetTemplateAsync(id);
	}

	[HttpPost]
	public Task<CommunicationTemplateDto> CreateAsync([FromQuery] Guid? academicMediumId, [FromBody] CreateUpdateCommunicationTemplateDto input)
	{
		input.AcademicMediumId = academicMediumId ?? input.AcademicMediumId;
		return appService.CreateTemplateAsync(input);
	}

	[HttpPut("{id}")]
	public Task<CommunicationTemplateDto> UpdateAsync(Guid id, [FromQuery] Guid? academicMediumId, [FromBody] CreateUpdateCommunicationTemplateDto input)
	{
		input.AcademicMediumId = academicMediumId ?? input.AcademicMediumId;
		return appService.UpdateTemplateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteTemplateAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/announcements")]
public class AnnouncementsController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CommunicationCampaignDto>> GetListAsync([FromQuery] LegacyCommunicationListRequest input)
	{
		return appService.GetCampaignsAsync(input.ToCampaignRequest("Announcement"));
	}

	[HttpPost]
	public Task<CommunicationCampaignDto> CreateAsync([FromQuery] Guid? academicMediumId, [FromBody] LegacyCampaignDraftDto input)
	{
		return appService.CreateCampaignAsync(input.ToCampaignInput(academicMediumId, "Announcement"));
	}

	[HttpPut]
	public async Task<CommunicationCampaignDto> UpdateAsync([FromQuery] Guid? academicMediumId, [FromBody] LegacyCampaignDraftDto input)
	{
		if (!input.Id.HasValue)
		{
			throw new BusinessException("Communication:AnnouncementIdRequired");
		}

		return await appService.UpdateCampaignAsync(input.Id.Value, input.ToCampaignInput(academicMediumId, "Announcement"));
	}

	[HttpGet("{id}")]
	public Task<CommunicationCampaignDto> GetAsync(Guid id)
	{
		return appService.GetCampaignAsync(id);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return appService.DeleteCampaignAsync(id);
	}

	[HttpPut("{id}/publish")]
	public Task<CommunicationCampaignDto> PublishAsync(Guid id)
	{
		return appService.PublishCampaignAsync(id);
	}
}

[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
public class CommunicationCompatibilityOptionsController(ICommunicationAppService appService) : AbpControllerBase
{
	[HttpGet("api/deliveryChannelTypeData")]
	public Task<List<CommunicationOptionDto>> GetDeliveryChannelTypeDataAsync()
	{
		return appService.GetChannelOptionsAsync();
	}

	[HttpGet("api/importanceLevelData")]
	public Task<List<CommunicationOptionDto>> GetImportanceLevelDataAsync()
	{
		return appService.GetImportanceLevelOptionsAsync();
	}

	[HttpGet("api/notificationHistoryData")]
	public Task<PagedResultDto<DeliveryAttemptDto>> GetNotificationHistoryDataAsync()
	{
		return appService.GetDeliveryAttemptsAsync(new GetDeliveryAttemptListRequestDto
		{
			MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
		});
	}

	[HttpGet("api/sentNotificationData")]
	public Task<PagedResultDto<CommunicationCampaignDto>> GetSentNotificationDataAsync()
	{
		return appService.GetCampaignsAsync(new GetCommunicationCampaignListRequestDto
		{
			CampaignType = "Notification",
			MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
		});
	}

	[HttpGet("api/classWiseAnnouncementData")]
	public Task<PagedResultDto<CommunicationCampaignDto>> GetClassWiseAnnouncementDataAsync()
	{
		return appService.GetCampaignsAsync(new GetCommunicationCampaignListRequestDto
		{
			CampaignType = "Announcement",
			MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
		});
	}

	[HttpGet("api/meetingData")]
	public Task<List<object>> GetMeetingDataAsync()
	{
		return Task.FromResult(new List<object>());
	}

	[HttpGet("api/meetingTypeData")]
	public Task<List<object>> GetMeetingTypeDataAsync()
	{
		return Task.FromResult(new List<object>());
	}

	[HttpGet("api/surveyTypeData")]
	public Task<List<object>> GetSurveyTypeDataAsync()
	{
		return Task.FromResult(new List<object>());
	}
}

public class LegacyCommunicationListRequest
{
	public Guid? AcademicMediumId { get; set; }
	public string? Search { get; set; }
	public string? Status { get; set; }
	public int? Page { get; set; }
	public int? PageSize { get; set; }

	public GetCommunicationCampaignListRequestDto ToCampaignRequest(string campaignType)
	{
		var pageSize = PageSize.GetValueOrDefault(10);
		var page = Math.Max(1, Page.GetValueOrDefault(1));

		var request = new GetCommunicationCampaignListRequestDto
		{
			AcademicMediumId = AcademicMediumId,
			CampaignType = campaignType,
			Search = Search,
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		};

		if (!Status.IsNullOrWhiteSpace() && Enum.TryParse<CommunicationCampaignStatus>(Status, true, out var status))
		{
			request.Status = status;
		}

		return request;
	}
}

public class LegacyCampaignDraftDto
{
	public Guid? Id { get; set; }
	public string? Title { get; set; }
	public string? Subject { get; set; }
	public string? Description { get; set; }
	public string? Body { get; set; }
	public string? Message { get; set; }
	public string? LanguageCode { get; set; }
	public DateTimeOffset? StartDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }
	public List<string> Channels { get; set; } = [];
	public List<Guid> GroupIds { get; set; } = [];
	public List<Guid> UserIds { get; set; } = [];
	public List<Guid> MemberIds { get; set; } = [];
	public List<LegacyMemberDto> Members { get; set; } = [];
	public LegacyTemplateDto? SelectedTemplate { get; set; }
	public List<LegacyAnnouncementTypeDto> Type { get; set; } = [];

	[JsonPropertyName("notification_preferences")]
	public List<string> NotificationPreferences { get; set; } = [];

	[JsonPropertyName("importance_level")]
	public object? ImportanceLevel { get; set; }

	public CreateUpdateCommunicationCampaignDto ToCampaignInput(Guid? academicMediumId, string campaignType)
	{
		var body = Body ?? Message ?? Description ?? SelectedTemplate?.Message ?? string.Empty;
		var channels = LegacyCommunicationMapper.ParseChannels(Channels.Count > 0 ? Channels : NotificationPreferences);
		if (channels.Count == 0 && campaignType == "Announcement")
		{
			channels.Add(CommunicationChannelType.InApp);
		}

		return new CreateUpdateCommunicationCampaignDto
		{
			AcademicMediumId = academicMediumId,
			SourceModule = CommunicationSourceModule.Manual,
			CampaignType = campaignType,
			Title = Title ?? Subject ?? SelectedTemplate?.TemplateName ?? $"{campaignType} draft",
			Subject = Subject ?? Title ?? SelectedTemplate?.Subject,
			Body = body,
			ImportanceLevel = LegacyCommunicationMapper.ParseImportanceLevel(ImportanceLevel),
			TemplateId = SelectedTemplate?.ParsedId,
			AnnouncementTypeId = Type.FirstOrDefault(x => x.Id.HasValue)?.Id,
			AudienceDefinition = new CommunicationAudienceDefinitionDto
			{
				GroupIds = GroupIds,
				UserIds = UserIds.Concat(MemberIds).Distinct().ToList(),
				ExplicitRecipients = Members.Select(x => x.ToRecipient()).ToList(),
				ParentsFirst = true
			},
			Channels = channels,
			FallbackOrder = channels,
			LanguageCode = LanguageCode ?? "en",
			ScheduledAt = StartDate
		};
	}
}

public class LegacyMemberDto
{
	public Guid? Id { get; set; }
	public string? Name { get; set; }
	public string? Email { get; set; }
	public string? ContactNumber { get; set; }
	public string? MobileNumber { get; set; }
	public string? Type { get; set; }

	public CommunicationExplicitRecipientDto ToRecipient()
	{
		return new CommunicationExplicitRecipientDto
		{
			RecipientType = Type ?? "ParentGuardian",
			RecipientId = Id,
			DisplayName = Name ?? Email ?? ContactNumber ?? MobileNumber ?? "Recipient",
			Email = Email,
			MobileNumber = MobileNumber ?? ContactNumber,
			IsParentDefault = string.Equals(Type, "student", StringComparison.OrdinalIgnoreCase) == false
		};
	}
}

public class LegacyTemplateDto
{
	public string? Id { get; set; }
	public string? TemplateName { get; set; }
	public string? Subject { get; set; }
	public string? Message { get; set; }
	public Guid? ParsedId => Guid.TryParse(Id, out var id) ? id : null;
}

public class LegacyAnnouncementTypeDto
{
	public Guid? Id { get; set; }
	public string? AnnouncementType { get; set; }
}

public class LegacyChannelUpdateDto
{
	public List<string> Channels { get; set; } = [];
}

public class LegacyMessageUpdateDto
{
	public string? Subject { get; set; }
	public string? Message { get; set; }
	public LegacyTemplateDto? SelectedTemplate { get; set; }
}

public static class LegacyCommunicationMapper
{
	public static CreateUpdateCommunicationCampaignDto ToUpdateInput(CommunicationCampaignDto campaign)
	{
		return new CreateUpdateCommunicationCampaignDto
		{
			AcademicMediumId = campaign.AcademicMediumId,
			AcademicYearId = campaign.AcademicYearId,
			SourceModule = campaign.SourceModule,
			SourceEntityType = campaign.SourceEntityType,
			SourceEntityId = campaign.SourceEntityId,
			SourceReference = campaign.SourceReference,
			CampaignType = campaign.CampaignType,
			Title = campaign.Title,
			Subject = campaign.Subject,
			Body = campaign.Body,
			ImportanceLevel = campaign.ImportanceLevel,
			ApprovalRequired = campaign.ApprovalRequired,
			TemplateId = campaign.TemplateId,
			AnnouncementTypeId = campaign.AnnouncementTypeId,
			AudienceDefinition = campaign.AudienceDefinition,
			Channels = campaign.Channels,
			FallbackOrder = campaign.FallbackOrder,
			Attachments = campaign.Attachments,
			LinkUrl = campaign.LinkUrl,
			LanguageCode = campaign.LanguageCode,
			ScheduledAt = campaign.ScheduledAt,
			RequiresAcknowledgement = campaign.RequiresAcknowledgement
		};
	}

	public static List<CommunicationChannelType> ParseChannels(IEnumerable<string>? channels)
	{
		return (channels ?? [])
			.Select(ParseChannel)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
	}

	public static string ToChannelLabel(CommunicationChannelType channel)
	{
		return channel switch
		{
			CommunicationChannelType.WhatsApp => "WhatsApp",
			CommunicationChannelType.Sms => "SMS",
			CommunicationChannelType.Email => "Email",
			CommunicationChannelType.InApp => "In-App",
			_ => channel.ToString()
		};
	}

	public static CommunicationImportanceLevel ParseImportanceLevel(object? value)
	{
		var normalized = value switch
		{
			null => null,
			string text => text,
			JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
			JsonElement { ValueKind: JsonValueKind.Object } element when element.TryGetProperty("levelName", out var levelName) => levelName.GetString(),
			JsonElement { ValueKind: JsonValueKind.Object } element when element.TryGetProperty("name", out var name) => name.GetString(),
			_ => value.ToString()
		};

		if (normalized.IsNullOrWhiteSpace())
		{
			return CommunicationImportanceLevel.Normal;
		}

		return normalized.Trim().ToLowerInvariant() switch
		{
			"urgent" or "high" => CommunicationImportanceLevel.Urgent,
			"critical" => CommunicationImportanceLevel.Critical,
			_ => CommunicationImportanceLevel.Normal
		};
	}

	private static CommunicationChannelType? ParseChannel(string? channel)
	{
		if (channel.IsNullOrWhiteSpace())
		{
			return null;
		}

		return channel.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant() switch
		{
			"whatsapp" => CommunicationChannelType.WhatsApp,
			"sms" => CommunicationChannelType.Sms,
			"email" => CommunicationChannelType.Email,
			"inapp" or "push" or "pushnotification" or "app" => CommunicationChannelType.InApp,
			_ => null
		};
	}
}
