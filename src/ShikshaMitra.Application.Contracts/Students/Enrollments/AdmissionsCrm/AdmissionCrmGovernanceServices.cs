using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmRetentionPrivacyService : IApplicationService
{
	Task<AdmissionCrmRetentionPolicyDto> CreateRetentionPolicyAsync(Guid academicYearId, CreateAdmissionCrmRetentionPolicyDto input);
	Task<IReadOnlyList<AdmissionCrmRetentionPolicyDto>> GetRetentionPoliciesAsync(Guid academicYearId);
	Task<AdmissionCrmRetentionPolicyDto> SubmitRetentionPolicyAsync(Guid id);
	Task<AdmissionCrmRetentionPolicyDto> PublishRetentionPolicyAsync(Guid id);
	Task<AdmissionCrmPrivacyPreviewDto> PreviewPrivacyRequestAsync(Guid leadId, AdmissionCrmPrivacyRequestType type);
	Task<AdmissionCrmPrivacyRequestDto> CreatePrivacyRequestAsync(Guid leadId, CreateAdmissionCrmPrivacyRequestDto input);
	Task<AdmissionCrmPrivacyRequestDto> DecidePrivacyRequestAsync(Guid id, DecideAdmissionCrmPrivacyRequestDto input);
	Task<AdmissionCrmPrivacyRequestDto> CompletePrivacyRequestAsync(Guid id, CompleteAdmissionCrmPrivacyRequestDto input);
	Task<IReadOnlyList<AdmissionCrmPrivacyRequestDto>> GetPrivacyRequestsAsync(Guid academicYearId);
}

public interface IAdmissionCrmExportService : IApplicationService
{
	Task<AdmissionCrmExportRequestDto> RequestExportAsync(Guid academicYearId, RequestAdmissionCrmExportDto input);
	Task<IReadOnlyList<AdmissionCrmExportRequestDto>> GetExportsAsync(Guid academicYearId);
	Task<AdmissionCrmExportDownloadDto> DownloadExportAsync(Guid id);
}

public interface IAdmissionCrmReportScheduleService : IApplicationService
{
	Task<AdmissionCrmReportScheduleDto> CreateReportScheduleAsync(Guid academicYearId, CreateAdmissionCrmReportScheduleDto input);
	Task<IReadOnlyList<AdmissionCrmReportScheduleDto>> GetReportSchedulesAsync(Guid academicYearId);
	Task<AdmissionCrmReportScheduleDto> SetReportScheduleActiveAsync(Guid id, bool active);
	Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesAsync(Guid academicYearId);
}

public interface IAdmissionCrmDelegationService : IApplicationService
{
	Task<AdmissionCrmWorkDelegationDto> CreateDelegationAsync(Guid academicYearId, CreateAdmissionCrmWorkDelegationDto input);
	Task<IReadOnlyList<AdmissionCrmWorkDelegationDto>> GetDelegationsAsync(Guid academicYearId);
	Task<AdmissionCrmWorkDelegationDto> CancelDelegationAsync(Guid id);
}

public interface IAdmissionCrmAuditForecastService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmTimelineEventDto>> SearchAuditAsync(GetAdmissionCrmAuditInput input);
	Task<AdmissionCrmForecastDto> GetAdvisoryForecastAsync(Guid academicYearId);
}
