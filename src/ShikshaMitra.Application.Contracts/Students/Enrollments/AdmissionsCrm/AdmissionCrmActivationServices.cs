using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmNumberSeriesService : IApplicationService
{
	Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid academicYearId, CreateAdmissionCrmNumberSeriesDto input);
	Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid academicYearId);
	Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid academicYearId);
	Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id);
	Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id);
}

public interface IAdmissionCrmIdentifierService : IApplicationService
{
	Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(Guid activationId, AssignManualAdmissionCrmIdentifierDto input);
	Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(Guid academicYearId, Guid numberSeriesId);
}

public interface IAdmissionCrmActivationLifecycleService : IApplicationService
{
	Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input);
	Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, RequestAdmissionCrmActivationDto input);
	Task<AdmissionCrmActivationDto> ApproveAsync(Guid id);
	Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId);
}

public interface IAdmissionCrmActivationExecutionService : IApplicationService
{
	Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id);
}

public interface IAdmissionCrmActivationCancellationService : IApplicationService
{
	Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id);
	Task<AdmissionCrmActivationDto> RequestCancellationAsync(Guid id, RequestAdmissionCrmActivationCancellationDto input);
	Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id);
}

public interface IAdmissionCrmServiceHandoffService : IApplicationService
{
	Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid activationId);
	Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(Guid id, UpdateAdmissionCrmServiceHandoffDto input);
}
