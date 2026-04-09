using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles reading and writing of student communication and address information during registration.
/// Covers local address, permanent address, and emergency address details.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationCommunicationDetailsController(
    IStudentCommunicationInfoAppService registrationAppService) : AbpControllerBase
{
    /// <summary>
    /// Gets communication and address information for a student in Registration status.
    /// Includes local address, permanent address, and emergency address details.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <returns>Communication information including all address details</returns>
    /// <response code="200">Returns the communication information</response>
    /// <response code="400">Student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpGet("{studentId}/communication-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentCommunicationInfoDto>> GetCommunicationInfoAsync(Guid studentId)
    {
        var result = await registrationAppService.GetCommunicationInfoAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Updates communication and address information for a student in Registration status.
    /// Includes local address, permanent address, and emergency address with contact details.
    /// Updates the registration state to Communication upon successful validation.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="input">Communication information data</param>
    /// <returns>Updated communication information</returns>
    /// <response code="200">Communication information updated successfully</response>
    /// <response code="400">Invalid data or student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpPut("{studentId}/communication-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentCommunicationInfoDto>> UpdateCommunicationInfoAsync(
        Guid studentId,
        [FromBody] UpdateCommunicationInfoDto input)
    {
        var result = await registrationAppService.UpdateCommunicationInfoAsync(studentId, input);
        return Result.Success(result);
    }
}
