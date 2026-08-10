using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[AllowAnonymous]
[RemoteService(false)]
[ExposeServices(typeof(IAdmissionCrmPublicDeliveryCallbackService), IncludeSelf = true)]
public class AdmissionCrmPublicDeliveryCallbackAppService(
	AdmissionCrmPublicDeliveryCallbackWorkflow callbackWorkflow) : ApplicationService, IAdmissionCrmPublicDeliveryCallbackService
{
	public Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackAsync(
		Guid tenantId, Guid deliveryLogId, ApplyAdmissionCrmDeliveryCallbackDto input) =>
		callbackWorkflow.ApplyAsync(tenantId, deliveryLogId, input);
	public Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackByProviderMessageAsync(
		Guid tenantId, Guid academicYearId, AdmissionCrmMessageChannel channel, ApplyAdmissionCrmDeliveryCallbackDto input) =>
		callbackWorkflow.ApplyByProviderMessageAsync(tenantId, academicYearId, channel, input);
}
