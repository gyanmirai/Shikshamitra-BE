using System;
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
/// API-level tests for student registration family-details endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET/PUT /api/student/enrollment/registrations/{id}/family-details.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class RegistrationFamilyDetailsApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student/enrollment/registrations";

    public RegistrationFamilyDetailsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Family_Details_For_Registered_Student()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamGet");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/family-details");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.StudentId.ShouldBe(studentId);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_Family_Details_With_Father_Info()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamFather");

        var dto = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto
            {
                FullName = "Ramesh TestStudent",
                Occupation = "Engineer",
                ContactNo = TestDataHelper.UniquePhone(),
                Qualification = "B.Tech",
            },
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/family-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FatherDetails.ShouldNotBeNull();
        body.Data.FatherDetails.FullName.ShouldBe("Ramesh TestStudent");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_Family_Details_With_All_Parents()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamAll");

        var dto = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto
            {
                FullName = "Vijay TestFather",
                Occupation = "Teacher",
                ContactNo = TestDataHelper.UniquePhone(),
            },
            MotherDetails = new ParentGuardianDto
            {
                FullName = "Sunita TestMother",
                Occupation = "Homemaker",
                ContactNo = TestDataHelper.UniquePhone(),
            },
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/family-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FatherDetails.ShouldNotBeNull();
        body.Data.MotherDetails.ShouldNotBeNull();
        body.Data.FatherDetails.FullName.ShouldBe("Vijay TestFather");
        body.Data.MotherDetails.FullName.ShouldBe("Sunita TestMother");
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Student()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/family-details");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Update_Nonexistent_Student()
    {
        var dto = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto { FullName = "Ghost Father" },
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/family-details", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Handle_Empty_Family_Details_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamEmpty");

        var dto = new UpdateFamilyInfoDto();

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/family-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FatherDetails.ShouldBeNull();
        body.Data.MotherDetails.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamAuth");

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/family-details");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_Father_Details_Completely()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamRTFather");

        var dto = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto
            {
                FullName = "Suresh Kumar",
                DateOfBirth = new DateTimeOffset(1975, 3, 10, 0, 0, 0, TimeSpan.Zero),
                Occupation = "Doctor",
                Email = TestDataHelper.UniqueEmail("father"),
                ContactNo = TestDataHelper.UniquePhone(),
                Qualification = "MBBS",
                Designation = "Senior Doctor",
                Organization = "City Hospital",
            },
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/family-details", dto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/family-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FatherDetails.ShouldNotBeNull();
        body.Data.FatherDetails.FullName.ShouldBe("Suresh Kumar");
        body.Data.FatherDetails.Occupation.ShouldBe("Doctor");
        body.Data.FatherDetails.Qualification.ShouldBe("MBBS");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Overwrite_Family_Details_On_Second_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamOvr");

        var first = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto { FullName = "First Father", Occupation = "Farmer" },
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/family-details", first, JsonOptions);

        var second = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto { FullName = "Updated Father", Occupation = "Businessman" },
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/family-details", second, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/family-details");
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.FatherDetails.ShouldNotBeNull();
        body.Data.FatherDetails.FullName.ShouldBe("Updated Father");
        body.Data.FatherDetails.Occupation.ShouldBe("Businessman");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Isolate_Family_Details_Between_Students()
    {
        var id1 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamIso1");
        var id2 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamIso2");

        var dto1 = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto { FullName = "Father of Student One" },
        };
        var dto2 = new UpdateFamilyInfoDto
        {
            FatherDetails = new ParentGuardianDto { FullName = "Father of Student Two" },
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{id1}/family-details", dto1, JsonOptions);
        await Client.PutAsJsonAsync($"{BaseUrl}/{id2}/family-details", dto2, JsonOptions);

        var body1 = await (await Client.GetAsync($"{BaseUrl}/{id1}/family-details"))
            .Content.ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        var body2 = await (await Client.GetAsync($"{BaseUrl}/{id2}/family-details"))
            .Content.ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);

        body1.ShouldNotBeNull();
        body1.Data.ShouldNotBeNull();
        body1.Data.FatherDetails.ShouldNotBeNull();
        body1.Data.FatherDetails.FullName.ShouldBe("Father of Student One");

        body2.ShouldNotBeNull();
        body2.Data.ShouldNotBeNull();
        body2.Data.FatherDetails.ShouldNotBeNull();
        body2.Data.FatherDetails.FullName.ShouldBe("Father of Student Two");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Update_Guardian_Details_When_No_Parents()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "FamGuard");

        var dto = new UpdateFamilyInfoDto
        {
            GuardianDetails = new ParentGuardianDto
            {
                FullName = "Uncle Guardian",
                ContactNo = TestDataHelper.UniquePhone(),
                Occupation = "Businessman",
            },
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/family-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentFamilyInfoDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.GuardianDetails.ShouldNotBeNull();
        body.Data.GuardianDetails.FullName.ShouldBe("Uncle Guardian");
        body.Data.FatherDetails.ShouldBeNull();
    }

    #endregion
}
