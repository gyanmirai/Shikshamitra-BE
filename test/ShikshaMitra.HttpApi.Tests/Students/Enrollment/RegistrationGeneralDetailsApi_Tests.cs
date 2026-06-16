using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Registrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for the registration general-details endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// POST/GET/PUT /api/student/enrollment/registrations/general-details
/// and GET/PUT /api/student/enrollment/registrations/{id}/general-details.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class RegistrationGeneralDetailsApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student/enrollment/registrations";

    public RegistrationGeneralDetailsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Direct_Registration_And_Return_Student_Details()
    {
        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto("RegCreate");

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/general-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.StudentId.ShouldNotBe(Guid.Empty);
        body.Data.FirstName.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_General_Details_After_Direct_Registration()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegGetDet");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/general-details");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.StudentId.ShouldBe(studentId);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Search_Registration_Students_For_Sibling_Selection()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "SiblingSearch");

        var response = await Client.GetAsync(
            $"{BaseUrl}/search?searchTerm={Uri.EscapeDataString(registration.FirstName)}&maxResultCount=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<List<StudentSearchResultDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.Count.ShouldBeLessThanOrEqualTo(5);
        body.Data.ShouldContain(item =>
            item.Id == registration.StudentId &&
            item.FormNo == registration.FormNo &&
            item.Status == StudentLifeCycleStatusEnum.Registration &&
            item.StudentName.Contains(registration.FirstName));
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Empty_Sibling_Search_For_One_Character_Query()
    {
        var response = await Client.GetAsync($"{BaseUrl}/search?searchTerm=A");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<List<StudentSearchResultDto>>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.ShouldBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_General_Details()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegUpd");

        var existing = (await (await Client.GetAsync($"{BaseUrl}/{studentId}/general-details"))
            .Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions))!.Data!;

        var updateDto = new UpdateGeneralInfoDto
        {
            FormNumber = existing.FormNo,
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-11),
            Gender = GenderEnum.Female,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            FamilyCast = "OBC",
            City = "Nagpur",
            State = new Common.State("Maharashtra", "MH"),
            Country = new Common.Country("India", "IN"),
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/general-details", updateDto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FirstName.ShouldBe("UpdatedFirst");
        body.Data.LastName.ShouldBe("UpdatedLast");
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Student()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/general-details");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Update_Nonexistent_Student()
    {
        var dto = new UpdateGeneralInfoDto
        {
            FormNumber = "FORM-MISSING",
            FirstName = "Ghost",
            LastName = "Student",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-10),
            Gender = GenderEnum.Male,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            FamilyCast = "General",
            City = "Mumbai",
            State = new Common.State("Maharashtra", "MH"),
            Country = new Common.Country("India", "IN"),
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/general-details", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegAuth");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/general-details");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_All_General_Fields()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegRT");

        var existing = (await (await Client.GetAsync($"{BaseUrl}/{studentId}/general-details"))
            .Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions))!.Data!;

        var updateDto = new UpdateGeneralInfoDto
        {
            FormNumber = existing.FormNo,
            FirstName = "RoundTrip",
            MiddleName = "Mid",
            LastName = "Student",
            DateOfBirth = new DateTimeOffset(2012, 6, 15, 0, 0, 0, TimeSpan.Zero),
            Gender = GenderEnum.Male,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            SecondaryContactNo = TestDataHelper.UniquePhone(),
            Email = TestDataHelper.UniqueEmail("student"),
            FamilyCast = "SC",
            Nationality = "Indian",
            BirthPlace = "Pune",
            City = "Nashik",
            State = new Common.State("Maharashtra", "MH"),
            Country = new Common.Country("India", "IN"),
            Pincode = "422001",
            PreviousSchool = "Vidyaniketan School",
            LastClassPassed = "5th",
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/general-details", updateDto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/general-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FirstName.ShouldBe("RoundTrip");
        body.Data.MiddleName.ShouldBe("Mid");
        body.Data.LastName.ShouldBe("Student");
        body.Data.FamilyCast.ShouldBe("SC");
        body.Data.PreviousSchool.ShouldBe("Vidyaniketan School");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Overwrite_General_Details_On_Second_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegOwr");

        var existing = (await (await Client.GetAsync($"{BaseUrl}/{studentId}/general-details"))
            .Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions))!.Data!;

        var firstUpdate = new UpdateGeneralInfoDto
        {
            FormNumber = existing.FormNo,
            FirstName = "FirstUpdate",
            LastName = "TestStudent",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-9),
            Gender = GenderEnum.Male,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            FamilyCast = "General",
            City = "Aurangabad",
            State = new Common.State("Maharashtra", "MH"),
            Country = new Common.Country("India", "IN"),
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/general-details", firstUpdate, JsonOptions);

        var secondUpdate = new UpdateGeneralInfoDto
        {
            FormNumber = existing.FormNo,
            FirstName = "SecondUpdate",
            LastName = "TestStudent",
            DateOfBirth = DateTimeOffset.UtcNow.AddYears(-9),
            Gender = GenderEnum.Female,
            PrimaryContactNo = TestDataHelper.UniquePhone(),
            FamilyCast = "ST",
            City = "Kolhapur",
            State = new Common.State("Maharashtra", "MH"),
            Country = new Common.Country("India", "IN"),
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/general-details", secondUpdate, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/general-details");
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FirstName.ShouldBe("SecondUpdate");
        body.Data.FamilyCast.ShouldBe("ST");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Isolate_General_Details_Between_Students()
    {
        var id1 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegIso1");
        var id2 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "RegIso2");

        var details1 = await (await Client.GetAsync($"{BaseUrl}/{id1}/general-details"))
            .Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        var details2 = await (await Client.GetAsync($"{BaseUrl}/{id2}/general-details"))
            .Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);

        details1.ShouldNotBeNull();
        details2.ShouldNotBeNull();
        details1.Data.ShouldNotBeNull();
        details2.Data.ShouldNotBeNull();
        details1.Data.StudentId.ShouldNotBe(details2.Data.StudentId);
    }

    #endregion
}
