using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmCommunicationProviderService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmCommunicationProviderDto>> GetProvidersAsync(Guid academicYearId);
	Task<AdmissionCrmCommunicationProviderDto> UpdateProviderAsync(Guid academicYearId, AdmissionCrmMessageChannel channel, UpdateAdmissionCrmCommunicationProviderDto input);
}

public interface IAdmissionCrmCommunicationOptOutService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmCommunicationOptOutDto>> GetOptOutsAsync(Guid academicYearId, GetAdmissionCrmCommunicationOptOutListInput input);
	Task<AdmissionCrmCommunicationOptOutDto> CreateOptOutAsync(Guid academicYearId, CreateAdmissionCrmCommunicationOptOutDto input);
	Task<AdmissionCrmCommunicationOptOutDto> ClearOptOutAsync(Guid academicYearId, ClearAdmissionCrmCommunicationOptOutDto input);
}

public interface IAdmissionCrmMessageDeliveryService : IApplicationService
{
	Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateAsync(Guid leadId, SendAdmissionCrmMessageTemplateDto input);
	Task<AdmissionCrmMessageTemplatePreviewDto> PreviewTemplateAsync(Guid academicYearId, PreviewAdmissionCrmMessageTemplateDto input);
	Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetDeliveryLogsAsync(Guid academicYearId, GetAdmissionCrmMessageDeliveryLogListInput input);
	Task<AdmissionCrmMessageDeliveryLogDto> RetryDeliveryAsync(Guid deliveryLogId, RetryAdmissionCrmMessageDeliveryDto input);
}
