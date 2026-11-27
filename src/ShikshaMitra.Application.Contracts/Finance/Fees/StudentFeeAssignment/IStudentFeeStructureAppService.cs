using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public interface IStudentFeeStructureAppService : IApplicationService
{
	Task<List<AssignmentContextDto>> GetContextsAsync(AssignmentContextsInput input);
	Task<AssignmentHistoryPage> GetHistoryAsync(Guid studentId, AssignmentHistoryRequest input);
	Task<VariableFeeChangeResult> PreviewVariableFeeChangeAsync(Guid studentId, VariableFeeChangeInput input);
	Task<VariableFeeChangeResult> CommitVariableFeeChangeAsync(Guid studentId, VariableFeeChangeInput input);
	Task<List<AssignmentPolicyDto>> GetPolicyOptionsAsync(Guid studentId, Guid academicYearId);
	Task<StudentFeeStructureResponseDto> UpdatePoliciesAsync(Guid studentId, AssignmentPolicyUpdateDto input);
	Task<OptionalServiceStopResult> PreviewOptionalServiceStopAsync(Guid studentId, OptionalServiceStopInput input);
	Task<OptionalServiceStopResult> StopOptionalServiceAsync(Guid studentId, OptionalServiceStopInput input);
	Task<OptionalServiceRestartContext> GetOptionalServiceRestartContextAsync(Guid studentId, Guid academicYearId, Guid feeHeadId);
	Task<OptionalServiceRestartResult> PreviewOptionalServiceRestartAsync(Guid studentId, OptionalServiceRestartInput input);
	Task<OptionalServiceRestartResult> RestartOptionalServiceAsync(Guid studentId, OptionalServiceRestartInput input);
	Task<List<AssignmentTransportSourceDto>> GetTransportSourcesAsync(Guid studentId, Guid academicYearId);
	Task<AssignmentCommitResultDto> AssignAsync(AssignStudentFeeStructureDto input);
    Task<List<AssignmentCandidateDto>> GetCandidatesAsync(Guid studentId, Guid academicYearId);
    Task<Volo.Abp.Application.Dtos.PagedResultDto<AssignmentStudentDto>> GetStudentsAsync(AssignmentStudentListInput input);
    Task<AssignmentBulkPreviewDto> PreviewBulkAsync(BulkAssignStudentFeeStructureDto input);

	Task<StudentFeeAssignmentPreviewDto> PreviewAsync(AssignStudentFeeStructureDto input);

	Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input);

	Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId, Guid academicYearId);
}
