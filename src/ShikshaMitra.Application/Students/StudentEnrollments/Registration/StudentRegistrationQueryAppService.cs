using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentRegistrationQueryAppService(
    IStudentRepository studentRepository,
    IRepository<StudentNote, Guid> studentNoteRepository,
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
        await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
        await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

        var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();

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

        var results = await AttachLatestInquiryStatusesAsync(
            await AsyncExecuter.ToListAsync(SelectRegistrationRows(pagedQuery)));
        var resultList = results.Select(MapRegistrationListRow).ToList();

        return new PagedResultDto<StudentDto>(totalCount, resultList);
    }

    public async Task<PagedResultDto<StudentDto>> GetRegistrationsByMediumAsync(
        Guid academicMediumId,
        int skipCount = 0,
        int maxResultCount = 10,
        string? searchTerm = null)
    {
        await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
        await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

        var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();

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

        var results = await AttachLatestInquiryStatusesAsync(
            await AsyncExecuter.ToListAsync(SelectRegistrationRows(pagedQuery)));
        var resultList = results.Select(MapRegistrationListRow).ToList();

        return new PagedResultDto<StudentDto>(totalCount, resultList);
    }

    public async Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(string searchTerm, int maxResultCount = 20)
    {
        var canReadApplications = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
        var canReadRecords = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
        if (!canReadApplications && !canReadRecords)
            await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
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
                && (s.Status == StudentLifeCycleStatusEnum.Enrolled ? canReadRecords : canReadApplications)
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

    private static IQueryable<StudentRegistrationListRow> SelectRegistrationRows(IQueryable<Student> query)
    {
        return query.Select(s => new StudentRegistrationListRow(
            s.Id,
            s.AdditionalDetail != null ? s.AdditionalDetail.InquiryNo : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.RegistrationFormNo : null,
            s.FirstName,
            s.MiddleName,
            s.LastName,
            s.AcademicYearId,
            s.AcademicYear != null ? s.AcademicYear.Name : null,
            s.AcademicBoardId,
            s.AcademicBoard != null ? s.AcademicBoard.Type : null,
            s.AcademicMediumId,
            s.AcademicMedium != null ? s.AcademicMedium.Name : null,
            s.StandardId,
            s.Standard != null ? s.Standard.Name : null,
            s.DivisionId,
            s.Division != null ? s.Division.Name : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.DateOfBirth : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.DateOfInquiry : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.Gender : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.Category : Category.General,
            s.AdditionalDetail != null && s.AdditionalDetail.IsLibraryMember == true,
            s.AdditionalDetail != null && s.AdditionalDetail.IsTransportMember == true,
            s.AdditionalDetail != null ? s.AdditionalDetail.EntranceExamSlotId : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.EntranceExamStatus : null,
            NoteStatusEnum.Pending,
            s.ContactDetail != null ? s.ContactDetail.Address1 : null,
            s.ContactDetail != null ? s.ContactDetail.Address2 : null,
            s.ContactDetail != null ? s.ContactDetail.Address3 : null,
            s.ContactDetail != null ? s.ContactDetail.City : null,
            s.ContactDetail != null && s.ContactDetail.State != null ? s.ContactDetail.State.Name : null,
            s.ContactDetail != null && s.ContactDetail.Country != null ? s.ContactDetail.Country.Name : null,
            s.ContactDetail != null ? s.ContactDetail.Pincode : null,
            s.ContactDetail != null ? s.ContactDetail.PrimaryContactNo : null,
            s.ContactDetail != null ? s.ContactDetail.SecondaryContactNo : null,
            s.ContactDetail != null ? s.ContactDetail.Email : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.PreviousSchool : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.InquiryPreferenceId : null,
            s.AdditionalDetail != null && s.AdditionalDetail.InquiryPreference != null
                ? s.AdditionalDetail.InquiryPreference.PreferenceName
                : null,
            s.AdditionalDetail != null ? s.AdditionalDetail.StudentRegistrationState : StudentRegistrationStateEnum.Default,
            s.FeeStructureId.HasValue,
            s.Status));
    }

    private static StudentDto MapRegistrationListRow(StudentRegistrationListRow row)
    {
        return new StudentDto
        {
            StudentId = row.StudentId,
            InquiryNo = row.InquiryNo ?? string.Empty,
            FormNo = row.FormNo,
            StudentName = BuildStudentName(row.FirstName, row.MiddleName, row.LastName),
            FirstName = row.FirstName,
            MiddleName = row.MiddleName,
            LastName = row.LastName,
            AcademicYearId = row.AcademicYearId,
            AcademicYearName = row.AcademicYearName,
            AcademicBoardId = row.AcademicBoardId,
            AcademicBoardName = row.AcademicBoardType?.ToString(),
            AcademicMediumId = row.AcademicMediumId,
            AcademicMediumName = row.AcademicMediumName,
            StandardId = row.StandardId ?? Guid.Empty,
            Standard = row.Standard ?? string.Empty,
            DivisionId = row.DivisionId,
            Division = row.Division,
            DateOfBirth = row.DateOfBirth,
            Age = FormatAge(row.DateOfBirth),
            DateOfInquiry = row.DateOfInquiry,
            Gender = row.Gender,
            Category = row.Category,
            IsLibraryMember = row.IsLibraryMember,
            IsTransportMember = row.IsTransportMember,
            Address1 = row.Address1,
            Address2 = row.Address2,
            Address3 = row.Address3,
            City = row.City,
            PinCode = row.PinCode,
            Address = string.Join(", ", new[]
            {
                row.Address1,
                row.Address2,
                row.Address3,
                row.City,
                row.StateName,
                row.CountryName
            }.Where(part => !string.IsNullOrWhiteSpace(part))),
            PrimaryContactNo = row.PrimaryContactNo,
            SecondaryContactNo = row.SecondaryContactNo,
            Email = row.Email,
            PreviousSchool = row.PreviousSchool,
            InquiryPreferenceId = row.InquiryPreferenceId,
            InquiryPreference = row.InquiryPreference,
            InquiryStatus = row.InquiryStatus,
            Progress = CalculateRegistrationProgressPercentage(row.RegistrationState),
            StudentEnrollmentState = row.StudentEnrollmentState,
            Status = row.StudentEnrollmentState,
            EntranceExamStatus = row.EntranceExamStatus,
            EntranceExamStatusDisplay = ResolveEntranceExamStatusDisplay(
                row.StudentEnrollmentState,
                row.EntranceExamSlotId,
                row.EntranceExamStatus),
            IsFeeStructureAssigned = row.IsFeeStructureAssigned
        };
    }

    private async Task<List<StudentRegistrationListRow>> AttachLatestInquiryStatusesAsync(
        List<StudentRegistrationListRow> rows)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var studentIds = rows.Select(row => row.StudentId).ToList();
        var noteRows = await AsyncExecuter.ToListAsync(
            (await studentNoteRepository.GetQueryableAsync())
                .Where(note => studentIds.Contains(note.StudentId))
                .OrderByDescending(note => note.CreationTime)
                .ThenByDescending(note => note.Id)
                .Select(note => new
                {
                    note.StudentId,
                    note.Status
                }));

        var latestStatusByStudent = noteRows
            .GroupBy(note => note.StudentId)
            .ToDictionary(group => group.Key, group => group.First().Status);

        return rows
            .Select(row => latestStatusByStudent.TryGetValue(row.StudentId, out var status)
                ? row with { InquiryStatus = status }
                : row)
            .ToList();
    }

    private static string ResolveEntranceExamStatusDisplay(
        StudentLifeCycleStatusEnum studentStatus,
        Guid? entranceExamSlotId,
        EntranceExamStatusEnum? entranceExamStatus)
    {
        if (studentStatus == StudentLifeCycleStatusEnum.Enrolled)
        {
            return "Enrolled";
        }

        return entranceExamStatus switch
        {
            EntranceExamStatusEnum.Pass or EntranceExamStatusEnum.Fail or EntranceExamStatusEnum.Absent => "Completed",
            EntranceExamStatusEnum.Pending => "Scheduled",
            _ when entranceExamSlotId.HasValue => "Scheduled",
            _ => "Not Scheduled"
        };
    }

    private static string? FormatAge(DateTimeOffset? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
        {
            return null;
        }

        var today = DateTimeOffset.UtcNow.Date;
        var birthday = dateOfBirth.Value.Date;
        var years = today.Year - birthday.Year;
        if (birthday > today.AddYears(-years))
        {
            years--;
        }

        return years >= 0 ? $"{years} years" : null;
    }

    private static double CalculateRegistrationProgressPercentage(
        StudentRegistrationStateEnum registrationState = StudentRegistrationStateEnum.Default)
    {
        var totalStates = (double)Enum.GetValues<StudentRegistrationStateEnum>()
            .Count(s => s != StudentRegistrationStateEnum.Default);
        return totalStates == 0 ? 0 : (double)registrationState / totalStates * 100;
    }

    private static string FormatStudentSearchLabel(StudentSearchResultDto student)
    {
        var classText = string.Join(" - ", new[] { student.Standard, student.Division }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        var boardText = string.IsNullOrWhiteSpace(student.AcademicBoard) ? string.Empty : $" [{student.AcademicBoard}]";
        var classSuffix = string.IsNullOrWhiteSpace(classText) ? string.Empty : $" ({classText})";
        return $"{student.StudentName}{boardText}{classSuffix}".Trim();
    }

    private sealed record StudentRegistrationListRow(
        Guid StudentId,
        string? InquiryNo,
        string? FormNo,
        string? FirstName,
        string? MiddleName,
        string? LastName,
        Guid? AcademicYearId,
        string? AcademicYearName,
        Guid? AcademicBoardId,
        EducationBoardType? AcademicBoardType,
        Guid AcademicMediumId,
        string? AcademicMediumName,
        Guid? StandardId,
        string? Standard,
        Guid? DivisionId,
        string? Division,
        DateTimeOffset? DateOfBirth,
        DateTimeOffset? DateOfInquiry,
        GenderEnum? Gender,
        Category Category,
        bool IsLibraryMember,
        bool IsTransportMember,
        Guid? EntranceExamSlotId,
        EntranceExamStatusEnum? EntranceExamStatus,
        NoteStatusEnum InquiryStatus,
        string? Address1,
        string? Address2,
        string? Address3,
        string? City,
        string? StateName,
        string? CountryName,
        string? PinCode,
        string? PrimaryContactNo,
        string? SecondaryContactNo,
        string? Email,
        string? PreviousSchool,
        Guid? InquiryPreferenceId,
        string? InquiryPreference,
        StudentRegistrationStateEnum RegistrationState,
        bool IsFeeStructureAssigned,
        StudentLifeCycleStatusEnum StudentEnrollmentState);
}
