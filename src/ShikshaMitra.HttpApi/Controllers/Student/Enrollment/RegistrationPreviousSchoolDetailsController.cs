using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles reading and writing of previous school details during registration.
/// Covers school name, academic board, TC number, academic performance, and transfer reason.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationPreviousSchoolDetailsController(
    IStudentPreviousSchoolDetailsAppService registrationAppService) : AbpControllerBase
{
    /// <summary>
    /// Gets previous school details for a student in Registration status.
    /// Includes school name, board, TC number, academic performance, etc.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <returns>Previous school details information</returns>
    /// <response code="200">Returns the previous school details</response>
    /// <response code="400">Student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpGet("{studentId}/previous-school-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentPreviousSchoolDetailsDto>> GetPreviousSchoolDetailsAsync(Guid studentId)
    {
        var result = await registrationAppService.GetPreviousSchoolDetailsAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Updates previous school details for a student in Registration status.
    /// Includes school information, academic board, TC number, grades, and reason for transfer.
    /// Updates the registration state to PrevSchoolDetails upon successful validation.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="input">Previous school details data</param>
    /// <returns>Updated previous school details information</returns>
    /// <response code="200">Previous school details updated successfully</response>
    /// <response code="400">Invalid data or student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpPut("{studentId}/previous-school-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentPreviousSchoolDetailsDto>> UpdatePreviousSchoolDetailsAsync(
        Guid studentId,
        [FromBody] UpdatePreviousSchoolDetailsDto input)
    {
        var result = await registrationAppService.UpdatePreviousSchoolDetailsAsync(studentId, input);
        return Result.Success(result);
    }
}
