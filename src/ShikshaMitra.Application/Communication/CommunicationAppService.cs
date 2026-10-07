using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.Communication;

public partial class CommunicationAppService : ApplicationService, ICommunicationAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly IRepository<CommunicationCampaign, Guid> _campaignRepository;
	private readonly IRepository<CommunicationTemplate, Guid> _templateRepository;
	private readonly IRepository<ChannelProviderSetting, Guid> _providerSettingRepository;
	private readonly IRepository<ConsentRecord, Guid> _consentRecordRepository;
	private readonly IRepository<CommunicationRecipientSnapshot, Guid> _recipientSnapshotRepository;
	private readonly IRepository<DeliveryAttempt, Guid> _deliveryAttemptRepository;
	private readonly IRepository<CommunicationReply, Guid> _replyRepository;
	private readonly IRepository<UsageLedgerEntry, Guid> _usageLedgerRepository;
	private readonly IRepository<CommunicationApprovalRule, Guid> _approvalRuleRepository;
	private readonly ICommunicationAudienceResolver _audienceResolver;
	private readonly IEnumerable<ICommunicationChannelGateway> _channelGateways;

	public CommunicationAppService(
		IRepository<CommunicationCampaign, Guid> campaignRepository,
		IRepository<CommunicationTemplate, Guid> templateRepository,
		IRepository<ChannelProviderSetting, Guid> providerSettingRepository,
		IRepository<ConsentRecord, Guid> consentRecordRepository,
		IRepository<CommunicationRecipientSnapshot, Guid> recipientSnapshotRepository,
		IRepository<DeliveryAttempt, Guid> deliveryAttemptRepository,
		IRepository<CommunicationReply, Guid> replyRepository,
		IRepository<UsageLedgerEntry, Guid> usageLedgerRepository,
		IRepository<CommunicationApprovalRule, Guid> approvalRuleRepository,
		ICommunicationAudienceResolver audienceResolver,
		IEnumerable<ICommunicationChannelGateway> channelGateways)
	{
		_campaignRepository = campaignRepository;
		_templateRepository = templateRepository;
		_providerSettingRepository = providerSettingRepository;
		_consentRecordRepository = consentRecordRepository;
		_recipientSnapshotRepository = recipientSnapshotRepository;
		_deliveryAttemptRepository = deliveryAttemptRepository;
		_replyRepository = replyRepository;
		_usageLedgerRepository = usageLedgerRepository;
		_approvalRuleRepository = approvalRuleRepository;
		_audienceResolver = audienceResolver;
		_channelGateways = channelGateways;
	}

	public async Task<CommunicationDashboardDto> GetDashboardAsync()
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Dashboard.View);

		var campaignQuery = await _campaignRepository.GetQueryableAsync();
		var replyQuery = await _replyRepository.GetQueryableAsync();
		var providerQuery = await _providerSettingRepository.GetQueryableAsync();
		var consentQuery = await _consentRecordRepository.GetQueryableAsync();
		var deliveryQuery = await _deliveryAttemptRepository.GetQueryableAsync();
		var usageQuery = await _usageLedgerRepository.GetQueryableAsync();

		var recentUsageRows = await AsyncExecuter.ToListAsync(
			usageQuery
				.OrderByDescending(x => x.UsageDate)
				.ThenByDescending(x => x.CreationTime)
				.Take(8));

		return new CommunicationDashboardDto
		{
			TotalCampaigns = await AsyncExecuter.CountAsync(campaignQuery),
			DraftCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.Draft)),
			PendingApprovalCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.PendingApproval)),
			ScheduledCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.Scheduled)),
			QueuedCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.Queued || x.Status == CommunicationCampaignStatus.Sending)),
			SentCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.Sent)),
			FailedCampaigns = await AsyncExecuter.CountAsync(campaignQuery.Where(x => x.Status == CommunicationCampaignStatus.Failed || x.Status == CommunicationCampaignStatus.PartiallyFailed)),
			OpenReplies = await AsyncExecuter.CountAsync(replyQuery.Where(x => x.Status == CommunicationReplyStatus.Open || x.Status == CommunicationReplyStatus.Assigned)),
			EnabledProviders = await AsyncExecuter.CountAsync(providerQuery.Where(x => x.IsEnabled && x.Status == CommunicationProviderStatus.Enabled)),
			ConsentOptInCount = await AsyncExecuter.CountAsync(consentQuery.Where(x => x.Status == CommunicationConsentStatus.OptedIn)),
			ConsentOptOutCount = await AsyncExecuter.CountAsync(consentQuery.Where(x => x.Status == CommunicationConsentStatus.OptedOut || x.Status == CommunicationConsentStatus.Revoked)),
			DeliveryAttemptCount = await AsyncExecuter.CountAsync(deliveryQuery),
			DeliveryFailureCount = await AsyncExecuter.CountAsync(deliveryQuery.Where(x => x.Status == CommunicationDeliveryStatus.Failed || x.Status == CommunicationDeliveryStatus.Bounced || x.Status == CommunicationDeliveryStatus.Skipped)),
			EstimatedCost = await SumEstimatedCostAsync(usageQuery),
			RecentUsage = recentUsageRows.Select(MapUsageLedgerEntry).ToList()
		};
	}

	public async Task<PagedResultDto<CommunicationCampaignDto>> GetCampaignsAsync(GetCommunicationCampaignListRequestDto input)
	{
		await CheckCampaignPolicyAsync(
			input.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.View,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.View);

		var query = await _campaignRepository.GetQueryableAsync();

		if (!input.Search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.Title.Contains(input.Search!) || (x.Subject != null && x.Subject.Contains(input.Search!)));
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		if (input.SourceModule.HasValue)
		{
			query = query.Where(x => x.SourceModule == input.SourceModule.Value);
		}

		if (!input.SourceEntityType.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.SourceEntityType == input.SourceEntityType);
		}

		if (input.SourceEntityId.HasValue)
		{
			query = query.Where(x => x.SourceEntityId == input.SourceEntityId.Value);
		}

		if (!input.CampaignType.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.CampaignType == input.CampaignType);
		}

		if (input.AcademicMediumId.HasValue)
		{
			query = query.Where(x => x.AcademicMediumId == input.AcademicMediumId);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<CommunicationCampaignDto>(totalCount, rows.Select(MapCampaign).ToList());
	}

	public async Task<CommunicationCampaignDto> GetCampaignAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.View,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.View);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> CreateCampaignAsync(CreateUpdateCommunicationCampaignDto input)
	{
		await CheckCampaignPolicyAsync(
			input.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Add,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Add);

		return await CreateCampaignCoreAsync(input);
	}

	private async Task<CommunicationCampaignDto> CreateCampaignCoreAsync(CreateUpdateCommunicationCampaignDto input)
	{
		var campaign = new CommunicationCampaign(GuidGenerator.Create(), CurrentTenant.Id);
		ApplyCampaignInput(campaign, input);

		var preview = await BuildAudiencePreviewAsync(input.AudienceDefinition);
		campaign.RecipientCount = preview.TotalRecipientCount;
		campaign.EligibleRecipientCount = preview.EligibleRecipientCount;
		campaign.BlockedRecipientCount = preview.BlockedRecipientCount;
		campaign.ApprovalRequired = await RequiresApprovalAsync(campaign, preview.EligibleRecipientCount)
			|| input.ApprovalRequired;
		campaign.Status = campaign.ApprovalRequired
			? CommunicationCampaignStatus.PendingApproval
			: CommunicationCampaignStatus.Draft;

		await _campaignRepository.InsertAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> UpdateCampaignAsync(Guid id, CreateUpdateCommunicationCampaignDto input)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Add,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Add);

		if (campaign.PublishedAt.HasValue
			|| campaign.Status is CommunicationCampaignStatus.Queued
				or CommunicationCampaignStatus.Sending
				or CommunicationCampaignStatus.Sent
				or CommunicationCampaignStatus.PartiallyFailed
				or CommunicationCampaignStatus.Failed
				or CommunicationCampaignStatus.Cancelled)
		{
			throw new BusinessException("Communication:CampaignAlreadyPublished");
		}

		ApplyCampaignInput(campaign, input);
		var preview = await BuildAudiencePreviewAsync(input.AudienceDefinition);
		campaign.RecipientCount = preview.TotalRecipientCount;
		campaign.EligibleRecipientCount = preview.EligibleRecipientCount;
		campaign.BlockedRecipientCount = preview.BlockedRecipientCount;
		campaign.ApprovalRequired = await RequiresApprovalAsync(campaign, preview.EligibleRecipientCount)
			|| input.ApprovalRequired;
		campaign.Status = campaign.ApprovalRequired
			? CommunicationCampaignStatus.PendingApproval
			: CommunicationCampaignStatus.Draft;
		campaign.ApprovedByUserId = null;
		campaign.ApprovedAt = null;
		campaign.RejectionReason = null;

		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task DeleteCampaignAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Delete,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Delete);

		if (campaign.PublishedAt.HasValue || await HasCampaignHistoryAsync(id))
		{
			throw new BusinessException("Communication:CancelPublishedCampaign");
		}

		if (campaign.Status is not (CommunicationCampaignStatus.Draft or CommunicationCampaignStatus.PendingApproval or CommunicationCampaignStatus.Approved))
		{
			throw new BusinessException("Communication:CancelPublishedCampaign");
		}

		await DeleteCampaignChildrenAsync(id);
		await _campaignRepository.DeleteAsync(id);
	}

	public async Task<CommunicationAudiencePreviewDto> PreviewAudienceAsync(CommunicationAudienceDefinitionDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Campaigns.View);
		return await BuildAudiencePreviewAsync(input);
	}

	public async Task<CommunicationCampaignDto> SubmitForApprovalAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Add,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Add);

		campaign.Status = CommunicationCampaignStatus.PendingApproval;
		campaign.RejectionReason = null;
		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> ApproveCampaignAsync(Guid id, CommunicationApprovalDto input)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Approve,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Approve);

		campaign.Status = CommunicationCampaignStatus.Approved;
		campaign.ApprovedByUserId = CurrentUser.Id;
		campaign.ApprovedAt = DateTimeOffset.UtcNow;
		campaign.RejectionReason = null;
		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> RejectCampaignAsync(Guid id, CommunicationApprovalDto input)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Approve,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Approve);

		campaign.Status = CommunicationCampaignStatus.Draft;
		campaign.ApprovedByUserId = null;
		campaign.ApprovedAt = null;
		campaign.RejectionReason = input.Reason;
		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> PublishCampaignAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Send,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Send);

		return await PublishCampaignCoreAsync(campaign);
	}

	private async Task<CommunicationCampaignDto> PublishCampaignCoreAsync(CommunicationCampaign campaign)
	{
		if (campaign.PublishedAt.HasValue || await HasCampaignHistoryAsync(campaign.Id))
		{
			throw new BusinessException("Communication:CampaignAlreadyPublished");
		}

		var channels = Deserialize(campaign.ChannelsJson, new List<CommunicationChannelType>()).Distinct().ToList();
		if (channels.Count == 0)
		{
			throw new BusinessException("Communication:ChannelRequired");
		}

		var audience = Deserialize(campaign.AudienceDefinitionJson, new CommunicationAudienceDefinitionDto());
		var resolution = await _audienceResolver.ResolveAsync(campaign.Id, audience);
		if (resolution.BlockingIssues.Count > 0)
		{
			throw new BusinessException("Communication:AudienceResolverIncomplete")
				.WithData("Issues", string.Join("; ", resolution.BlockingIssues));
		}

		if (resolution.Recipients.Count == 0)
		{
			throw new BusinessException("Communication:AudienceEmpty");
		}

		if ((campaign.ApprovalRequired || await RequiresApprovalAsync(campaign, resolution.Recipients.Count(x => x.IsEligible)))
			&& campaign.Status != CommunicationCampaignStatus.Approved)
		{
			throw new BusinessException("Communication:ApprovalRequired");
		}

		var providers = await GetEnabledProvidersAsync(channels);
		var missingProviderChannels = channels.Where(channel => !providers.ContainsKey(channel)).ToList();
		if (missingProviderChannels.Count > 0)
		{
			throw new BusinessException("Communication:ProviderMissing")
				.WithData("Channels", string.Join(", ", missingProviderChannels));
		}

		await ValidateTemplateApprovalAsync(campaign, providers.Values);

		var recipients = resolution.Recipients;
		foreach (var recipient in recipients)
		{
			await _recipientSnapshotRepository.InsertAsync(recipient);
		}

		var attempts = new List<DeliveryAttempt>();
		foreach (var recipient in recipients)
		{
			foreach (var channel in channels)
			{
				var provider = providers[channel];
				var attempt = new DeliveryAttempt(GuidGenerator.Create(), CurrentTenant.Id)
				{
					CampaignId = campaign.Id,
					RecipientSnapshotId = recipient.Id,
					Channel = channel,
					ProviderName = provider.ProviderName,
					Destination = ResolveDestination(recipient, channel),
					QueuedAt = DateTimeOffset.UtcNow,
					CurrencyCode = "INR"
				};

				if (attempt.Destination.IsNullOrWhiteSpace())
				{
					MarkAttemptSkipped(attempt, "DESTINATION_MISSING", "Recipient does not have a usable destination for this channel.");
				}
				else if (campaign.SourceEntityType == InquiryQualificationSource &&
					await HasWithdrawnInquiryConsentAsync(recipient, channel, attempt.Destination))
				{
					MarkAttemptSkipped(attempt, "CONSENT_WITHDRAWN", "Recipient withdrew consent for inquiry notifications.");
				}
				else if (provider.ConsentRequired && !await HasOptedInConsentAsync(recipient, channel, attempt.Destination))
				{
					MarkAttemptSkipped(attempt, "CONSENT_REQUIRED", "Recipient has no opt-in consent for this channel destination.");
				}
				else
				{
					var sendResult = await GetGateway(channel, provider.ProviderName).SendAsync(new CommunicationSendRequest
					{
						CampaignId = campaign.Id,
						RecipientSnapshotId = recipient.Id,
						Channel = channel,
						ProviderName = provider.ProviderName,
						Destination = attempt.Destination,
						Subject = campaign.Subject,
						Body = campaign.Body,
						LinkUrl = campaign.LinkUrl,
						LanguageCode = recipient.PreferredLanguageCode.IsNullOrWhiteSpace() ? campaign.LanguageCode : recipient.PreferredLanguageCode,
						AttemptNumber = attempt.AttemptNumber
					});

					attempt.Status = sendResult.Status;
					attempt.ProviderMessageId = sendResult.ProviderMessageId;
					attempt.ErrorCode = sendResult.ErrorCode;
					attempt.ErrorMessage = sendResult.ErrorMessage;
					attempt.EstimatedCost = sendResult.EstimatedCost;
					attempt.CurrencyCode = sendResult.CurrencyCode;
					attempt.QueuedAt = sendResult.QueuedAt ?? attempt.QueuedAt;
					attempt.SentAt = sendResult.SentAt;
				}

				attempts.Add(attempt);
				await _deliveryAttemptRepository.InsertAsync(attempt);
			}
		}

		foreach (var channelGroup in attempts.GroupBy(x => new { x.Channel, x.ProviderName }))
		{
			await _usageLedgerRepository.InsertAsync(new UsageLedgerEntry(GuidGenerator.Create(), CurrentTenant.Id)
			{
				CampaignId = campaign.Id,
				Channel = channelGroup.Key.Channel,
				ProviderName = channelGroup.Key.ProviderName ?? "Not configured",
				UsageDate = DateOnly.FromDateTime(DateTime.UtcNow),
				AttemptCount = channelGroup.Count(),
				DeliveredCount = channelGroup.Count(x => x.Status == CommunicationDeliveryStatus.Delivered),
				FailedCount = channelGroup.Count(x => x.Status is CommunicationDeliveryStatus.Failed or CommunicationDeliveryStatus.Bounced or CommunicationDeliveryStatus.Skipped),
				EstimatedCost = channelGroup.Sum(x => x.EstimatedCost ?? 0),
				CurrencyCode = "INR"
			});
		}

		campaign.PublishedAt = DateTimeOffset.UtcNow;
		campaign.RecipientCount = recipients.Count;
		campaign.EligibleRecipientCount = recipients.Count(x => x.IsEligible);
		campaign.BlockedRecipientCount = recipients.Count(x => !x.IsEligible);
		campaign.QueuedCount = attempts.Count(x => x.Status == CommunicationDeliveryStatus.Queued);
		campaign.DeliveredCount = attempts.Count(x => x.Status == CommunicationDeliveryStatus.Delivered);
		campaign.FailedCount = attempts.Count(x => x.Status is CommunicationDeliveryStatus.Failed or CommunicationDeliveryStatus.Bounced or CommunicationDeliveryStatus.Skipped);
		campaign.Status = CommunicationCampaignDeliveryStatusResolver.Resolve(attempts.Select(attempt => attempt.Status));

		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> CancelCampaignAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Send,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Send);

		campaign.Status = CommunicationCampaignStatus.Cancelled;
		await _campaignRepository.UpdateAsync(campaign, autoSave: true);
		return MapCampaign(campaign);
	}

	public async Task<CommunicationCampaignDto> CloneCampaignAsync(Guid id)
	{
		var campaign = await _campaignRepository.GetAsync(id);
		await CheckCampaignPolicyAsync(
			campaign.CampaignType,
			ShikshaMitraSubmodulePermissions.Communication.Campaigns.Add,
			ShikshaMitraSubmodulePermissions.Communication.Announcements.Add);

		var clone = new CommunicationCampaign(GuidGenerator.Create(), CurrentTenant.Id)
		{
			AcademicMediumId = campaign.AcademicMediumId,
			AcademicYearId = campaign.AcademicYearId,
			SourceModule = campaign.SourceModule,
			SourceEntityType = campaign.SourceEntityType,
			SourceEntityId = campaign.SourceEntityId,
			SourceReference = campaign.SourceReference,
			CampaignType = campaign.CampaignType,
			Title = $"{campaign.Title} Copy",
			Subject = campaign.Subject,
			Body = campaign.Body,
			ImportanceLevel = campaign.ImportanceLevel,
			ApprovalRequired = campaign.ApprovalRequired,
			TemplateId = campaign.TemplateId,
			AnnouncementTypeId = campaign.AnnouncementTypeId,
			AudienceDefinitionJson = campaign.AudienceDefinitionJson,
			ChannelsJson = campaign.ChannelsJson,
			FallbackOrderJson = campaign.FallbackOrderJson,
			AttachmentsJson = campaign.AttachmentsJson,
			LinkUrl = campaign.LinkUrl,
			LanguageCode = campaign.LanguageCode,
			ScheduledAt = campaign.ScheduledAt,
			RequiresAcknowledgement = campaign.RequiresAcknowledgement
		};

		await _campaignRepository.InsertAsync(clone, autoSave: true);
		return MapCampaign(clone);
	}

	public async Task<PagedResultDto<CommunicationTemplateDto>> GetTemplatesAsync(GetCommunicationTemplateListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.View);

		var query = await _templateRepository.GetQueryableAsync();

		if (input.AcademicMediumId.HasValue)
		{
			query = query.Where(x => x.AcademicMediumId == input.AcademicMediumId);
		}

		if (!input.Search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.TemplateName.Contains(input.Search!) || x.Subject.Contains(input.Search!) || x.Body.Contains(input.Search!));
		}

		if (!input.LanguageCode.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.LanguageCode == input.LanguageCode);
		}

		if (!input.Category.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.Category == input.Category);
		}

		if (input.Channel.HasValue)
		{
			var channelName = input.Channel.Value.ToString();
			query = query.Where(x => x.ChannelsJson.Contains(channelName));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<CommunicationTemplateDto>(totalCount, rows.Select(MapTemplate).ToList());
	}

	public async Task<CommunicationTemplateDto> GetTemplateAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.View);
		return MapTemplate(await _templateRepository.GetAsync(id));
	}

	public async Task<CommunicationTemplateDto> CreateTemplateAsync(CreateUpdateCommunicationTemplateDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.Add);

		var template = new CommunicationTemplate(GuidGenerator.Create(), CurrentTenant.Id);
		ApplyTemplateInput(template, input);
		await _templateRepository.InsertAsync(template, autoSave: true);
		return MapTemplate(template);
	}

	public async Task<CommunicationTemplateDto> UpdateTemplateAsync(Guid id, CreateUpdateCommunicationTemplateDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.Add);

		var template = await _templateRepository.GetAsync(id);
		ApplyTemplateInput(template, input);
		template.Version += 1;
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return MapTemplate(template);
	}

	public async Task DeleteTemplateAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.Delete);
		await _templateRepository.DeleteAsync(id);
	}

	public async Task<PagedResultDto<ChannelProviderSettingDto>> GetProviderSettingsAsync(GetChannelProviderSettingListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.View);

		var query = await _providerSettingRepository.GetQueryableAsync();

		if (!input.Search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.ProviderName.Contains(input.Search!) || (x.SenderId != null && x.SenderId.Contains(input.Search!)));
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status);
		}

		if (input.IsEnabled.HasValue)
		{
			query = query.Where(x => x.IsEnabled == input.IsEnabled);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Channel)
				.ThenBy(x => x.ProviderName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<ChannelProviderSettingDto>(totalCount, rows.Select(MapProviderSetting).ToList());
	}

	public async Task<ChannelProviderSettingDto> GetProviderSettingAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.View);
		return MapProviderSetting(await _providerSettingRepository.GetAsync(id));
	}

	public async Task<ChannelProviderSettingDto> CreateProviderSettingAsync(CreateUpdateChannelProviderSettingDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add);

		if (input.IsDefault)
		{
			await ClearDefaultProviderAsync(input.Channel);
		}

		var setting = new ChannelProviderSetting(GuidGenerator.Create(), CurrentTenant.Id);
		ApplyProviderSettingInput(setting, input);
		await _providerSettingRepository.InsertAsync(setting, autoSave: true);
		return MapProviderSetting(setting);
	}

	public async Task<ChannelProviderSettingDto> UpdateProviderSettingAsync(Guid id, CreateUpdateChannelProviderSettingDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add);

		if (input.IsDefault)
		{
			await ClearDefaultProviderAsync(input.Channel, id);
		}

		var setting = await _providerSettingRepository.GetAsync(id);
		ApplyProviderSettingInput(setting, input);
		await _providerSettingRepository.UpdateAsync(setting, autoSave: true);
		return MapProviderSetting(setting);
	}

	public async Task DeleteProviderSettingAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Delete);
		await _providerSettingRepository.DeleteAsync(id);
	}

	public async Task<PagedResultDto<ConsentRecordDto>> GetConsentRecordsAsync(GetConsentRecordListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Consent.View);

		var query = await _consentRecordRepository.GetQueryableAsync();

		if (!input.Search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.DisplayName.Contains(input.Search!) || x.Destination.Contains(input.Search!));
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status);
		}

		if (!input.RecipientType.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.RecipientType == input.RecipientType);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<ConsentRecordDto>(totalCount, rows.Select(MapConsentRecord).ToList());
	}

	public async Task<ConsentRecordDto> UpsertConsentRecordAsync(CreateUpdateConsentRecordDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Consent.Add);

		var query = await _consentRecordRepository.GetQueryableAsync();
		var record = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x =>
				x.Channel == input.Channel
				&& x.Destination == input.Destination
				&& x.RecipientType == input.RecipientType
				&& x.RecipientId == input.RecipientId));

		var isNew = record is null;
		if (record is null)
		{
			record = new ConsentRecord(GuidGenerator.Create(), CurrentTenant.Id);
			record.ConsentCapturedAt = DateTimeOffset.UtcNow;
		}

		record.RecipientType = input.RecipientType;
		record.RecipientId = input.RecipientId;
		record.DisplayName = input.DisplayName;
		record.Channel = input.Channel;
		record.Destination = input.Destination;
		record.Status = input.Status;
		record.Source = input.Source;
		record.Notes = input.Notes;

		if (input.Status == CommunicationConsentStatus.OptedIn)
		{
			record.ConsentCapturedAt ??= DateTimeOffset.UtcNow;
			record.RevokedAt = null;
		}
		else if (input.Status is CommunicationConsentStatus.OptedOut or CommunicationConsentStatus.Revoked)
		{
			record.RevokedAt = DateTimeOffset.UtcNow;
		}

		if (isNew)
		{
			await _consentRecordRepository.InsertAsync(record, autoSave: true);
		}
		else
		{
			await _consentRecordRepository.UpdateAsync(record, autoSave: true);
		}

		return MapConsentRecord(record);
	}

	public async Task<PagedResultDto<CommunicationApprovalRuleDto>> GetApprovalRulesAsync(GetCommunicationApprovalRuleListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ApprovalRules.View);

		var query = await _approvalRuleRepository.GetQueryableAsync();

		if (!input.Search.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.RuleName.Contains(input.Search!));
		}

		if (input.IsEnabled.HasValue)
		{
			query = query.Where(x => x.IsEnabled == input.IsEnabled);
		}

		if (!input.CampaignType.IsNullOrWhiteSpace())
		{
			query = query.Where(x => x.CampaignType == input.CampaignType);
		}

		if (input.SourceModule.HasValue)
		{
			query = query.Where(x => x.SourceModule == input.SourceModule);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.IsEnabled)
				.ThenBy(x => x.RuleName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<CommunicationApprovalRuleDto>(totalCount, rows.Select(MapApprovalRule).ToList());
	}

	public async Task<CommunicationApprovalRuleDto> GetApprovalRuleAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ApprovalRules.View);
		return MapApprovalRule(await _approvalRuleRepository.GetAsync(id));
	}

	public async Task<CommunicationApprovalRuleDto> CreateApprovalRuleAsync(CreateUpdateCommunicationApprovalRuleDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ApprovalRules.Add);

		var rule = new CommunicationApprovalRule(GuidGenerator.Create(), CurrentTenant.Id);
		ApplyApprovalRuleInput(rule, input);
		await _approvalRuleRepository.InsertAsync(rule, autoSave: true);
		return MapApprovalRule(rule);
	}

	public async Task<CommunicationApprovalRuleDto> UpdateApprovalRuleAsync(Guid id, CreateUpdateCommunicationApprovalRuleDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ApprovalRules.Add);

		var rule = await _approvalRuleRepository.GetAsync(id);
		ApplyApprovalRuleInput(rule, input);
		await _approvalRuleRepository.UpdateAsync(rule, autoSave: true);
		return MapApprovalRule(rule);
	}

	public async Task DeleteApprovalRuleAsync(Guid id)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Settings_ApprovalRules.Delete);
		await _approvalRuleRepository.DeleteAsync(id);
	}

	public async Task<PagedResultDto<DeliveryAttemptDto>> GetDeliveryAttemptsAsync(GetDeliveryAttemptListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.History.View);

		var query = await _deliveryAttemptRepository.GetQueryableAsync();

		if (input.CampaignId.HasValue)
		{
			query = query.Where(x => x.CampaignId == input.CampaignId);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<DeliveryAttemptDto>(totalCount, rows.Select(MapDeliveryAttempt).ToList());
	}

	public async Task<PagedResultDto<CommunicationReplyDto>> GetRepliesAsync(GetCommunicationReplyListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Inbox.View);

		var query = await _replyRepository.GetQueryableAsync();

		if (input.CampaignId.HasValue)
		{
			query = query.Where(x => x.CampaignId == input.CampaignId);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.ReceivedAt)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<CommunicationReplyDto>(totalCount, rows.Select(MapReply).ToList());
	}

	public async Task<CommunicationReplyDto> CaptureReplyAsync(CaptureCommunicationReplyDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Inbox.View);

		var reply = new CommunicationReply(GuidGenerator.Create(), CurrentTenant.Id)
		{
			CampaignId = input.CampaignId,
			DeliveryAttemptId = input.DeliveryAttemptId,
			RecipientSnapshotId = input.RecipientSnapshotId,
			Channel = input.Channel,
			FromAddress = input.FromAddress,
			Body = input.Body,
			ReceivedAt = DateTimeOffset.UtcNow
		};

		await _replyRepository.InsertAsync(reply, autoSave: true);
		return MapReply(reply);
	}

	public async Task<CommunicationReplyDto> AssignReplyAsync(Guid id, AssignCommunicationReplyDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.Inbox.Assign);

		var reply = await _replyRepository.GetAsync(id);
		reply.AssignedToUserId = input.AssignedToUserId;
		reply.AssignedDepartment = input.AssignedDepartment;
		reply.Status = input.Status;
		reply.InternalNotes = input.InternalNotes;
		await _replyRepository.UpdateAsync(reply, autoSave: true);
		return MapReply(reply);
	}

	public async Task<PagedResultDto<UsageLedgerEntryDto>> GetUsageLedgerAsync(GetUsageLedgerListRequestDto input)
	{
		await CheckPolicyAsync(ShikshaMitraSubmodulePermissions.Communication.UsageLedger.View);

		var query = await _usageLedgerRepository.GetQueryableAsync();

		if (input.CampaignId.HasValue)
		{
			query = query.Where(x => x.CampaignId == input.CampaignId);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		if (input.FromDate.HasValue)
		{
			query = query.Where(x => x.UsageDate >= input.FromDate.Value);
		}

		if (input.ToDate.HasValue)
		{
			query = query.Where(x => x.UsageDate <= input.ToDate.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.UsageDate)
				.ThenByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<UsageLedgerEntryDto>(totalCount, rows.Select(MapUsageLedgerEntry).ToList());
	}

	public Task<List<CommunicationOptionDto>> GetChannelOptionsAsync()
	{
		return Task.FromResult(Enum.GetValues<CommunicationChannelType>()
			.Select(x => new CommunicationOptionDto
			{
				Id = ((int)x).ToString(),
				Name = x.ToString(),
				Label = x == CommunicationChannelType.InApp ? "In-App" : x.ToString()
			})
			.ToList());
	}

	public Task<List<CommunicationOptionDto>> GetImportanceLevelOptionsAsync()
	{
		return Task.FromResult(Enum.GetValues<CommunicationImportanceLevel>()
			.Select(x => new CommunicationOptionDto
			{
				Id = ((int)x).ToString(),
				Name = x.ToString(),
				Label = x.ToString()
			})
			.ToList());
	}

	private void ApplyCampaignInput(CommunicationCampaign campaign, CreateUpdateCommunicationCampaignDto input)
	{
		campaign.AcademicMediumId = input.AcademicMediumId;
		campaign.AcademicYearId = input.AcademicYearId;
		campaign.SourceModule = input.SourceModule;
		campaign.SourceEntityType = input.SourceEntityType.IsNullOrWhiteSpace() ? null : input.SourceEntityType.Trim();
		campaign.SourceEntityId = input.SourceEntityId;
		campaign.SourceReference = input.SourceReference.IsNullOrWhiteSpace() ? null : input.SourceReference.Trim();
		campaign.CampaignType = input.CampaignType.IsNullOrWhiteSpace() ? "Notification" : input.CampaignType.Trim();
		campaign.Title = input.Title.Trim();
		campaign.Subject = input.Subject;
		campaign.Body = input.Body;
		campaign.ImportanceLevel = input.ImportanceLevel;
		campaign.ApprovalRequired = input.ApprovalRequired;
		campaign.TemplateId = input.TemplateId;
		campaign.AnnouncementTypeId = input.AnnouncementTypeId;
		campaign.AudienceDefinitionJson = Serialize(input.AudienceDefinition);
		campaign.ChannelsJson = Serialize(input.Channels.Distinct().ToList());
		campaign.FallbackOrderJson = Serialize(input.FallbackOrder.Distinct().ToList());
		campaign.AttachmentsJson = Serialize(input.Attachments);
		campaign.LinkUrl = input.LinkUrl;
		campaign.LanguageCode = input.LanguageCode.IsNullOrWhiteSpace() ? "en" : input.LanguageCode.Trim();
		campaign.ScheduledAt = input.ScheduledAt;
		campaign.RequiresAcknowledgement = input.RequiresAcknowledgement;
	}

	private void ApplyTemplateInput(CommunicationTemplate template, CreateUpdateCommunicationTemplateDto input)
	{
		template.AcademicMediumId = input.AcademicMediumId;
		template.TemplateName = input.TemplateName.Trim();
		template.TargetRole = input.TargetRole;
		template.Category = input.Category.IsNullOrWhiteSpace() ? "General" : input.Category.Trim();
		template.Subject = input.Subject;
		template.Body = input.Body;
		template.LanguageCode = input.LanguageCode.IsNullOrWhiteSpace() ? "en" : input.LanguageCode.Trim();
		template.ChannelsJson = Serialize(input.Channels.Distinct().ToList());
		template.VariablesJson = Serialize(input.Variables.Distinct().ToList());
		template.HashtagsJson = Serialize(input.Hashtags.Distinct().ToList());
		template.ApprovalStatus = input.ApprovalStatus;
		template.IsActive = input.IsActive;
	}

	private void ApplyProviderSettingInput(ChannelProviderSetting setting, CreateUpdateChannelProviderSettingDto input)
	{
		setting.Channel = input.Channel;
		setting.ProviderName = input.ProviderName.Trim();
		setting.SenderId = input.SenderId;
		setting.IsEnabled = input.IsEnabled;
		setting.IsDefault = input.IsDefault;
		setting.ConfigurationJson = input.ConfigurationJson;
		setting.SecretReference = input.SecretReference;
		setting.FallbackOrderJson = Serialize(input.FallbackOrder.Distinct().ToList());
		setting.DailyQuota = input.DailyQuota;
		setting.MonthlyQuota = input.MonthlyQuota;
		setting.ConsentRequired = input.ConsentRequired;
		setting.TemplateApprovalRequired = input.TemplateApprovalRequired;
		setting.Status = input.Status;
	}

	private static void ApplyApprovalRuleInput(CommunicationApprovalRule rule, CreateUpdateCommunicationApprovalRuleDto input)
	{
		rule.RuleName = input.RuleName.Trim();
		rule.IsEnabled = input.IsEnabled;
		rule.CampaignType = input.CampaignType.IsNullOrWhiteSpace() ? null : input.CampaignType.Trim();
		rule.SourceModule = input.SourceModule;
		rule.Channel = input.Channel;
		rule.MinimumAudienceSize = input.MinimumAudienceSize;
		rule.ImportanceLevel = input.ImportanceLevel;
		rule.RequiresApproval = input.RequiresApproval;
	}

	private static CommunicationCampaignDto MapCampaign(CommunicationCampaign campaign)
	{
		return new CommunicationCampaignDto
		{
			Id = campaign.Id,
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
			Status = campaign.Status,
			ApprovalRequired = campaign.ApprovalRequired,
			ApprovedByUserId = campaign.ApprovedByUserId,
			ApprovedAt = campaign.ApprovedAt,
			RejectionReason = campaign.RejectionReason,
			TemplateId = campaign.TemplateId,
			AnnouncementTypeId = campaign.AnnouncementTypeId,
			AudienceDefinition = Deserialize(campaign.AudienceDefinitionJson, new CommunicationAudienceDefinitionDto()),
			Channels = Deserialize(campaign.ChannelsJson, new List<CommunicationChannelType>()),
			FallbackOrder = Deserialize(campaign.FallbackOrderJson, new List<CommunicationChannelType>()),
			Attachments = Deserialize(campaign.AttachmentsJson, new List<CommunicationAttachmentDto>()),
			LinkUrl = campaign.LinkUrl,
			LanguageCode = campaign.LanguageCode,
			ScheduledAt = campaign.ScheduledAt,
			PublishedAt = campaign.PublishedAt,
			RequiresAcknowledgement = campaign.RequiresAcknowledgement,
			RecipientCount = campaign.RecipientCount,
			EligibleRecipientCount = campaign.EligibleRecipientCount,
			BlockedRecipientCount = campaign.BlockedRecipientCount,
			QueuedCount = campaign.QueuedCount,
			DeliveredCount = campaign.DeliveredCount,
			FailedCount = campaign.FailedCount,
			CreationTime = campaign.CreationTime
		};
	}

	private static CommunicationTemplateDto MapTemplate(CommunicationTemplate template)
	{
		return new CommunicationTemplateDto
		{
			Id = template.Id,
			AcademicMediumId = template.AcademicMediumId,
			TemplateName = template.TemplateName,
			TargetRole = template.TargetRole,
			Category = template.Category,
			Subject = template.Subject,
			Body = template.Body,
			LanguageCode = template.LanguageCode,
			Channels = Deserialize(template.ChannelsJson, new List<CommunicationChannelType>()),
			Variables = Deserialize(template.VariablesJson, new List<string>()),
			Hashtags = Deserialize(template.HashtagsJson, new List<string>()),
			ApprovalStatus = template.ApprovalStatus,
			Version = template.Version,
			IsActive = template.IsActive
		};
	}

	private static ChannelProviderSettingDto MapProviderSetting(ChannelProviderSetting setting)
	{
		return new ChannelProviderSettingDto
		{
			Id = setting.Id,
			Channel = setting.Channel,
			ProviderName = setting.ProviderName,
			SenderId = setting.SenderId,
			IsEnabled = setting.IsEnabled,
			IsDefault = setting.IsDefault,
			ConfigurationJson = setting.ConfigurationJson,
			SecretReference = setting.SecretReference,
			FallbackOrder = Deserialize(setting.FallbackOrderJson, new List<CommunicationChannelType>()),
			DailyQuota = setting.DailyQuota,
			MonthlyQuota = setting.MonthlyQuota,
			ConsentRequired = setting.ConsentRequired,
			TemplateApprovalRequired = setting.TemplateApprovalRequired,
			Status = setting.Status
		};
	}

	private static ConsentRecordDto MapConsentRecord(ConsentRecord record)
	{
		return new ConsentRecordDto
		{
			Id = record.Id,
			RecipientType = record.RecipientType,
			RecipientId = record.RecipientId,
			DisplayName = record.DisplayName,
			Channel = record.Channel,
			Destination = record.Destination,
			Status = record.Status,
			Source = record.Source,
			ConsentCapturedAt = record.ConsentCapturedAt,
			RevokedAt = record.RevokedAt,
			Notes = record.Notes
		};
	}

	private static CommunicationApprovalRuleDto MapApprovalRule(CommunicationApprovalRule rule)
	{
		return new CommunicationApprovalRuleDto
		{
			Id = rule.Id,
			RuleName = rule.RuleName,
			IsEnabled = rule.IsEnabled,
			CampaignType = rule.CampaignType,
			SourceModule = rule.SourceModule,
			Channel = rule.Channel,
			MinimumAudienceSize = rule.MinimumAudienceSize,
			ImportanceLevel = rule.ImportanceLevel,
			RequiresApproval = rule.RequiresApproval,
			CreationTime = rule.CreationTime
		};
	}

	private static CommunicationRecipientSnapshotDto ToSampleRecipient(CommunicationExplicitRecipientDto recipient)
	{
		return new CommunicationRecipientSnapshotDto
		{
			RecipientType = recipient.RecipientType,
			RecipientId = recipient.RecipientId,
			UserId = recipient.UserId,
			DisplayName = recipient.DisplayName,
			Email = recipient.Email,
			MobileNumber = recipient.MobileNumber,
			PreferredLanguageCode = recipient.PreferredLanguageCode,
			IsParentDefault = recipient.IsParentDefault,
			IsEligible = !string.IsNullOrWhiteSpace(recipient.Email) || !string.IsNullOrWhiteSpace(recipient.MobileNumber) || recipient.UserId.HasValue,
			BlockedReason = string.IsNullOrWhiteSpace(recipient.Email) && string.IsNullOrWhiteSpace(recipient.MobileNumber) && !recipient.UserId.HasValue
				? "No channel destination is available."
				: null
		};
	}

	private static CommunicationRecipientSnapshotDto MapRecipientSnapshot(CommunicationRecipientSnapshot recipient)
	{
		return new CommunicationRecipientSnapshotDto
		{
			Id = recipient.Id,
			CampaignId = recipient.CampaignId,
			RecipientType = recipient.RecipientType,
			RecipientId = recipient.RecipientId,
			UserId = recipient.UserId,
			DisplayName = recipient.DisplayName,
			Email = recipient.Email,
			MobileNumber = recipient.MobileNumber,
			PreferredLanguageCode = recipient.PreferredLanguageCode,
			IsParentDefault = recipient.IsParentDefault,
			IsEligible = recipient.IsEligible,
			BlockedReason = recipient.BlockedReason
		};
	}

	private static DeliveryAttemptDto MapDeliveryAttempt(DeliveryAttempt attempt)
	{
		return new DeliveryAttemptDto
		{
			Id = attempt.Id,
			CampaignId = attempt.CampaignId,
			RecipientSnapshotId = attempt.RecipientSnapshotId,
			Channel = attempt.Channel,
			Destination = attempt.Destination,
			Status = attempt.Status,
			ProviderName = attempt.ProviderName,
			ProviderMessageId = attempt.ProviderMessageId,
			AttemptNumber = attempt.AttemptNumber,
			ErrorCode = attempt.ErrorCode,
			ErrorMessage = attempt.ErrorMessage,
			QueuedAt = attempt.QueuedAt,
			SentAt = attempt.SentAt,
			DeliveredAt = attempt.DeliveredAt,
			ReadAt = attempt.ReadAt,
			EstimatedCost = attempt.EstimatedCost,
			CurrencyCode = attempt.CurrencyCode
		};
	}

	private static CommunicationReplyDto MapReply(CommunicationReply reply)
	{
		return new CommunicationReplyDto
		{
			Id = reply.Id,
			CampaignId = reply.CampaignId,
			DeliveryAttemptId = reply.DeliveryAttemptId,
			RecipientSnapshotId = reply.RecipientSnapshotId,
			Channel = reply.Channel,
			FromAddress = reply.FromAddress,
			Body = reply.Body,
			ReceivedAt = reply.ReceivedAt,
			AssignedToUserId = reply.AssignedToUserId,
			AssignedDepartment = reply.AssignedDepartment,
			Status = reply.Status,
			InternalNotes = reply.InternalNotes
		};
	}

	private static UsageLedgerEntryDto MapUsageLedgerEntry(UsageLedgerEntry entry)
	{
		return new UsageLedgerEntryDto
		{
			Id = entry.Id,
			CampaignId = entry.CampaignId,
			Channel = entry.Channel,
			ProviderName = entry.ProviderName,
			UsageDate = entry.UsageDate,
			AttemptCount = entry.AttemptCount,
			DeliveredCount = entry.DeliveredCount,
			FailedCount = entry.FailedCount,
			EstimatedCost = entry.EstimatedCost,
			CurrencyCode = entry.CurrencyCode
		};
	}

	private async Task<Dictionary<CommunicationChannelType, ChannelProviderSetting>> GetEnabledProvidersAsync(List<CommunicationChannelType> channels)
	{
		var query = await _providerSettingRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			query
				.Where(x => channels.Contains(x.Channel) && x.IsEnabled && x.Status == CommunicationProviderStatus.Enabled)
				.OrderByDescending(x => x.IsDefault)
				.ThenByDescending(x => x.CreationTime));

		return rows
			.GroupBy(x => x.Channel)
			.ToDictionary(x => x.Key, x => x.First());
	}

	private async Task ValidateTemplateApprovalAsync(CommunicationCampaign campaign, IEnumerable<ChannelProviderSetting> providers)
	{
		if (!providers.Any(x => x.TemplateApprovalRequired))
		{
			return;
		}

		if (!campaign.TemplateId.HasValue)
		{
			throw new BusinessException("Communication:TemplateRequired");
		}

		var template = await _templateRepository.GetAsync(campaign.TemplateId.Value);
		if (template.ApprovalStatus != CommunicationTemplateApprovalStatus.Approved)
		{
			throw new BusinessException("Communication:TemplateApprovalRequired");
		}
	}

	private async Task<CommunicationAudiencePreviewDto> BuildAudiencePreviewAsync(CommunicationAudienceDefinitionDto input)
	{
		var resolution = await _audienceResolver.ResolveAsync(Guid.Empty, input);
		var total = resolution.Recipients.Count;
		var blocked = resolution.Recipients.Count(x => !x.IsEligible);

		return new CommunicationAudiencePreviewDto
		{
			GroupRecipientCount = resolution.GroupRecipientCount,
			ExplicitRecipientCount = resolution.ExplicitRecipientCount,
			FilterMatchedCount = resolution.FilterMatchedCount,
			TotalRecipientCount = total,
			EligibleRecipientCount = Math.Max(0, total - blocked),
			BlockedRecipientCount = blocked,
			ParentsFirst = input.ParentsFirst,
			CanPublish = total > 0 && resolution.BlockingIssues.Count == 0,
			Issues = resolution.Issues,
			BlockingIssues = resolution.BlockingIssues,
			SampleRecipients = resolution.Recipients.Take(10).Select(MapRecipientSnapshot).ToList()
		};
	}

	private async Task<bool> RequiresApprovalAsync(CommunicationCampaign campaign, int audienceSize)
	{
		var channels = Deserialize(campaign.ChannelsJson, new List<CommunicationChannelType>()).Distinct().ToList();
		var query = await _approvalRuleRepository.GetQueryableAsync();
		var rules = await AsyncExecuter.ToListAsync(query.Where(x => x.IsEnabled && x.RequiresApproval));

		return rules.Any(rule => ApprovalRuleMatches(rule, campaign, channels, audienceSize));
	}

	private static bool ApprovalRuleMatches(
		CommunicationApprovalRule rule,
		CommunicationCampaign campaign,
		List<CommunicationChannelType> channels,
		int audienceSize)
	{
		return (rule.CampaignType.IsNullOrWhiteSpace() || string.Equals(rule.CampaignType, campaign.CampaignType, StringComparison.OrdinalIgnoreCase))
			&& (!rule.SourceModule.HasValue || rule.SourceModule == campaign.SourceModule)
			&& (!rule.Channel.HasValue || channels.Contains(rule.Channel.Value))
			&& (!rule.MinimumAudienceSize.HasValue || audienceSize >= rule.MinimumAudienceSize.Value)
			&& (!rule.ImportanceLevel.HasValue || campaign.ImportanceLevel >= rule.ImportanceLevel.Value);
	}

	private async Task<bool> HasCampaignHistoryAsync(Guid campaignId)
	{
		var deliveryQuery = await _deliveryAttemptRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(deliveryQuery.Where(x => x.CampaignId == campaignId)))
		{
			return true;
		}

		var usageQuery = await _usageLedgerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(usageQuery.Where(x => x.CampaignId == campaignId)))
		{
			return true;
		}

		var replyQuery = await _replyRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(replyQuery.Where(x => x.CampaignId == campaignId)))
		{
			return true;
		}

		var recipientQuery = await _recipientSnapshotRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(recipientQuery.Where(x => x.CampaignId == campaignId));
	}

	private ICommunicationChannelGateway GetGateway(CommunicationChannelType channel, string providerName)
	{
		return _channelGateways.FirstOrDefault(x => x is not QueueOnlyCommunicationChannelGateway && x.CanHandle(channel, providerName))
			?? _channelGateways.FirstOrDefault(x => x.CanHandle(channel, providerName))
			?? throw new BusinessException("Communication:GatewayMissing")
				.WithData("Channel", channel.ToString())
				.WithData("ProviderName", providerName);
	}

	private static string ResolveDestination(CommunicationRecipientSnapshot recipient, CommunicationChannelType channel)
	{
		return channel switch
		{
			CommunicationChannelType.WhatsApp or CommunicationChannelType.Sms => recipient.MobileNumber ?? string.Empty,
			CommunicationChannelType.Email => recipient.Email ?? string.Empty,
			CommunicationChannelType.InApp => recipient.UserId?.ToString() ?? recipient.RecipientId?.ToString() ?? string.Empty,
			_ => string.Empty
		};
	}

	private async Task<bool> HasOptedInConsentAsync(CommunicationRecipientSnapshot recipient, CommunicationChannelType channel, string destination)
	{
		if (channel == CommunicationChannelType.InApp)
		{
			return true;
		}

		var query = await _consentRecordRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(query.Where(x =>
			x.Channel == channel
			&& x.Destination == destination
			&& x.Status == CommunicationConsentStatus.OptedIn
			&& (x.RecipientId == recipient.RecipientId || recipient.RecipientId == null)));
	}

	private async Task DeleteCampaignChildrenAsync(Guid campaignId)
	{
		await DeleteRowsAsync(_deliveryAttemptRepository, x => x.CampaignId == campaignId);
		await DeleteRowsAsync(_usageLedgerRepository, x => x.CampaignId == campaignId);
		await DeleteRowsAsync(_recipientSnapshotRepository, x => x.CampaignId == campaignId);
	}

	private async Task DeleteRowsAsync<TEntity>(IRepository<TEntity, Guid> repository, System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
		where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var query = await repository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(predicate));
		foreach (var row in rows)
		{
			await repository.DeleteAsync(row);
		}
	}

	private async Task ClearDefaultProviderAsync(CommunicationChannelType channel, Guid? exceptId = null)
	{
		var query = await _providerSettingRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.Channel == channel && x.IsDefault && x.Id != exceptId));
		foreach (var row in rows)
		{
			row.IsDefault = false;
			await _providerSettingRepository.UpdateAsync(row);
		}
	}

	private static void MarkAttemptSkipped(DeliveryAttempt attempt, string errorCode, string errorMessage)
	{
		attempt.Status = CommunicationDeliveryStatus.Skipped;
		attempt.ErrorCode = errorCode;
		attempt.ErrorMessage = errorMessage;
	}

	private Task CheckCampaignPolicyAsync(string? campaignType, string campaignPermission, string announcementPermission)
	{
		return CheckPolicyAsync(IsAnnouncementCampaign(campaignType) ? announcementPermission : campaignPermission);
	}

	private static bool IsAnnouncementCampaign(string? campaignType)
	{
		return string.Equals(campaignType, "Announcement", StringComparison.OrdinalIgnoreCase);
	}

	private static List<CommunicationExplicitRecipientDto> NormalizeExplicitRecipients(List<CommunicationExplicitRecipientDto> recipients)
	{
		return recipients
			.Where(x => !x.DisplayName.IsNullOrWhiteSpace() || x.RecipientId.HasValue || x.UserId.HasValue || !x.Email.IsNullOrWhiteSpace() || !x.MobileNumber.IsNullOrWhiteSpace())
			.Select(x => new CommunicationExplicitRecipientDto
			{
				RecipientType = x.RecipientType.IsNullOrWhiteSpace() ? "ParentGuardian" : x.RecipientType.Trim(),
				RecipientId = x.RecipientId,
				UserId = x.UserId,
				DisplayName = x.DisplayName.IsNullOrWhiteSpace() ? x.Email ?? x.MobileNumber ?? "Recipient" : x.DisplayName.Trim(),
				Email = x.Email?.Trim(),
				MobileNumber = x.MobileNumber?.Trim(),
				PreferredLanguageCode = x.PreferredLanguageCode.IsNullOrWhiteSpace() ? "en" : x.PreferredLanguageCode.Trim(),
				IsParentDefault = x.IsParentDefault
			})
			.ToList();
	}

	private static string BuildChannelEligibilityJson(string? email, string? mobileNumber, Guid? userId)
	{
		return Serialize(new Dictionary<CommunicationChannelType, bool>
		{
			[CommunicationChannelType.WhatsApp] = !mobileNumber.IsNullOrWhiteSpace(),
			[CommunicationChannelType.Sms] = !mobileNumber.IsNullOrWhiteSpace(),
			[CommunicationChannelType.Email] = !email.IsNullOrWhiteSpace(),
			[CommunicationChannelType.InApp] = userId.HasValue
		});
	}

	private async Task<decimal> SumEstimatedCostAsync(IQueryable<UsageLedgerEntry> query)
	{
		var rows = await AsyncExecuter.ToListAsync(query.Select(x => x.EstimatedCost));
		return rows.Sum();
	}

	private static string Serialize<T>(T value)
	{
		return JsonSerializer.Serialize(value, JsonOptions);
	}

	private static T Deserialize<T>(string? value, T fallback)
	{
		if (value.IsNullOrWhiteSpace())
		{
			return fallback;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? fallback;
		}
		catch (JsonException)
		{
			return fallback;
		}
	}
}
