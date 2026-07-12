using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmSpecialRouteAppService : IApplicationService
{
	Task<AdmissionCrmRouteDefinitionDto> CreateDefinitionAsync(Guid academicYearId, CreateAdmissionCrmRouteDefinitionDto input);
	Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetDefinitionsAsync(Guid academicYearId);
	Task<AdmissionCrmRouteDefinitionDto> SubmitDefinitionAsync(Guid id);
	Task<AdmissionCrmRouteDefinitionDto> PublishDefinitionAsync(Guid id);
	Task<AdmissionCrmRouteAssignmentDto> AssignAsync(Guid leadId, AssignAdmissionCrmSpecialRouteDto input);
	Task<AdmissionCrmRouteImpactPreviewDto> PreviewAssignmentAsync(Guid leadId, AssignAdmissionCrmSpecialRouteDto input);
	Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId);
	Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(Guid id, EvaluateAdmissionCrmSpecialRouteDto input);
	Task<AdmissionCrmRteAllotmentImportResultDto> PreviewRteAllotmentsAsync(Guid academicYearId, ImportAdmissionCrmRteAllotmentsDto input);
	Task<AdmissionCrmRteAllotmentImportResultDto> ImportRteAllotmentsAsync(Guid academicYearId, ImportAdmissionCrmRteAllotmentsDto input);
	Task<AdmissionCrmRouteAppealDto> RequestAppealAsync(Guid assignmentId, RequestAdmissionCrmRouteAppealDto input);
	Task<AdmissionCrmRouteAppealDto> DecideAppealAsync(Guid appealId, DecideAdmissionCrmRouteAppealDto input);
	Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetAppealsAsync(Guid leadId);
}
