using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public interface IStudentFeeStructureAppService : IApplicationService
{
	Task<List<AssignmentPolicyDto>> GetPolicyOptionsAsync(Guid studentId, Guid academicYearId);
	Task<StudentFeeStructureResponseDto> UpdatePoliciesAsync(Guid studentId, AssignmentPolicyUpdateDto input);
	Task<List<AssignmentTransportSourceDto>> GetTransportSourcesAsync(Guid studentId, Guid academicYearId);
	Task<AssignmentCommitResultDto> AssignAsync(AssignStudentFeeStructureDto input);
    Task<List<AssignmentCandidateDto>> GetCandidatesAsync(Guid studentId, Guid academicYearId);
    Task<Volo.Abp.Application.Dtos.PagedResultDto<AssignmentStudentDto>> GetStudentsAsync(AssignmentStudentListInput input);
    Task<AssignmentBulkPreviewDto> PreviewBulkAsync(BulkAssignStudentFeeStructureDto input);

	Task<StudentFeeAssignmentPreviewDto> PreviewAsync(AssignStudentFeeStructureDto input);

	Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(BulkAssignStudentFeeStructureDto input);

	Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId, Guid academicYearId);
}
