using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentRegistrationQueryAppService(
    IRepository<Student, Guid> studentRepository,
    EntityValidationService validationService
) : StudentRegistrationBaseAppService(studentRepository, validationService),
    IStudentRegistrationQueryAppService
{
    public async Task<PagedResultDto<StudentDto>> GetRegistrationFormsByMediumAsync(
        Guid academicMediumId,
        int skipCount = 0,
        int maxResultCount = 10,
        string? searchTerm = null)
    {
        await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

        var queryable = await studentRepository.WithDetailsAsync(
			 s => s.AdditionalDetail,
			 s => s.ContactDetail,
			 s => s.Notes
		);

		var query = queryable
            .Where(s => s.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued
                && s.AcademicMediumId == academicMediumId
                && s.TenantId == CurrentTenant.Id);

        query = ApplySearchFilter(query, searchTerm);

        var totalCount = await AsyncExecuter.CountAsync(query);

        if (totalCount == 0)
        {
            return new PagedResultDto<StudentDto>(0, new List<StudentDto>());
        }

        var pagedQuery = query
            .OrderByDescending(x => x.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount);

        var results = await AsyncExecuter.ToListAsync(pagedQuery);
        var resultList = ObjectMapper.Map<List<Student>, List<StudentDto>>(results);

        return new PagedResultDto<StudentDto>(totalCount, resultList);
    }

    public async Task<PagedResultDto<StudentDto>> GetRegistrationsByMediumAsync(
        Guid academicMediumId,
        int skipCount = 0,
        int maxResultCount = 10,
        string? searchTerm = null)
    {
        await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

        var queryable = await studentRepository.WithDetailsAsync(
			 s => s.AdditionalDetail,
			 s => s.ContactDetail,
			 s => s.Notes
		);

		var query = queryable
            .Where(s => s.Status == StudentLifeCycleStatusEnum.Registration
                && s.AcademicMediumId == academicMediumId
                && s.TenantId == CurrentTenant.Id);

        query = ApplySearchFilter(query, searchTerm);

        var totalCount = await AsyncExecuter.CountAsync(query);

        if (totalCount == 0)
        {
            return new PagedResultDto<StudentDto>(0, new List<StudentDto>());
        }

        var pagedQuery = query
            .OrderByDescending(x => x.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount);

        var results = await AsyncExecuter.ToListAsync(pagedQuery);
        var resultList = ObjectMapper.Map<List<Student>, List<StudentDto>>(results);

        return new PagedResultDto<StudentDto>(totalCount, resultList);
    }

    public async Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(string searchTerm)
    {
        var normalizedSearchTerm = (searchTerm ?? string.Empty).Trim().ToLower();

        var queryable = await studentRepository.GetQueryableAsync();

        var query = queryable
            .Where(s => s.TenantId == CurrentTenant.Id
                && s.Status == StudentLifeCycleStatusEnum.Admission
                && (
                    (s.FirstName != null && s.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
                    (s.MiddleName != null && s.MiddleName.ToLower().Contains(normalizedSearchTerm)) ||
                    (s.LastName != null && s.LastName.ToLower().Contains(normalizedSearchTerm))
                ));

        var students = await AsyncExecuter.ToListAsync(query);

        if (students.Count == 0)
        {
            return new List<StudentSearchResultDto>();
        }

        return students
            .Select(s => new StudentSearchResultDto
            {
                Id = s.Id,
                Student = $"{s.FirstName} {s.MiddleName} {s.LastName} [{s.AcademicMedium.EducationBoard.Type.ToString()}] ({s.Standard?.Name} - {s.Division?.Name})".Trim()
            })
            .ToList();
    }
}
