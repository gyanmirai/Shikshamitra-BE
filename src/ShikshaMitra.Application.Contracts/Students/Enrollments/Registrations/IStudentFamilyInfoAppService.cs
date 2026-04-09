using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Manages the Family Information section of the student registration form.
/// Covers father, mother, guardian, and sibling details.
/// </summary>
public interface IStudentFamilyInfoAppService : IApplicationService
{
    Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId);

    Task<StudentFamilyInfoDto> UpdateFamilyInfoAsync(Guid studentId, UpdateFamilyInfoDto input);
}
