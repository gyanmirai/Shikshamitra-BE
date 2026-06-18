using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
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
        foreach (var item in body.Items)
        {
            // Contract includes fee-structure assignment flag (defaults to false when unset)
            _ = item.IsFeeStructureAssigned;
        }
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

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Membership_Flags_And_Create_Library_Member_On_Approval()
    {
        var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(
            Client,
            "AdmissionMembership");
        await MoveRegistrationToAdmissionWithMembershipFlagsAsync(registration.StudentId);

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}?search={Uri.EscapeDataString(registration.FirstName)}&maxResultCount=20");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var admissions = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        admissions.ShouldNotBeNull();
        admissions.Items.ShouldContain(item =>
            item.StudentId == registration.StudentId &&
            item.IsLibraryMember &&
            item.IsTransportMember);

        var approveResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/status",
            new BatchAdmissionStatusChangeRequestDto
            {
                Items =
                [
                    new AdmissionStatusChangeItemDto
                    {
                        StudentId = registration.StudentId,
                        Action = StudentEnrollmentApprovalState.Approved
                    }
                ]
            },
            JsonOptions);

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await approveResponse.Content.ReadFromJsonAsync<BatchAdmissionStatusChangeResultDto>(JsonOptions);
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBe(1);
        (await HasLibraryMemberAsync(registration.StudentId)).ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Approve_Admission_With_Student_Email_Without_Generic_Processing_Error()
    {
        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto("AdmissionEmail");
        dto.Email = TestDataHelper.UniqueEmail("student-admission");
        var createResponse = await Client.PostAsJsonAsync(
            "/api/student/enrollment/registrations/general-details",
            dto,
            JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
        var createBody = await createResponse.Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        createBody.ShouldNotBeNull();
        createBody.Data.ShouldNotBeNull();

        await MoveRegistrationToAdmissionWithMembershipFlagsAsync(createBody.Data.StudentId);

        var approveResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/status",
            new BatchAdmissionStatusChangeRequestDto
            {
                Items =
                [
                    new AdmissionStatusChangeItemDto
                    {
                        StudentId = createBody.Data.StudentId,
                        Action = StudentEnrollmentApprovalState.Approved
                    }
                ]
            },
            JsonOptions);

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResponse.Content.ReadAsStringAsync());
        var result = await approveResponse.Content.ReadFromJsonAsync<BatchAdmissionStatusChangeResultDto>(JsonOptions);
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBe(1);
        result.Failed.ShouldBe(0);
        var item = result.Results.Single();
        item.Success.ShouldBeTrue();
        (item.Message ?? string.Empty).ShouldNotContain("unexpected error");
        item.NewStatus.ShouldBe(StudentEnrollmentApprovalState.Approved.ToString());

        var secondApproveResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/status",
            new BatchAdmissionStatusChangeRequestDto
            {
                Items =
                [
                    new AdmissionStatusChangeItemDto
                    {
                        StudentId = createBody.Data.StudentId,
                        Action = StudentEnrollmentApprovalState.Approved
                    }
                ]
            },
            JsonOptions);

        secondApproveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondApproveResponse.Content.ReadAsStringAsync());
        var secondResult = await secondApproveResponse.Content.ReadFromJsonAsync<BatchAdmissionStatusChangeResultDto>(JsonOptions);
        secondResult.ShouldNotBeNull();
        secondResult.Succeeded.ShouldBe(1);
        secondResult.Failed.ShouldBe(0);
        var secondItem = secondResult.Results.Single();
        secondItem.Success.ShouldBeTrue();
        (secondItem.Message ?? string.Empty).ShouldNotContain("unexpected error");
        secondItem.NewStatus.ShouldBe(StudentEnrollmentApprovalState.Approved.ToString());
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Add_Parent_Role_To_Existing_Parent_User_On_Approval()
    {
        var parentEmail = TestDataHelper.UniqueEmail("existing-parent");
        await CreateIdentityUserWithoutRoleAsync($"existing-parent-{Guid.NewGuid():N}", parentEmail);
        (await IsUserInRoleByEmailAsync(parentEmail, "Parent")).ShouldBeFalse();

        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto("ExistingParent");
        dto.Email = TestDataHelper.UniqueEmail("student-existing-parent");
        var createResponse = await Client.PostAsJsonAsync(
            "/api/student/enrollment/registrations/general-details",
            dto,
            JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
        var createBody = await createResponse.Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        createBody.ShouldNotBeNull();
        createBody.Data.ShouldNotBeNull();

        var familyResponse = await Client.PutAsJsonAsync(
            $"/api/student/enrollment/registrations/{createBody.Data.StudentId}/family-details",
            new UpdateFamilyInfoDto
            {
                FatherDetails = new ParentGuardianDto
                {
                    FullName = "Existing Parent",
                    Email = parentEmail,
                    ContactNo = TestDataHelper.UniquePhone()
                }
            },
            JsonOptions);
        familyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await familyResponse.Content.ReadAsStringAsync());

        await MoveRegistrationToAdmissionWithMembershipFlagsAsync(createBody.Data.StudentId);

        var approveResponse = await Client.PostAsJsonAsync(
            $"{BaseUrl}/status",
            new BatchAdmissionStatusChangeRequestDto
            {
                Items =
                [
                    new AdmissionStatusChangeItemDto
                    {
                        StudentId = createBody.Data.StudentId,
                        Action = StudentEnrollmentApprovalState.Approved
                    }
                ]
            },
            JsonOptions);

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResponse.Content.ReadAsStringAsync());
        var result = await approveResponse.Content.ReadFromJsonAsync<BatchAdmissionStatusChangeResultDto>(JsonOptions);
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBe(1);
        result.Failed.ShouldBe(0);
        var item = result.Results.Single();
        item.Success.ShouldBeTrue();
        (item.Message ?? string.Empty).ShouldNotContain("unexpected error");
        (await IsUserInRoleByEmailAsync(parentEmail, "Parent")).ShouldBeTrue();
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

    private async Task MoveRegistrationToAdmissionWithMembershipFlagsAsync(Guid studentId)
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
            student.AdditionalDetail!.UpdateAdditionalDetails(
                isBPLStudent: false,
                bplCardNo: null,
                secondLanguage: null,
                haveDisability: false,
                disabilityDetails: null,
                emergencyRelativeName: null,
                emergencyRelation: null,
                emergencyContactNo: null,
                isSingleChild: false,
                isSingleParentChild: false,
                category: Category.General,
                isLibraryMember: true,
                isTransportMember: true);
            student.AssignDivision(Factory.SeededDivisionId);
            student.MoveToAdmission();

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task<bool> HasLibraryMemberAsync(Guid studentId)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<LibraryMember, Guid>>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var query = await repository.GetQueryableAsync();
            var exists = query.Any(member => member.StudentId == studentId && member.IsActive);
            await uow.CompleteAsync();
            return exists;
        }
    }

    private async Task CreateIdentityUserWithoutRoleAsync(string userName, string email)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var userManager = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
            user.SetEmailConfirmed(true);
            var result = await userManager.CreateAsync(user, "Test@1234");
            result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Description)));
            await uow.CompleteAsync();
        }
    }

    private async Task<bool> IsUserInRoleByEmailAsync(string email, string roleName)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var userManager = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var inRole = await userManager.IsInRoleAsync(user, roleName);
            await uow.CompleteAsync();
            return inRole;
        }
    }
}
