using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the Additional Details section of the student registration form.
/// Covers category, BPL status, disability information, emergency contacts,
/// and library/transport membership flags.
/// </summary>
public interface IStudentAdditionalDetailsAppService : IApplicationService
{
    Task<StudentAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid studentId);

    Task<StudentAdditionalDetailsDto> UpdateAdditionalDetailsAsync(Guid studentId, UpdateAdditionalDetailsDto input);
}
