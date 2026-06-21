using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Notes;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
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
        var listResult = await listResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        var list = listResult?.Data;
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
        var listResult = await listResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        var list = listResult?.Data;
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

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Recreate_Inquiry_Preference_After_Delete()
    {
        var preferenceName = $"Online {Guid.NewGuid():N}";
        var createResponse = await Client.PostAsJsonAsync(
            $"/api/student-enrollment/{Factory.SeededAcademicYearId}/settings/inquiry-preferences",
            new CreateUpdateInquiryPreferenceDto { PreferenceName = preferenceName },
            JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
        var createdPreference = await createResponse.Content.ReadFromJsonAsync<InquiryPreferenceDto>(JsonOptions);
        createdPreference.ShouldNotBeNull();

        var deleteResponse = await Client.DeleteAsync(
            $"/api/student-enrollment/settings/inquiry-preferences/{createdPreference.Id}");
        deleteResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        var recreateResponse = await Client.PostAsJsonAsync(
            $"/api/student-enrollment/{Factory.SeededAcademicYearId}/settings/inquiry-preferences",
            new CreateUpdateInquiryPreferenceDto { PreferenceName = preferenceName },
            JsonOptions);

        recreateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await recreateResponse.Content.ReadAsStringAsync());
        var recreatedPreference = await recreateResponse.Content.ReadFromJsonAsync<InquiryPreferenceDto>(JsonOptions);
        recreatedPreference.ShouldNotBeNull();
        recreatedPreference.PreferenceName.ShouldBe(preferenceName);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Scoped_User_Should_Not_Get_OutOfScope_Inquiry_By_Id()
    {
        var studentId = await StudentEnrollmentTestHelper.CreateInquiryAndGetIdAsync(
            Client,
            "InqScopeDenied");
        var scopedUserId = Guid.NewGuid();
        await SeedAcademicYearScopeAsync(scopedUserId, Guid.NewGuid());
        var scopedClient = CreateClientAs(scopedUserId);

        var response = await scopedClient.GetAsync($"{BaseUrl}/inquiries/{studentId}");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        content.ShouldContain(UserScopeErrorCodes.AccessDenied);
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
        var listResult = await listResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        var list = listResult?.Data;
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
    public async Task Should_Return_Computed_Inquiry_List_And_Detail_Fields()
    {
        var preferenceName = $"Walk-in {Guid.NewGuid():N}";
        var preferenceResponse = await Client.PostAsJsonAsync(
            $"/api/student-enrollment/{Factory.SeededAcademicYearId}/settings/inquiry-preferences",
            new CreateUpdateInquiryPreferenceDto { PreferenceName = preferenceName },
            JsonOptions);
        preferenceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await preferenceResponse.Content.ReadAsStringAsync());
        var preference = await preferenceResponse.Content.ReadFromJsonAsync<InquiryPreferenceDto>(JsonOptions);
        preference.ShouldNotBeNull();

        var dto = StudentEnrollmentTestHelper.BuildInquiryDto("InqProjection");
        dto.DateOfBirth = new DateTimeOffset(2015, 6, 16, 0, 0, 0, TimeSpan.Zero);
        dto.Address1 = "12 Sunrise Street";
        dto.Address2 = "Near City Library";
        dto.Address3 = "Block C";
        dto.City = "Ahmedabad";
        dto.PinCode = "380015";
        dto.InquiryPreferenceId = preference.Id;

        var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/inquiries", dto, JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=20&searchTerm={Uri.EscapeDataString(dto.FirstName)}");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
        var listResult = await listResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        listResult.ShouldNotBeNull();
        listResult.Data.ShouldNotBeNull();
        var listItem = listResult.Data.Items.Single(item => item.FirstName == dto.FirstName);

        var reminderDate = DateTimeOffset.UtcNow.AddDays(3);
        var noteResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/{listItem.StudentId}/notes",
            new CreateUpdateStudentNoteDto
            {
                Note = "Projection regression follow-up",
                NextReminderDate = reminderDate,
                Status = NoteStatusEnum.Interested,
            },
            JsonOptions);
        noteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await noteResponse.Content.ReadAsStringAsync());

        var getResponse = await Client.GetAsync($"{BaseUrl}/inquiries/{listItem.StudentId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getResponse.Content.ReadAsStringAsync());
        var detail = await getResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<StudentDto>>(JsonOptions);
        detail.ShouldNotBeNull();
        detail.Data.ShouldNotBeNull();
        detail.Data.Age.ShouldNotBeNullOrWhiteSpace();
        detail.Data.Address.ShouldNotBeNull();
        detail.Data.Address!.ShouldContain("12 Sunrise Street");
        detail.Data.Address.ShouldContain("Near City Library");
        detail.Data.InquiryPreferenceId.ShouldBe(preference.Id);
        detail.Data.InquiryPreference.ShouldBe(preferenceName);
        detail.Data.InquiryStatus.ShouldBe(NoteStatusEnum.Interested);
        detail.Data.NextReminderDate.ShouldNotBeNull();
        detail.Data.NextReminderDate!.Value.Date.ShouldBe(reminderDate.Date);

        var refreshedListResponse = await Client.GetAsync(
            $"{BaseUrl}/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/inquiries?maxResultCount=20&searchTerm={Uri.EscapeDataString(dto.FirstName)}");
        var refreshedList = await refreshedListResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        var refreshedItem = refreshedList!.Data!.Items.Single(item => item.StudentId == listItem.StudentId);
        refreshedItem.Age.ShouldNotBeNullOrWhiteSpace();
        refreshedItem.Address.ShouldNotBeNull();
        refreshedItem.Address!.ShouldContain("Block C");
        refreshedItem.InquiryPreference.ShouldBe(preferenceName);
        refreshedItem.NextReminderDate.ShouldNotBeNull();
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
        var listResult = await listResponse.Content
            .ReadFromJsonAsync<Common.Responses.Result<PagedResultDto<StudentDto>>>(JsonOptions);
        var list = listResult?.Data;
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

    private async Task SeedAcademicYearScopeAsync(Guid userId, Guid academicYearId)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            await scopeRepository.InsertAsync(
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    tenantId,
                    userId: userId,
                    academicYearId: academicYearId,
                    description: "Student API out-of-scope regression test"),
                autoSave: false);

            await userScopeService.InvalidateUserScopeCacheAsync(userId);
            await uow.CompleteAsync();
        }
    }
}
