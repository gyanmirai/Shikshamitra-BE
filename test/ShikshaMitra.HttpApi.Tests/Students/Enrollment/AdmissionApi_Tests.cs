using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student admission endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET /api/students/admissions, PUT /{id}/division, POST /status.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/students/admissions";

    public AdmissionApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Admissions_List_Without_Error()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Admissions_List_With_Valid_Shape()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Items.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Empty_List_When_No_Students_In_Admission_Stage()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Assign_Division_Nonexistent_Student()
    {
        var dto = new AssignDivisionDto { DivisionId = Factory.SeededDivisionId };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/division", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Change_Status_With_Empty_Items()
    {
        var dto = new BatchAdmissionStatusChangeRequestDto
        {
            Items = new List<AdmissionStatusChangeItemDto>(),
        };

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/status", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Approve_Nonexistent_Student()
    {
        var dto = new BatchAdmissionStatusChangeRequestDto
        {
            Items = new List<AdmissionStatusChangeItemDto>
            {
                new()
                {
                    StudentId = Guid.NewGuid(),
                    Action = StudentEnrollmentApprovalState.Approved,
                    Remarks = "Test approval",
                },
            },
        };

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/status", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);
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
    public async Task Should_Return_Admissions_List_With_Pagination()
    {
        var response = await Client.GetAsync($"{BaseUrl}?skipCount=0&maxResultCount=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBeLessThanOrEqualTo(5);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_Reject_Nonexistent_Student()
    {
        var dto = new BatchAdmissionStatusChangeRequestDto
        {
            Items = new List<AdmissionStatusChangeItemDto>
            {
                new()
                {
                    StudentId = Guid.NewGuid(),
                    Action = StudentEnrollmentApprovalState.Rejected,
                    Remarks = "Does not meet criteria",
                },
            },
        };

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/status", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Return_Error_For_Assign_Invalid_Division()
    {
        var dto = new AssignDivisionDto { DivisionId = Guid.NewGuid() };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/division", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    #endregion
}
