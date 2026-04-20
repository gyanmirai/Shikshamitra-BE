using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.FeeCollection;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.FeeCollection;

/// <summary>
/// Manages fee collection transactions for students.
/// </summary>
[Authorize]
[ApiController]
[Area("finance")]
[Route("api/fee-collections")]
public class FeeCollectionController(
	IFeeCollectionAppService feeCollectionAppService
) : AbpControllerBase
{
	/// <summary>
	/// Records a fee payment for a student with support for multiple payment
	/// methods and fee heads.
	/// </summary>
	/// <remarks>
	/// Required roles: ADMIN, PRINCIPAL, ACCOUNTANT<br/>
	/// Required permission: fee.collection.create
	/// </remarks>
	/// <response code="201">Fee collected successfully.</response>
	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	[ProducesResponseType(typeof(CollectFeeOutput), StatusCodes.Status201Created)]
	public async Task<IActionResult> CollectFeeAsync([FromBody] CollectFeeInput input)
	{
		var result = await feeCollectionAppService.CollectFeeAsync(input);

		return CreatedAtAction(
			actionName: nameof(GetStudentFeeDetailsAsync),
			routeValues: new { studentId = result.StudentId },
			value: result);
	}

	/// <summary>
	/// Retrieves a paginated list of students with their fee collection status.
	/// </summary>
	[HttpGet("students")]
	public Task<StudentFeeCollectionListDto> GetStudentsAsync([FromQuery] GetStudentsInput input)
	{
		return feeCollectionAppService.GetStudentsAsync(input);
	}

	/// <summary>
	/// Retrieves fee collection details for a specific student in an academic year.
	/// </summary>
	[HttpGet("{studentId:guid}/fee-details")]
	public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		[FromRoute] Guid studentId,
		[FromQuery] GetStudentFeeDetailsInput input)
	{
		input.StudentId = studentId;
		return feeCollectionAppService.GetStudentFeeDetailsAsync(input);
	}

	/// <summary>
	/// Retrieves comprehensive fee details for a student, including fee structure,
	/// concessions, and full payment history.
	/// </summary>
	[HttpGet("{studentId:guid}/comprehensive-fee-details")]
	public Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(
		[FromRoute] Guid studentId,
		[FromQuery] GetComprehensiveFeeDetailsInput input)
	{
		input.StudentId = studentId;
		return feeCollectionAppService.GetComprehensiveFeeDetailsAsync(input);
	}
}

