using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.StudentFeeAssignment;

[RemoteService]
[Area("Finance")]
[Route("api/student-fee-structure")]
[Authorize]
public class StudentFeeStructureController(
	IStudentFeeStructureAppService _studentFeeStructureAppService
) : AbpController
{
	/// <summary>
	/// Assign fee structure to a student
	/// </summary>
	[HttpPost("assign")]
	public async Task AssignAsync([FromBody] AssignStudentFeeStructureDto input)
	{
		await _studentFeeStructureAppService.AssignAsync(input);
	}

	/// <summary>
	/// Get assigned fee structure of student
	/// </summary>
	//[HttpGet("student/{studentId}")]
	//public Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	//{
	//	//return _studentFeeStructureAppService.GetByStudentIdAsync(studentId);
	//}
}
