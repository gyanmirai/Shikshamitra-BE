using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.StudentFeeAssignment;

[RemoteService]
[Area("Finance")]
[Route("api/student-fee-structure")]
[Authorize]
public class StudentFeeStructureController : AbpController
{
	private readonly IStudentFeeStructureAppService _studentFeeStructureAppService;

	public StudentFeeStructureController(IStudentFeeStructureAppService studentFeeStructureAppService)
	{
		_studentFeeStructureAppService = studentFeeStructureAppService;
	}

	/// <summary>
	/// Assign fee structure to a student
	/// </summary>
	[HttpPost("assign")]
	public Task<StudentFeeStructureResponseDto> AssignAsync([FromBody] AssignStudentFeeStructureDto input)
	{
		return _studentFeeStructureAppService.AssignAsync(input);
	}

	/// <summary>
	/// Get assigned fee structure of student
	/// </summary>
	[HttpGet("student/{studentId}")]
	public Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		return _studentFeeStructureAppService.GetByStudentIdAsync(studentId);
	}
}
