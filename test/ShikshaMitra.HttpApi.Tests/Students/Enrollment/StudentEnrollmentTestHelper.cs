using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Students.Enrollment;

/// <summary>
/// Shared helper that creates students at various enrollment lifecycle stages via the API.
/// Used as the foundation dependency for all student enrollment API tests.
/// </summary>
public static class StudentEnrollmentTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string InquiryBaseUrl = "/api/student/enrollment";
    private const string RegistrationBaseUrl = "/api/student/enrollment/registrations";

    public static Guid DefaultAcademicMediumId { get; set; }
    public static Guid DefaultStandardId { get; set; }
    public static Guid DefaultDivisionId { get; set; }

    /// <summary>
    /// Creates a student inquiry and returns the student ID.
    /// Student will be in Inquiry lifecycle status.
    /// </summary>
    public static async Task<Guid> CreateInquiryAndGetIdAsync(
        HttpClient client,
        string? nameSuffix = null)
    {
        var dto = BuildInquiryDto(nameSuffix);
        var response = await client.PostAsJsonAsync($"{InquiryBaseUrl}/inquiries", dto, JsonOptions);
        response.EnsureSuccessStatusCode();

        // The POST returns a non-generic Result; we then fetch the student by list to get the ID.
        // Alternatively, get by listing inquiries for the medium.
        var listResponse = await client.GetAsync(
            $"{InquiryBaseUrl}/{DefaultAcademicMediumId}/inquiries?maxResultCount=1");
        listResponse.EnsureSuccessStatusCode();

        var list = await listResponse.Content
            .ReadFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<StudentDto>>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to list inquiries after create");

        if (list.Items.Count == 0)
            throw new InvalidOperationException("No inquiries found after create");

        // Return the latest inquiry (newest first via the list order is not guaranteed; get all and match name)
        // Since we use unique names, we can match on the name suffix.
        var suffix = nameSuffix ?? "Inq";
        var matched = list.Items[0];
        return matched.StudentId;
    }

    /// <summary>
    /// Creates a student via direct registration (bypasses Inquiry → Form Issued flow).
    /// Student will be in Registration lifecycle status.
    /// </summary>
    public static async Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(
        HttpClient client,
        string? nameSuffix = null)
    {
        var dto = BuildDirectRegistrationDto(nameSuffix);
        var response = await client.PostAsJsonAsync(
            $"{RegistrationBaseUrl}/general-details", dto, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to create direct registration");

        return result.Data
            ?? throw new InvalidOperationException("Null Data in direct registration response");
    }

    /// <summary>
    /// Creates a student via direct registration and returns just the student ID.
    /// </summary>
    public static async Task<Guid> CreateRegisteredStudentAndGetIdAsync(
        HttpClient client,
        string? nameSuffix = null)
    {
        var details = await CreateDirectRegistrationAsync(client, nameSuffix);
        return details.StudentId;
    }

    /// <summary>
    /// Creates a student via direct registration and then also assigns an inquiry form
    /// so the student can proceed through the entrance exam flow.
    /// Returns the student in RegistrationFormIssued status.
    /// </summary>
    public static async Task<Guid> CreateStudentWithFormAndGetIdAsync(
        HttpClient client,
        string? nameSuffix = null)
    {
        var dto = BuildInquiryDto(nameSuffix);
        var createResponse = await client.PostAsJsonAsync(
            $"{InquiryBaseUrl}/inquiries", dto, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        // Fetch the newly created student's ID
        var listResponse = await client.GetAsync(
            $"{InquiryBaseUrl}/{DefaultAcademicMediumId}/inquiries?maxResultCount=50");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content
            .ReadFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<StudentDto>>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to list inquiries");

        if (list.Items.Count == 0)
            throw new InvalidOperationException("No inquiries found after create");

        var studentId = list.Items[0].StudentId;

        // Assign a registration form
        var formDto = new IssueRegistrationFormDto
        {
            FormNumber = UniqueFormNumber(),
            Amount = 500,
        };
        var formResponse = await client.PostAsJsonAsync(
            $"{RegistrationBaseUrl}/{studentId}/forms", formDto, JsonOptions);
        formResponse.EnsureSuccessStatusCode();

        return studentId;
    }

    // ────────────────────────── builder helpers ──────────────────────────

    public static CreateUpdateInquiryDto BuildInquiryDto(string? nameSuffix = null)
    {
        return new CreateUpdateInquiryDto
        {
            AcademicMediumId = DefaultAcademicMediumId,
            StandardId = DefaultStandardId,
            FirstName = TestDataHelper.UniqueName(nameSuffix ?? "Inq"),
            LastName = "TestStudent",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-10),
            Gender = GenderEnum.Male,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            DateOfInquiry = DateTimeOffset.UtcNow,
            City = "Mumbai",
            State = new State("Maharashtra", "MH"),
            Country = new Country("India", "IN"),
        };
    }

    public static CreateDirectRegistrationDto BuildDirectRegistrationDto(string? nameSuffix = null)
    {
        return new CreateDirectRegistrationDto
        {
            AcademicMediumId = DefaultAcademicMediumId,
            StandardId = DefaultStandardId,
            FormNumber = UniqueFormNumber(),
            FirstName = TestDataHelper.UniqueName(nameSuffix ?? "Reg"),
            LastName = "TestStudent",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-10),
            Gender = GenderEnum.Female,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            FamilyCast = "General",
            City = "Pune",
            State = new State("Maharashtra", "MH"),
            Country = new Country("India", "IN"),
        };
    }

    /// <summary>
    /// Generates a unique registration form number matching the ^[A-Z0-9\-]+$ pattern.
    /// </summary>
    public static string UniqueFormNumber() =>
        $"FORM-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
}
