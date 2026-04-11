using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the Communication &amp; Address section of the student registration form.
/// Covers local, permanent, and emergency address details.
/// </summary>
public interface IStudentCommunicationInfoAppService : IApplicationService
{
    Task<StudentCommunicationInfoDto> GetCommunicationInfoAsync(Guid studentId);

    Task<StudentCommunicationInfoDto> UpdateCommunicationInfoAsync(Guid studentId, UpdateCommunicationInfoDto input);
}
