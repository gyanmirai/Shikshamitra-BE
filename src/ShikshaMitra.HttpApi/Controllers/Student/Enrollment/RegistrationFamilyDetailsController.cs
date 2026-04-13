using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles reading and writing of student family information during registration.
/// Covers father, mother, guardian, and sibling details.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationFamilyDetailsController(
    IStudentFamilyInfoAppService registrationAppService) : AbpControllerBase
{
    /// <summary>
    /// Gets family information for a student in Registration status.
    /// Includes father, mother, guardian, and sibling details.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <returns>Family information including all parent/guardian and sibling details</returns>
    /// <response code="200">Returns the family information</response>
    /// <response code="400">Student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpGet("{studentId}/family-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentFamilyInfoDto>> GetFamilyInfoAsync(Guid studentId)
    {
        var result = await registrationAppService.GetFamilyInfoAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Updates family information for a student in Registration status.
    /// Includes father, mother, guardian details and sibling information.
    /// Updates the registration state to Family upon successful validation.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="input">Family information data</param>
    /// <returns>Updated family information</returns>
    /// <response code="200">Family information updated successfully</response>
    /// <response code="400">Invalid data or student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpPut("{studentId}/family-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentFamilyInfoDto>> UpdateFamilyInfoAsync(
        Guid studentId,
        [FromBody] UpdateFamilyInfoDto input)
    {
        var result = await registrationAppService.UpdateFamilyInfoAsync(studentId, input);
        return Result.Success(result);
    }
}
