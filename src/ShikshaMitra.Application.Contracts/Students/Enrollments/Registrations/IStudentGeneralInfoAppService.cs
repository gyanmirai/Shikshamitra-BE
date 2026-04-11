using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the General Information section of the student registration form.
/// Handles direct registration creation and general info updates that transition
/// a student from RegistrationFormIssued to Registration status.
/// </summary>
public interface IStudentGeneralInfoAppService : IApplicationService
{
    Task<StudentGeneralDetailsDto> GetRegistrationGeneralDetailsByIdAsync(Guid studentId);

    Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input);

    Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto input);
}
