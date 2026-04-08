using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the Previous School Details section of the student registration form.
/// Covers school name, academic board, TC number, academic performance, and reason for transfer.
/// </summary>
public interface IStudentPreviousSchoolDetailsAppService : IApplicationService
{
    Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId);

    Task<StudentPreviousSchoolDetailsDto> UpdatePreviousSchoolDetailsAsync(Guid studentId, UpdatePreviousSchoolDetailsDto input);
}
