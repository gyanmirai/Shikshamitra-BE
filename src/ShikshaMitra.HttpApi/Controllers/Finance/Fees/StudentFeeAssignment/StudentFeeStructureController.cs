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
	[HttpPost("assign")]
	public async Task AssignAsync([FromBody] AssignStudentFeeStructureDto input)
	{
		await studentFeeStructureAppService.AssignAsync(input);
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

