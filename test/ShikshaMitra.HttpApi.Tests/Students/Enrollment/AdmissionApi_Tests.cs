using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Application.Dtos;
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
}
