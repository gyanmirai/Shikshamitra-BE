using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize, RemoteService(false)]
public class AdmissionCrmSeatPoolService(AdmissionCrmSeatPoolWorkflow workflow) : ApplicationService, IAdmissionCrmSeatPoolService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public Task<AdmissionCrmSeatPoolDto> CreateSeatPoolAsync(Guid yearId, UpsertAdmissionCrmSeatPoolDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public Task<AdmissionCrmSeatPoolDto> UpdateSeatPoolAsync(Guid id, UpsertAdmissionCrmSeatPoolDto input) => workflow.UpdateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public Task<PagedResultDto<AdmissionCrmSeatPoolDto>> GetSeatPoolsAsync(Guid yearId, GetAdmissionCrmSeatPoolListInput input) => workflow.GetListAsync(yearId, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmSeatOfferService(
	AdmissionCrmOfferDraftWorkflow drafts,
	AdmissionCrmOfferLifecycleWorkflow lifecycle,
	AdmissionCrmOfferExpiryWorkflow expiry,
	StudentScopeAccessService scopeAccess) : ApplicationService, IAdmissionCrmSeatOfferService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> CreateOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input) => drafts.CreateAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferPreviewDto> PreviewOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input) => drafts.PreviewAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmSeatOfferDto>> GetOffersAsync(Guid yearId, GetAdmissionCrmSeatOfferListInput input) => drafts.GetListAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmSeatOfferDto> GetOfferAsync(Guid id) => drafts.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> SubmitOfferAsync(Guid id) => lifecycle.SubmitAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers)]
	public Task<AdmissionCrmSeatOfferDto> ApproveOfferAsync(Guid id) => lifecycle.ApproveAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatReservations)]
	public Task<AdmissionCrmSeatOfferDto> IssueOfferAsync(Guid id, IssueAdmissionCrmSeatOfferDto input) => lifecycle.IssueAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> AcceptOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input) => lifecycle.AcceptAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> DeclineOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input) => lifecycle.DeclineAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> RevokeOfferAsync(Guid id, AdmissionCrmReasonDto input) => lifecycle.RevokeAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> SupersedeOfferAsync(Guid id, SupersedeAdmissionCrmSeatOfferDto input) => lifecycle.SupersedeAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> ExpireOfferAsync(Guid id) => lifecycle.ExpireAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryAsync(Guid yearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, nameof(RunOfferExpiryAsync));
		return await expiry.RunAsync(yearId, true);
	}
}

[Authorize, RemoteService(false)]
public class AdmissionCrmWaitlistService(
	AdmissionCrmWaitlistCommandWorkflow commands,
	AdmissionCrmWaitlistQueryWorkflow queries,
	AdmissionCrmWaitlistPromotionWorkflow promotion) : ApplicationService, IAdmissionCrmWaitlistService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistEntryDto> AddToWaitlistAsync(Guid leadId, CreateAdmissionCrmWaitlistEntryDto input) => commands.AddAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public Task<IReadOnlyList<AdmissionCrmWaitlistEntryDto>> GetWaitlistAsync(Guid poolId) => queries.GetAsync(poolId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmSeatOfferDto> PrepareWaitlistPromotionAsync(Guid id, PrepareAdmissionCrmWaitlistPromotionDto input) => promotion.PrepareAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistEntryDto> WithdrawFromWaitlistAsync(Guid id, AdmissionCrmReasonDto input) => commands.WithdrawAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistEntryDto> UpdateWaitlistGovernanceAsync(Guid id, UpdateAdmissionCrmWaitlistGovernanceDto input) => commands.UpdateGovernanceAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public Task<IReadOnlyList<AdmissionCrmWaitlistHistoryDto>> GetWaitlistHistoryAsync(Guid id) => queries.GetHistoryAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistPromotionRunResultDto> RunWaitlistPromotionAsync(Guid yearId, RunAdmissionCrmWaitlistPromotionDto input) => promotion.RunAsync(yearId, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmAdmissionDecisionService(
	AdmissionCrmAdmissionDecisionWorkflow commands,
	AdmissionCrmAdmissionDecisionQueryWorkflow queries) : ApplicationService, IAdmissionCrmAdmissionDecisionService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision)]
	public Task<AdmissionCrmAdmissionDecisionDto> RecordDecisionAsync(Guid leadId, RecordAdmissionCrmAdmissionDecisionDto input) => commands.RecordAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmAdmissionDecisionDto?> GetDecisionAsync(Guid leadId) => queries.GetAsync(leadId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionDecisionDto>> GetDecisionHistoryAsync(Guid leadId) => queries.GetHistoryAsync(leadId);
}
