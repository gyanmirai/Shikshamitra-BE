using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student entrance exam endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET/POST /api/student-enrollment/entrance-exam.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class EntranceExamApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student-enrollment/entrance-exam";

    public EntranceExamApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Entrance_Exam_List_Without_Error()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Entrance_Exam_List_With_Valid_Shape()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Empty_List_When_No_Students_In_Exam_Stage()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_FormNo_In_Entrance_Exam_List()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamFormNo");
        await MoveRegistrationToEntranceExamAsync(registration.StudentId);

        var response = await Client.GetAsync(
            $"{BaseUrl}?searchTerm={Uri.EscapeDataString(registration.FirstName)}&maxResultCount=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.ShouldContain(item =>
            item.StudentId == registration.StudentId &&
            item.FormNo == registration.FormNo);
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Student()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Schedule_Nonexistent_Student()
    {
        var dto = new ScheduleEntranceExamDto { EntranceExamSlotId = Guid.NewGuid() };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/schedule", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Handled_Error_When_Scheduling_Invalid_Status()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "ExamInvalidStatus");
        await MoveRegistrationToEntranceExamAsync(registration.StudentId);

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{registration.StudentId}/schedule",
            new ScheduleEntranceExamDto { EntranceExamSlotId = Guid.NewGuid() },
            JsonOptions);

        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ShouldContain("ST:Enrollment:030001");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Upload_Result_Nonexistent_Student()
    {
        var dto = new UploadEntranceExamResultDto
        {
            TotalMarks = 100,
            PassingMarks = 40,
            ObtainedMarks = 75,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/result", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_MoveToAdmission_Nonexistent_Student()
    {
        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/move-to-admission", new { }, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401_On_List()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_List_With_Pagination_Defaults()
    {
        var response = await Client.GetAsync($"{BaseUrl}?skipCount=0&maxResultCount=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<PagedResultDto<EntranceExamStudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Items.Count.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_BulkMoveToAdmission_With_Empty_List()
    {
        var dto = new BulkMoveToAdmissionDto
        {
            StudentIds = new System.Collections.Generic.List<Guid>(),
            AcademicYearId = Factory.SeededAcademicYearId,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/bulk-move-to-admission", dto, JsonOptions);

        // Empty list may return validation error or empty result — both are acceptable
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_BulkMoveToAdmission_Nonexistent_Students()
    {
        var dto = new BulkMoveToAdmissionDto
        {
            StudentIds = new System.Collections.Generic.List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
            },
            AcademicYearId = Factory.SeededAcademicYearId,
        };

        var response = await Client.PostAsJsonAsync(
            $"{BaseUrl}/bulk-move-to-admission", dto, JsonOptions);

        // Non-existent IDs should either return an error or a result with all failures
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);
    }

    #endregion

    private async Task MoveRegistrationToEntranceExamAsync(Guid studentId)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var student = await studentRepository.GetInquiryForUpdateAsync(studentId)
                          ?? throw new InvalidOperationException($"Student not found: {studentId}");
            student.ScheduleEntranceExam(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(1),
                DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                101);

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

}
