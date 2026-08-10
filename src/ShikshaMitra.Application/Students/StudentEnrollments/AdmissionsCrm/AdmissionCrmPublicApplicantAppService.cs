using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[AllowAnonymous]
[RemoteService(false)]
[ExposeServices(typeof(IAdmissionCrmPublicApplicantService), IncludeSelf = true)]
public class AdmissionCrmPublicApplicantAppService(
	AdmissionCrmPublicRegistrationWorkflow registrationWorkflow,
	AdmissionCrmPublicExamWorkflow examWorkflow,
	AdmissionCrmPublicEventWorkflow eventWorkflow,
	AdmissionCrmPublicOfferWorkflow offerWorkflow) : ApplicationService, IAdmissionCrmPublicApplicantService
{
	public Task<AdmissionCrmRegistrationApplicationDto> SaveRegistrationAsync(
		Guid tenantId, string token, SavePublicAdmissionCrmRegistrationDto input) =>
		registrationWorkflow.SaveAsync(tenantId, token, input);
	public Task<AdmissionCrmRegistrationApplicationDto> SubmitRegistrationAsync(
		Guid tenantId, string token, SavePublicAdmissionCrmRegistrationDto input) =>
		registrationWorkflow.SubmitAsync(tenantId, token, input);
	public Task<AdmissionCrmApplicantDocumentChecklistDto> UploadRegistrationDocumentAsync(
		Guid tenantId, string token, string accessSessionToken, ProcessedAdmissionCrmApplicantDocumentUploadDto input) =>
		registrationWorkflow.UploadDocumentAsync(tenantId, token, accessSessionToken, input);
	public Task<AdmissionCrmPublicExamSlotListDto> GetAvailableExamSlotsAsync(
		Guid tenantId, string token, string accessSessionToken) =>
		examWorkflow.GetAvailableSlotsAsync(tenantId, token, accessSessionToken);
	public Task<AdmissionCrmEntranceExamDto> ScheduleExamAsync(
		Guid tenantId, string token, ScheduleAdmissionCrmPublicExamDto input) =>
		examWorkflow.ScheduleAsync(tenantId, token, input);
	public Task<AdmissionCrmPublicEventListDto> GetAvailableEventsAsync(
		Guid tenantId, string token, string accessSessionToken) =>
		eventWorkflow.GetAvailableAsync(tenantId, token, accessSessionToken);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionCrmPublicEventDto> ReserveEventAsync(
		Guid tenantId, string token, Guid eventId, ReserveAdmissionCrmPublicEventDto input) =>
		eventWorkflow.ReserveAsync(tenantId, token, eventId, input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionCrmPublicEventDto> CancelEventRegistrationAsync(
		Guid tenantId, string token, Guid eventId, CancelAdmissionCrmPublicEventRegistrationDto input) =>
		eventWorkflow.CancelAsync(tenantId, token, eventId, input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionApplicantStatusOfferDto> RespondToOfferAsync(
		Guid tenantId, string token, Guid offerId, RespondAdmissionApplicantOfferDto input) =>
		offerWorkflow.RespondAsync(tenantId, token, offerId, input);
}
