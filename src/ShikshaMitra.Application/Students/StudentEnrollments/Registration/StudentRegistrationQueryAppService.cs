using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentRegistrationQueryAppService(
    IStudentRepository studentRepository,
    EntityValidationService validationService,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
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
        query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

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
            .Where(s => (s.Status == StudentLifeCycleStatusEnum.Registration || s.Status == StudentLifeCycleStatusEnum.EntranceExam)
                && s.AcademicMediumId == academicMediumId
                && s.TenantId == CurrentTenant.Id);
        query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

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

    public async Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(string searchTerm, int maxResultCount = 20)
    {
        var normalizedSearchTerm = (searchTerm ?? string.Empty).Trim().ToLower();
        if (normalizedSearchTerm.Length < 2)
        {
            return new List<StudentSearchResultDto>();
        }

        var resultLimit = Math.Clamp(maxResultCount, 1, 50);
        var eligibleStatuses = new[]
        {
            StudentLifeCycleStatusEnum.Registration,
            StudentLifeCycleStatusEnum.EntranceExam,
            StudentLifeCycleStatusEnum.Admission,
            StudentLifeCycleStatusEnum.Enrolled
        };

        var queryable = await studentRepository.GetQueryableAsync();

        var query = queryable
            .Where(s => s.TenantId == CurrentTenant.Id
                && eligibleStatuses.Contains(s.Status)
                && (
                    (s.FirstName != null && s.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
                    (s.MiddleName != null && s.MiddleName.ToLower().Contains(normalizedSearchTerm)) ||
                    (s.LastName != null && s.LastName.ToLower().Contains(normalizedSearchTerm))
                ));
        query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

        var students = await AsyncExecuter.ToListAsync(
            query
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.MiddleName)
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.Id)
                .Take(resultLimit)
                .Select(s => new
                {
                    s.Id,
                    s.FirstName,
                    s.MiddleName,
                    s.LastName,
                    s.GrNo,
                    s.Status,
                    FormNo = s.AdditionalDetail != null ? s.AdditionalDetail.RegistrationFormNo : null,
                    AcademicBoard = s.AcademicBoard != null ? s.AcademicBoard.Type : (EducationBoardType?)null,
                    EducationMedium = s.AcademicMedium != null ? s.AcademicMedium.Name : null,
                    Standard = s.Standard != null ? s.Standard.Name : null,
                    Division = s.Division != null ? s.Division.Name : null
                }));

        if (students.Count == 0)
        {
            return new List<StudentSearchResultDto>();
        }

        return students
            .Select(s => new StudentSearchResultDto
            {
                Id = s.Id,
                StudentName = BuildStudentName(s.FirstName, s.MiddleName, s.LastName),
                FormNo = s.FormNo,
                GrNo = s.GrNo,
                AcademicBoard = s.AcademicBoard?.ToString(),
                EducationMedium = s.EducationMedium,
                Standard = s.Standard,
                Division = s.Division,
                Status = s.Status
            })
            .Select(s =>
            {
                s.Student = FormatStudentSearchLabel(s);
                return s;
            })
            .ToList();
    }

    private static string BuildStudentName(string? firstName, string? middleName, string? lastName)
        => string.Join(" ", new[] { firstName, middleName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string FormatStudentSearchLabel(StudentSearchResultDto student)
    {
        var classText = string.Join(" - ", new[] { student.Standard, student.Division }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        var boardText = string.IsNullOrWhiteSpace(student.AcademicBoard) ? string.Empty : $" [{student.AcademicBoard}]";
        var classSuffix = string.IsNullOrWhiteSpace(classText) ? string.Empty : $" ({classText})";
        return $"{student.StudentName}{boardText}{classSuffix}".Trim();
    }
}
