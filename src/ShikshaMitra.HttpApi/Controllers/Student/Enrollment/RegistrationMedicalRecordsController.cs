using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles reading and writing of student medical records during registration.
/// Covers weight, height, immunization records, allergies, and other health information.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationMedicalRecordsController(
    IStudentMedicalRecordsAppService registrationAppService) : AbpControllerBase
{
    /// <summary>
    /// Gets medical records for a student in Registration status.
    /// Includes weight, height, immunization records, allergies, and other health information.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <returns>Medical records information</returns>
    /// <response code="200">Returns the medical records</response>
    /// <response code="400">Student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpGet("{studentId}/medical-records")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentMedicalRecordsDto>> GetMedicalRecordsAsync(Guid studentId)
    {
        var result = await registrationAppService.GetMedicalRecordsAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Updates medical records for a student in Registration status.
    /// Includes physical measurements, immunization records, allergies, and health conditions.
    /// Updates the registration state to MedicalRecords upon successful validation.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="input">Medical records data</param>
    /// <returns>Updated medical records information</returns>
    /// <response code="200">Medical records updated successfully</response>
    /// <response code="400">Invalid data or student not in valid status</response>
    /// <response code="404">Student not found</response>
    [HttpPut("{studentId}/medical-records")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
    [ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<StudentMedicalRecordsDto>> UpdateMedicalRecordsAsync(
        Guid studentId,
        [FromBody] UpdateMedicalRecordsDto input)
    {
        var result = await registrationAppService.UpdateMedicalRecordsAsync(studentId, input);
        return Result.Success(result);
    }
}
