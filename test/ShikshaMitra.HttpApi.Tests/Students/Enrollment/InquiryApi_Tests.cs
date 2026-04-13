using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student inquiry endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// POST/GET/PUT /api/student/enrollment/inquiries.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class InquiryApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student/enrollment";

    public InquiryApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Inquiry_And_Return_Success()
    {
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqCreate");

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Inquiry_By_Id_After_Create()
    {
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqGet");
        var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        // Retrieve the created student ID via the list endpoint
        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=50");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        list.ShouldNotBeNull();
        list.Items.Count.ShouldBeGreaterThan(0);

        var studentId = list.Items[0].StudentId;
        var getResponse = await Client.GetAsync($"{BaseUrl}/inquiries/{studentId}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.StudentId.ShouldBe(studentId);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_Inquiry()
    {
        // Arrange – create and resolve the student ID
        var createDto = StudentEnrollmentTestHelper.BuildInquiryDto("InqUpd");
        var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", createDto, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=50");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        var studentId = list!.Items[0].StudentId;

        // Act – update the inquiry
        var updateDto = StudentEnrollmentTestHelper.BuildInquiryDto("InqUpdNew");
        updateDto.PreviousSchool = "St. Mary School";

        var updateResponse = await Client.PutAsJsonAsync(
            $"{BaseUrl}/inquiries/{studentId}", updateDto, JsonOptions);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await updateResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.PreviousSchool.ShouldBe("St. Mary School");
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Inquiry()
    {
        var response = await Client.GetAsync($"{BaseUrl}/inquiries/{Guid.NewGuid()}");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Update_Nonexistent_Inquiry()
    {
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqUpdMissing");

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/inquiries/{Guid.NewGuid()}", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401()
    {
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqAuth");
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Inquiry_List_By_Medium()
    {
        // Pre-populate at least one inquiry
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqList");
        await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);

        var response = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_Optional_Fields()
    {
        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqRT");
        dto.PreviousSchool = "DPS School";
        dto.SecondaryContactNo = TestDataHelper.UniquePhone();

        await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=50");
        var list = await listResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        var studentId = list!.Items[0].StudentId;

        var getResponse = await Client.GetAsync($"{BaseUrl}/inquiries/{studentId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.PreviousSchool.ShouldBe("DPS School");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Overwrite_Inquiry_Fields_On_Update()
    {
        var createDto = StudentEnrollmentTestHelper.BuildInquiryDto("InqOver");
        createDto.PreviousSchool = "Old School";
        await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", createDto, JsonOptions);

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=50");
        var list = await listResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        var studentId = list!.Items[0].StudentId;

        var updateDto = StudentEnrollmentTestHelper.BuildInquiryDto("InqOverNew");
        updateDto.PreviousSchool = "New School";
        await Client.PutAsJsonAsync($"{BaseUrl}/inquiries/{studentId}", updateDto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/inquiries/{studentId}");
        var body = await getResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.PreviousSchool.ShouldBe("New School");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Create_Multiple_Inquiries_For_Different_Students()
    {
        var dto1 = StudentEnrollmentTestHelper.BuildInquiryDto("InqMulti1");
        var dto2 = StudentEnrollmentTestHelper.BuildInquiryDto("InqMulti2");

        var r1 = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto1, JsonOptions);
        var r2 = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto2, JsonOptions);

        r1.StatusCode.ShouldBe(HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}
