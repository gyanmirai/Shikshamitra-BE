using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmStructuredReviewAppService : IApplicationService
{
	Task<AdmissionCrmReviewConfigurationDto> GetConfigurationAsync(Guid academicYearId);
	Task<AdmissionCrmReviewConfigurationDto> UpdateConfigurationAsync(Guid academicYearId, UpdateAdmissionCrmReviewConfigurationDto input);
	Task<AdmissionCrmStructuredReviewDto?> GetByLeadAsync(Guid leadId);
	Task<IReadOnlyList<AdmissionCrmReviewAssignmentDto>> GetMyAssignmentsAsync(Guid academicYearId);
	Task<AdmissionCrmStructuredReviewDto> StartAsync(Guid leadId);
	Task<AdmissionCrmStructuredReviewDto> AssignAsync(Guid reviewId, AssignAdmissionCrmReviewerDto input);
	Task<AdmissionCrmReviewAssignmentDto> DeclareConflictAsync(Guid assignmentId, DeclareAdmissionCrmReviewConflictDto input);
	Task<AdmissionCrmReviewAssignmentDto> SubmitAsync(Guid assignmentId, SubmitAdmissionCrmStructuredReviewDto input);
	Task<AdmissionCrmStructuredReviewDto> CloseAsync(Guid reviewId, CloseAdmissionCrmStructuredReviewDto input);
}
