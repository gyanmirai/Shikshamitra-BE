using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmActivationAppService : IApplicationService
{
	Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid academicYearId, CreateAdmissionCrmNumberSeriesDto input);
	Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid academicYearId);
	Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid academicYearId);
	Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id);
	Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id);
	Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(Guid activationId, AssignManualAdmissionCrmIdentifierDto input);
	Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(Guid academicYearId, Guid numberSeriesId);
	Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input);
	Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, RequestAdmissionCrmActivationDto input);
	Task<AdmissionCrmActivationDto> ApproveAsync(Guid id);
	Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id);
	Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id);
	Task<AdmissionCrmActivationDto> RequestCancellationAsync(Guid id, RequestAdmissionCrmActivationCancellationDto input);
	Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id);
	Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId);
	Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid activationId);
	Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(Guid id, UpdateAdmissionCrmServiceHandoffDto input);
}
