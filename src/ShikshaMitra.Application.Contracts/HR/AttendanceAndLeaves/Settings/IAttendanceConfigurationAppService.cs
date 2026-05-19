using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface IAttendanceConfigurationAppService : IApplicationService
{
	Task<AttendanceConfigurationResponse> GetAsync(Guid academicYearId);
	Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(Guid academicYearId, AttendanceConfigurationRequest input);
	Task<AttendanceConfigurationPreviewResponse> PreviewAsync(Guid academicYearId, AttendanceConfigurationPreviewRequest input);
	Task<AttendancePolicyEvaluationRunResponse> PreviewEvaluationAsync(AttendancePolicyEvaluationRequest input);
	Task<AttendancePolicyEvaluationRunResponse> CreateEvaluationAsync(AttendancePolicyEvaluationRequest input);
	Task<List<AttendancePolicyEvaluationRunResponse>> GetEvaluationsAsync(Guid academicYearId, int month, int year);
	Task<AttendancePolicyEvaluationRunResponse> GetEvaluationAsync(Guid runId);
	Task<PagedResultDto<AttendancePolicyEvaluationResultResponse>> GetEvaluationResultsAsync(Guid runId, AttendancePolicyEvaluationResultsRequest input);
	Task<AttendancePolicyEvaluationRunResponse> ApproveEvaluationAsync(Guid runId);
	Task<AttendancePolicyEvaluationRunResponse> PublishEvaluationAsync(Guid runId);
	Task<AttendancePolicyEvaluationRunResponse> CancelEvaluationAsync(Guid runId);
}
