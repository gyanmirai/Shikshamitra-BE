using System;
using System.Net.Http;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students.Records.Profiles;

namespace ShikshaMitra.Students.Enrollment;

/// <summary>
/// Shared helper that creates students at various enrollment lifecycle stages via the API.
/// Used as the foundation dependency for all student enrollment API tests.
/// </summary>
public static class StudentEnrollmentTestHelper
{
    public static ShikshaMitraApiTestFactory Factory { get; set; } = null!;

    public static Guid DefaultAcademicMediumId { get; set; }
    public static Guid DefaultDivisionId { get; set; }

    /// <summary>
    /// Creates canonical student fixture data directly in the test database.
    /// This deliberately avoids exposing a production enrollment bypass.
    /// </summary>
    public static async Task<StudentGeneralDetailsDto> CreateStudentFixtureAsync(
        HttpClient client,
        string? nameSuffix = null,
        Guid? divisionId = null,
        DateTimeOffset? dateOfBirth = null)
    {
        var student = await Factory.CreateStudentFixtureAsync(
            TestDataHelper.UniqueName(nameSuffix ?? "Student"),
            TestDataHelper.UniquePhone(), divisionId, dateOfBirth);
        return new StudentGeneralDetailsDto
        {
            StudentId = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            AcademicYearId = student.AcademicYearId!.Value,
            AcademicBoardId = student.AcademicBoardId!.Value,
            AcademicMediumId = student.AcademicMediumId,
            StandardId = student.StandardId!.Value,
            DateOfBirth = student.AdditionalDetail!.DateOfBirth!.Value,
            Gender = student.AdditionalDetail.Gender!.Value,
            PrimaryContactNo = student.ContactDetail!.PrimaryContactNo,
            City = student.ContactDetail.City!,
            State = student.ContactDetail.State!,
            Country = student.ContactDetail.Country!,
            FamilyCast = string.Empty,
            FormNo = string.Empty,
        };
    }

    /// <summary>
    /// Creates canonical student fixture data and returns just the student ID.
    /// </summary>
    public static async Task<Guid> CreateRegisteredStudentAndGetIdAsync(
        HttpClient client,
        string? nameSuffix = null)
    {
        var details = await CreateStudentFixtureAsync(client, nameSuffix);
        return details.StudentId;
    }

    /// <summary>
    /// Generates a unique registration form number matching the ^[A-Z0-9\-]+$ pattern.
    /// </summary>
    public static string UniqueFormNumber() =>
        $"FORM-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
}
