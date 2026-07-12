using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmFinalAdmissionAppService : IApplicationService
{
	Task<AdmissionCrmSeatPoolDto> CreateSeatPoolAsync(Guid academicYearId, UpsertAdmissionCrmSeatPoolDto input);
	Task<AdmissionCrmSeatPoolDto> UpdateSeatPoolAsync(Guid id, UpsertAdmissionCrmSeatPoolDto input);
	Task<PagedResultDto<AdmissionCrmSeatPoolDto>> GetSeatPoolsAsync(Guid academicYearId, GetAdmissionCrmSeatPoolListInput input);
	Task<AdmissionCrmSeatOfferDto> CreateOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input);
	Task<AdmissionCrmSeatOfferPreviewDto> PreviewOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input);
	Task<PagedResultDto<AdmissionCrmSeatOfferDto>> GetOffersAsync(Guid academicYearId, GetAdmissionCrmSeatOfferListInput input);
	Task<AdmissionCrmSeatOfferDto> GetOfferAsync(Guid id);
	Task<AdmissionCrmSeatOfferDto> SubmitOfferAsync(Guid id);
	Task<AdmissionCrmSeatOfferDto> ApproveOfferAsync(Guid id);
	Task<AdmissionCrmSeatOfferDto> IssueOfferAsync(Guid id, IssueAdmissionCrmSeatOfferDto input);
	Task<AdmissionCrmSeatOfferDto> AcceptOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input);
	Task<AdmissionCrmSeatOfferDto> DeclineOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input);
	Task<AdmissionCrmSeatOfferDto> RevokeOfferAsync(Guid id, AdmissionCrmReasonDto input);
	Task<AdmissionCrmSeatOfferDto> SupersedeOfferAsync(Guid id, SupersedeAdmissionCrmSeatOfferDto input);
	Task<AdmissionCrmSeatOfferDto> ExpireOfferAsync(Guid id);
	Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryAsync(Guid academicYearId);
	Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryFromAutomationAsync(Guid academicYearId);
	Task<AdmissionCrmWaitlistEntryDto> AddToWaitlistAsync(Guid leadId, CreateAdmissionCrmWaitlistEntryDto input);
	Task<IReadOnlyList<AdmissionCrmWaitlistEntryDto>> GetWaitlistAsync(Guid seatPoolId);
	Task<AdmissionCrmSeatOfferDto> PrepareWaitlistPromotionAsync(Guid entryId, PrepareAdmissionCrmWaitlistPromotionDto input);
	Task<AdmissionCrmWaitlistEntryDto> WithdrawFromWaitlistAsync(Guid entryId, AdmissionCrmReasonDto input);
	Task<AdmissionCrmWaitlistEntryDto> UpdateWaitlistGovernanceAsync(Guid entryId, UpdateAdmissionCrmWaitlistGovernanceDto input);
	Task<IReadOnlyList<AdmissionCrmWaitlistHistoryDto>> GetWaitlistHistoryAsync(Guid entryId);
	Task<AdmissionCrmWaitlistPromotionRunResultDto> RunWaitlistPromotionAsync(Guid academicYearId, RunAdmissionCrmWaitlistPromotionDto input);
	Task<AdmissionCrmAdmissionDecisionDto> RecordDecisionAsync(Guid leadId, RecordAdmissionCrmAdmissionDecisionDto input);
	Task<AdmissionCrmAdmissionDecisionDto?> GetDecisionAsync(Guid leadId);
	Task<IReadOnlyList<AdmissionCrmAdmissionDecisionDto>> GetDecisionHistoryAsync(Guid leadId);
}
