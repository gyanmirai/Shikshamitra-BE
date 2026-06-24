using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Communication;

public interface ICommunicationAppService : IApplicationService
{
	Task<CommunicationDashboardDto> GetDashboardAsync();

	Task<PagedResultDto<CommunicationCampaignDto>> GetCampaignsAsync(GetCommunicationCampaignListRequestDto input);
	Task<CommunicationCampaignDto> GetCampaignAsync(Guid id);
	Task<CommunicationCampaignDto> CreateCampaignAsync(CreateUpdateCommunicationCampaignDto input);
	Task<CommunicationCampaignDto> UpdateCampaignAsync(Guid id, CreateUpdateCommunicationCampaignDto input);
	Task DeleteCampaignAsync(Guid id);
	Task<CommunicationAudiencePreviewDto> PreviewAudienceAsync(CommunicationAudienceDefinitionDto input);
	Task<CommunicationCampaignDto> SubmitForApprovalAsync(Guid id);
	Task<CommunicationCampaignDto> ApproveCampaignAsync(Guid id, CommunicationApprovalDto input);
	Task<CommunicationCampaignDto> RejectCampaignAsync(Guid id, CommunicationApprovalDto input);
	Task<CommunicationCampaignDto> PublishCampaignAsync(Guid id);
	Task<CommunicationCampaignDto> CancelCampaignAsync(Guid id);
	Task<CommunicationCampaignDto> CloneCampaignAsync(Guid id);

	Task<PagedResultDto<CommunicationTemplateDto>> GetTemplatesAsync(GetCommunicationTemplateListRequestDto input);
	Task<CommunicationTemplateDto> GetTemplateAsync(Guid id);
	Task<CommunicationTemplateDto> CreateTemplateAsync(CreateUpdateCommunicationTemplateDto input);
	Task<CommunicationTemplateDto> UpdateTemplateAsync(Guid id, CreateUpdateCommunicationTemplateDto input);
	Task DeleteTemplateAsync(Guid id);

	Task<PagedResultDto<ChannelProviderSettingDto>> GetProviderSettingsAsync(GetChannelProviderSettingListRequestDto input);
	Task<ChannelProviderSettingDto> GetProviderSettingAsync(Guid id);
	Task<ChannelProviderSettingDto> CreateProviderSettingAsync(CreateUpdateChannelProviderSettingDto input);
	Task<ChannelProviderSettingDto> UpdateProviderSettingAsync(Guid id, CreateUpdateChannelProviderSettingDto input);
	Task DeleteProviderSettingAsync(Guid id);

	Task<PagedResultDto<ConsentRecordDto>> GetConsentRecordsAsync(GetConsentRecordListRequestDto input);
	Task<ConsentRecordDto> UpsertConsentRecordAsync(CreateUpdateConsentRecordDto input);

	Task<PagedResultDto<CommunicationApprovalRuleDto>> GetApprovalRulesAsync(GetCommunicationApprovalRuleListRequestDto input);
	Task<CommunicationApprovalRuleDto> GetApprovalRuleAsync(Guid id);
	Task<CommunicationApprovalRuleDto> CreateApprovalRuleAsync(CreateUpdateCommunicationApprovalRuleDto input);
	Task<CommunicationApprovalRuleDto> UpdateApprovalRuleAsync(Guid id, CreateUpdateCommunicationApprovalRuleDto input);
	Task DeleteApprovalRuleAsync(Guid id);

	Task<PagedResultDto<DeliveryAttemptDto>> GetDeliveryAttemptsAsync(GetDeliveryAttemptListRequestDto input);
	Task<PagedResultDto<CommunicationReplyDto>> GetRepliesAsync(GetCommunicationReplyListRequestDto input);
	Task<CommunicationReplyDto> CaptureReplyAsync(CaptureCommunicationReplyDto input);
	Task<CommunicationReplyDto> AssignReplyAsync(Guid id, AssignCommunicationReplyDto input);

	Task<PagedResultDto<UsageLedgerEntryDto>> GetUsageLedgerAsync(GetUsageLedgerListRequestDto input);
	Task<List<CommunicationOptionDto>> GetChannelOptionsAsync();
	Task<List<CommunicationOptionDto>> GetImportanceLevelOptionsAsync();
}
