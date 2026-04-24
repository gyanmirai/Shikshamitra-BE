using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.FeeCollection;

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
	/// Retrieves comprehensive fee collection details for a specific student fee record,
	/// including student info, fee summary, fee heads breakdown, and charges.
	/// </summary>
	/// <remarks>
	/// Required roles: ADMIN, PRINCIPAL, ACCOUNTANT, TEACHER, PARENT<br/>
	/// Required permission: fee.collection.read
	/// </remarks>
	/// <response code="200">Fee collection details retrieved successfully.</response>
	[HttpGet("{studentFeeId:guid}")]
	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	[ProducesResponseType(typeof(FeeCollectionDetailsDto), StatusCodes.Status200OK)]
	public Task<FeeCollectionDetailsDto> GetFeeCollectionDetailsAsync([FromRoute] Guid studentFeeId)
	{
		return feeCollectionAppService.GetFeeCollectionDetailsAsync(studentFeeId);
	}

	/// <summary>
	/// Returns a paginated list of students with their fee collection status.
	/// </summary>
	/// <remarks>
	/// Required roles: ADMIN, PRINCIPAL, ACCOUNTANT, TEACHER, PARENT<br/>
	/// Required permission: fee.collection.list<br/>
	/// Filters by academic year, board, and medium (all required).
	/// Supports search by student name or roll number.
	/// Returns overdue fees highlighted for quick action.
	/// </remarks>
	/// <response code="200">Fee collection list retrieved successfully.</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	[ProducesResponseType(typeof(StudentFeeCollectionListDto), StatusCodes.Status200OK)]
	public Task<StudentFeeCollectionListDto> GetStudentsAsync([FromQuery] GetStudentsInput input)
	{
		return feeCollectionAppService.GetStudentsAsync(input);
	}

	/// <summary>
	/// Returns complete fee information for a student in an academic year,
	/// including fee heads breakdown, payment history, and applied concessions.
	/// </summary>
	/// <remarks>
	/// Required roles: ADMIN, PRINCIPAL, ACCOUNTANT, TEACHER, PARENT, STUDENT<br/>
	/// Required permission: fee.details.read
	/// </remarks>
	/// <response code="200">Fee details retrieved successfully.</response>
	[HttpGet("{studentId:guid}/fee-details")]
	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	[ProducesResponseType(typeof(StudentFeeDetailsDto), StatusCodes.Status200OK)]
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

