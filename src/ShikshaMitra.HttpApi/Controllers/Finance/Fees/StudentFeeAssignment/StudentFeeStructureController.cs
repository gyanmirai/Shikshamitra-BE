using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.StudentFeeAssignment;

[RemoteService]
[Area("Finance")]
[Route("api/student-fee-structure")]
[Authorize]
public class StudentFeeStructureController(
	IStudentFeeStructureAppService studentFeeStructureAppService
) : AbpController
{
	/// <summary>
	/// Assign fee structure to a student
	/// </summary>
	[HttpPost("assign")]
	public async Task AssignAsync([FromBody] AssignStudentFeeStructureDto input)
	{
		await studentFeeStructureAppService.AssignAsync(input);
	}

	/// <summary>
	/// Get the fee structure assigned to a student
	/// </summary>
	[HttpGet("student/{studentId}")]
	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		return await studentFeeStructureAppService.GetByStudentIdAsync(studentId);
	}
}
