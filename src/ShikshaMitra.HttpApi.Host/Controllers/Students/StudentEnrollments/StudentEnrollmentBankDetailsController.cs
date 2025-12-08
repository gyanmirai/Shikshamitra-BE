using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student bank details during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/bank-details")]
public class StudentEnrollmentBankDetailsController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentBankDetailsController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets bank details information for a student in Registration status.
	/// Includes bank name, account number, IFSC code, branch details, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Bank details information</returns>
	/// <response code="200">Returns the bank details information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentBankDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentBankDetailsDto>> GetBankDetailsAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetBankDetailsAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates bank details for a student in Registration status.
	/// This includes banking information for fee payments, refunds, and financial transactions.
	/// Updates the registration state to BankDetails upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Bank details data</param>
	/// <returns>Updated bank details information</returns>
	/// <response code="200">Bank details updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentBankDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentBankDetailsDto>> UpdateBankDetailsAsync(
		Guid studentId,
		[FromBody] UpdateBankDetailsDto input)
	{
		var result = await _registrationAppService.UpdateBankDetailsAsync(studentId, input);
		return Result.Success(result);
	}
}
