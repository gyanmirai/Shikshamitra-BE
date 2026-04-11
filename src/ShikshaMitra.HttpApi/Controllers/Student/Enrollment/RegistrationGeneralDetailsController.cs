using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles reading and writing of student general details during registration.
/// Covers the General tab of the registration form and direct registration entry.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationGeneralDetailsController(
    IStudentGeneralInfoAppService registrationAppService) : AbpControllerBase
{
    /// <summary>
    /// Gets general details for a student in Registration status.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <returns>Student general details including registration progress</returns>
    /// <response code="200">Returns the general details</response>
    /// <response code="404">Student not found</response>
    [HttpGet("{studentId}/general-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<StudentGeneralDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentGeneralDetailsDto>> GetGeneralDetailsAsync(Guid studentId)
    {
        var result = await registrationAppService.GetRegistrationGeneralDetailsByIdAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Updates general information for a student and moves them to Registration status.
    /// Transitions the student from RegistrationFormIssued to Registration status upon
    /// successful validation. Can also be used to update an existing registration.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="input">General information data</param>
    /// <returns>Updated student information with Registration status</returns>
    /// <response code="200">General details updated successfully</response>
    /// <response code="400">Invalid data or student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpPut("{studentId}/general-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentGeneralDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentGeneralDetailsDto>> UpdateGeneralDetailsAsync(
        Guid studentId,
        [FromBody] UpdateGeneralInfoDto input)
    {
        var result = await registrationAppService.UpdateGeneralInfoAsync(studentId, input);
        return Result.Success(result);
    }

    /// <summary>
    /// Creates a new student directly in Registration status, bypassing the inquiry
    /// and registration form issued flow. Intended for direct registration from the
    /// Registration screen.
    /// </summary>
    /// <param name="input">General information data including education medium</param>
    /// <returns>Newly created student in Registration status</returns>
    /// <response code="200">Student created successfully</response>
    /// <response code="400">Invalid data</response>
    [HttpPost("general-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentGeneralDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<Result<StudentGeneralDetailsDto>> CreateDirectRegistrationAsync(
        [FromBody] CreateDirectRegistrationDto input)
    {
        var result = await registrationAppService.CreateDirectRegistrationAsync(input);
        return Result.Success(result);
    }
}
