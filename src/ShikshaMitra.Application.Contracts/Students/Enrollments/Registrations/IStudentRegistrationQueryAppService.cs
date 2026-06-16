using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// Provides paginated list and search queries for students in
/// RegistrationFormIssued and Registration lifecycle statuses.
/// </summary>
public interface IStudentRegistrationQueryAppService : IApplicationService
{
    Task<PagedResultDto<StudentDto>> GetRegistrationFormsByMediumAsync(
        Guid academicMediumId,
        int skipCount = 0,
        int maxResultCount = 10,
        string? searchTerm = null);

    Task<PagedResultDto<StudentDto>> GetRegistrationsByMediumAsync(
        Guid academicMediumId,
        int skipCount = 0,
        int maxResultCount = 10,
        string? searchTerm = null);

    Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(string searchTerm, int maxResultCount = 20);
}
