using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

/// <summary>
/// API-level tests for student registration additional-details endpoints.
/// Validates HTTP pipeline, auth, and response contracts for
/// GET/PUT /api/student/enrollment/registrations/{id}/additional-details.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class RegistrationAdditionalDetailsApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/student/enrollment/registrations";

    public RegistrationAdditionalDetailsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    #region Tier 0 — Critical

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Additional_Details_After_Upsert()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetGetUps");

        var seed = new UpdateAdditionalDetailsDto { SecondLanguage = "Hindi" };
        var putResponse = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", seed, JsonOptions);
        putResponse.EnsureSuccessStatusCode();

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.SecondLanguage.ShouldBe("Hindi");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Additional_Details_Via_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetPut");

        var dto = new UpdateAdditionalDetailsDto
        {
            Category = Category.General,
            IsBPLStudent = false,
            BPLCardNo = "BPL-12345",
            SecondLanguage = "Marathi",
            HaveDisability = false,
            IsLibraryMember = true,
            IsTransportMember = false,
            IsSingleChild = true,
        };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_Existing_Additional_Details()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetUpd");

        var initial = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-INIT",
            SecondLanguage = "Hindi",
            IsLibraryMember = false,
        };
        var createResponse = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", initial, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var updated = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-UPDATED",
            SecondLanguage = "Tamil",
            IsLibraryMember = true,
            IsTransportMember = true,
            HaveDisability = true,
            IsBPLStudent = true,
        };
        var updateResponse = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", updated, JsonOptions);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.BPLCardNo.ShouldBe("BPL-UPDATED");
        body.Data.SecondLanguage.ShouldBe("Tamil");
        body.Data.IsLibraryMember.ShouldBeTrue();
        body.Data.IsTransportMember.ShouldBeTrue();
        body.Data.HaveDisability.ShouldBeTrue();
        body.Data.IsBPLStudent.ShouldBeTrue();
    }

    #endregion

    #region Tier 1 — Important

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Get_Nonexistent_Student()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/additional-details");

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_For_Update_Nonexistent_Student()
    {
        var dto = new UpdateAdditionalDetailsDto { SecondLanguage = "Hindi" };

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/additional-details", dto, JsonOptions);

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Handle_Put_With_All_Default_Values()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetDef");

        var dto = new UpdateAdditionalDetailsDto();

        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.BPLCardNo.ShouldBeNull();
        body.Data.SecondLanguage.ShouldBeNull();
        body.Data.IsBPLStudent.ShouldBeFalse();
        body.Data.HaveDisability.ShouldBeFalse();
        body.Data.IsLibraryMember.ShouldBeFalse();
        body.Data.IsTransportMember.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Be_Idempotent_On_Repeated_Put()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetIdem");

        var dto = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-IDEM",
            SecondLanguage = "Kannada",
            HaveDisability = true,
            IsLibraryMember = true,
            IsTransportMember = false,
        };

        var r1 = await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);
        r1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var r2 = await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);
        r2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.BPLCardNo.ShouldBe("BPL-IDEM");
        body.Data.HaveDisability.ShouldBeTrue();
        body.Data.SecondLanguage.ShouldBe("Kannada");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Authenticated_Request_Should_Not_Return_401()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetAuth");

        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details",
            new UpdateAdditionalDetailsDto { SecondLanguage = "Hindi" }, JsonOptions);

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tier 2 — Extended

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_All_Fields()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetRT");

        var dto = new UpdateAdditionalDetailsDto
        {
            Category = Category.Rte,
            IsBPLStudent = true,
            BPLCardNo = "BPL-VERIFY",
            SecondLanguage = "Sanskrit",
            HaveDisability = true,
            EmergencyRelativeName = "Ramesh Sharma",
            EmergencyRelation = "Uncle",
            EmergencyContactNo = TestDataHelper.UniquePhone(),
            IsSingleChild = true,
            IsSingleParentChild = false,
            IsLibraryMember = false,
            IsTransportMember = true,
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.IsBPLStudent.ShouldBeTrue();
        body.Data.BPLCardNo.ShouldBe("BPL-VERIFY");
        body.Data.SecondLanguage.ShouldBe("Sanskrit");
        body.Data.HaveDisability.ShouldBeTrue();
        body.Data.EmergencyRelativeName.ShouldBe("Ramesh Sharma");
        body.Data.EmergencyRelation.ShouldBe("Uncle");
        body.Data.IsLibraryMember.ShouldBeFalse();
        body.Data.IsTransportMember.ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Clear_All_Optional_Fields_With_Null()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetClr");

        var populate = new UpdateAdditionalDetailsDto
        {
            IsBPLStudent = true,
            BPLCardNo = "BPL-CLEAR",
            SecondLanguage = "Urdu",
            HaveDisability = true,
            IsLibraryMember = true,
            IsTransportMember = true,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", populate, JsonOptions);

        var clear = new UpdateAdditionalDetailsDto
        {
            IsBPLStudent = false,
            BPLCardNo = null,
            SecondLanguage = null,
            HaveDisability = false,
            IsLibraryMember = false,
            IsTransportMember = false,
        };
        var response = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{studentId}/additional-details", clear, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.BPLCardNo.ShouldBeNull();
        body.Data.SecondLanguage.ShouldBeNull();
        body.Data.IsBPLStudent.ShouldBeFalse();
        body.Data.IsLibraryMember.ShouldBeFalse();
        body.Data.IsTransportMember.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_All_Boolean_Fields_As_True()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetBoolT");

        var dto = new UpdateAdditionalDetailsDto
        {
            IsBPLStudent = true,
            HaveDisability = true,
            IsLibraryMember = true,
            IsTransportMember = true,
            IsSingleChild = true,
            IsSingleParentChild = true,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.IsBPLStudent.ShouldBeTrue();
        body.Data.HaveDisability.ShouldBeTrue();
        body.Data.IsLibraryMember.ShouldBeTrue();
        body.Data.IsTransportMember.ShouldBeTrue();
        body.Data.IsSingleChild.ShouldBeTrue();
        body.Data.IsSingleParentChild.ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_RoundTrip_All_Boolean_Fields_As_False_After_True()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetBoolF");

        var setTrue = new UpdateAdditionalDetailsDto
        {
            IsBPLStudent = true,
            HaveDisability = true,
            IsLibraryMember = true,
            IsTransportMember = true,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", setTrue, JsonOptions);

        var setFalse = new UpdateAdditionalDetailsDto
        {
            IsBPLStudent = false,
            HaveDisability = false,
            IsLibraryMember = false,
            IsTransportMember = false,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", setFalse, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.IsBPLStudent.ShouldBeFalse();
        body.Data.HaveDisability.ShouldBeFalse();
        body.Data.IsLibraryMember.ShouldBeFalse();
        body.Data.IsTransportMember.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Overwrite_Previous_Values_Completely()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetOvr");

        var first = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-FIRST",
            SecondLanguage = "Hindi",
            IsLibraryMember = true,
            IsBPLStudent = true,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", first, JsonOptions);

        var second = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-SECOND",
            SecondLanguage = "Bengali",
            IsLibraryMember = false,
            IsBPLStudent = false,
        };
        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", second, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.BPLCardNo.ShouldBe("BPL-SECOND");
        body.Data.SecondLanguage.ShouldBe("Bengali");
        body.Data.IsLibraryMember.ShouldBeFalse();
        body.Data.IsBPLStudent.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Isolate_Additional_Details_Between_Students()
    {
        var id1 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetIso1");
        var id2 = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetIso2");

        var dto1 = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-STU1",
            SecondLanguage = "Hindi",
            IsLibraryMember = true,
        };
        var dto2 = new UpdateAdditionalDetailsDto
        {
            BPLCardNo = "BPL-STU2",
            SecondLanguage = "Tamil",
            IsTransportMember = true,
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{id1}/additional-details", dto1, JsonOptions);
        await Client.PutAsJsonAsync($"{BaseUrl}/{id2}/additional-details", dto2, JsonOptions);

        var body1 = await (await Client.GetAsync($"{BaseUrl}/{id1}/additional-details"))
            .Content.ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        var body2 = await (await Client.GetAsync($"{BaseUrl}/{id2}/additional-details"))
            .Content.ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);

        body1.ShouldNotBeNull();
        body1.Data.ShouldNotBeNull();
        body1.Data.BPLCardNo.ShouldBe("BPL-STU1");
        body1.Data.SecondLanguage.ShouldBe("Hindi");
        body1.Data.IsLibraryMember.ShouldBeTrue();
        body1.Data.IsTransportMember.ShouldBeFalse();

        body2.ShouldNotBeNull();
        body2.Data.ShouldNotBeNull();
        body2.Data.BPLCardNo.ShouldBe("BPL-STU2");
        body2.Data.SecondLanguage.ShouldBe("Tamil");
        body2.Data.IsLibraryMember.ShouldBeFalse();
        body2.Data.IsTransportMember.ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Handle_Emergency_Contact_Fields()
    {
        var studentId = await StudentEnrollmentTestHelper
            .CreateRegisteredStudentAndGetIdAsync(Client, "AddDetEmrg");

        var dto = new UpdateAdditionalDetailsDto
        {
            EmergencyRelativeName = "Priya Sharma",
            EmergencyRelation = "Aunt",
            EmergencyContactNo = TestDataHelper.UniquePhone(),
        };

        await Client.PutAsJsonAsync($"{BaseUrl}/{studentId}/additional-details", dto, JsonOptions);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
        var body = await getResponse.Content
            .ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.EmergencyRelativeName.ShouldBe("Priya Sharma");
        body.Data.EmergencyRelation.ShouldBe("Aunt");
        body.Data.EmergencyContactNo.ShouldNotBeNullOrWhiteSpace();
    }

    #endregion
}
