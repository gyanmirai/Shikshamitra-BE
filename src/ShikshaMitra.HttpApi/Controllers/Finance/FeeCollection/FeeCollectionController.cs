using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.FeeCollection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.FeeCollection;

[Authorize]
[Area("finance")]
[Route("api/finance/fee-collection")]
public class FeeCollectionController(
	IFeeCollectionAppService feeCollectionAppService
) : AbpControllerBase
{
	// Get API to get all students in given academic medium id
	[HttpGet("students")]
	public Task<StudentFeeCollectionListDto> GetStudentsAsync([FromQuery] GetStudentsInput input)
	{
		return feeCollectionAppService.GetStudentsAsync(input);
	}

	// POST API to collect fee for a student
	[HttpPost("collect")]
	public Task<CollectFeeOutput> CollectFeeAsync([FromBody] CollectFeeInput input)
	{
		return feeCollectionAppService.CollectFeeAsync(input);
	}

	// GET API to get fee collection details for a student
	[HttpGet("{studentId}/student-fee-details")]
	public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync([FromRoute] Guid studentId, [FromQuery] GetStudentFeeDetailsInput input)
	{
		input.StudentId = studentId;
		return feeCollectionAppService.GetStudentFeeDetailsAsync(input);
	}

	// GET API to get comprehensive fee details for a student, including fee structure, concessions, and payment history
	[HttpGet("{studentId}/comprehensive-fee-details")]
	public Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync([FromRoute] Guid studentId, [FromQuery] GetComprehensiveFeeDetailsInput input)
	{
		input.StudentId = studentId;
		return feeCollectionAppService.GetComprehensiveFeeDetailsAsync(input);
	}
}
