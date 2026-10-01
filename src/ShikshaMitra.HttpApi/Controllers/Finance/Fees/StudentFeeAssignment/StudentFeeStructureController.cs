using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.StudentFeeAssignment;

[RemoteService]
[Area("finance")]
[Route("api/student-fee-structure")]
[Authorize]
public class StudentFeeStructureController(
	IStudentFeeStructureAppService studentFeeStructureAppService
) : AbpController
{
	[HttpGet("policy-options")]
	public Task<List<AssignmentPolicyDto>> GetPolicyOptionsAsync(Guid studentId, Guid academicYearId) => studentFeeStructureAppService.GetPolicyOptionsAsync(studentId, academicYearId);

	[HttpPut("student/{studentId}/policies")]
	public Task<StudentFeeStructureResponseDto> UpdatePoliciesAsync(Guid studentId, [FromBody] AssignmentPolicyUpdateDto input) => studentFeeStructureAppService.UpdatePoliciesAsync(studentId, input);
	[HttpGet("transport-sources")]
	public Task<List<AssignmentTransportSourceDto>> GetTransportSourcesAsync(Guid studentId, Guid academicYearId) => studentFeeStructureAppService.GetTransportSourcesAsync(studentId, academicYearId);
    [HttpGet("students")]
    public Task<Volo.Abp.Application.Dtos.PagedResultDto<AssignmentStudentDto>> GetStudentsAsync([FromQuery] AssignmentStudentListInput input) => studentFeeStructureAppService.GetStudentsAsync(input);

    [HttpGet("candidates")]
    public Task<List<AssignmentCandidateDto>> GetCandidatesAsync(Guid studentId, Guid academicYearId) => studentFeeStructureAppService.GetCandidatesAsync(studentId, academicYearId);

    [HttpPost("preview-bulk")]
    public Task<AssignmentBulkPreviewDto> PreviewBulkAsync([FromBody] BulkAssignStudentFeeStructureDto input) => studentFeeStructureAppService.PreviewBulkAsync(input);

	[HttpPost("assign")]
	public async Task<AssignmentCommitResultDto> AssignAsync([FromBody] AssignStudentFeeStructureDto input)
	{
		return await studentFeeStructureAppService.AssignAsync(input);
	}

	[HttpPost("preview")]
	public Task<StudentFeeAssignmentPreviewDto> PreviewAsync([FromBody] AssignStudentFeeStructureDto input) =>
		studentFeeStructureAppService.PreviewAsync(input);

	[HttpPost("assign-bulk")]
	public async Task<BulkAssignStudentFeeStructureResultDto> BulkAssignAsync(
		[FromBody] BulkAssignStudentFeeStructureDto input)
	{
		return await studentFeeStructureAppService.BulkAssignAsync(input);
	}

	[HttpGet("student/{studentId}")]
	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId, [FromQuery] Guid academicYearId)
	{
		return await studentFeeStructureAppService.GetByStudentIdAsync(studentId, academicYearId);
	}
}

