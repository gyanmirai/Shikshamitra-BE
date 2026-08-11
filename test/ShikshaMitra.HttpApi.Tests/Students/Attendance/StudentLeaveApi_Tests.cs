using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Records.Profiles;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentLeaveApi_Tests : ShikshaMitraApiTestBase
{
    private const string LeaveBaseUrl = "/api/students/attendance/leaves";
    private const string LeaveTypeBaseUrl = "/api/students/attendance/settings/leave-types";

    public StudentLeaveApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_List_And_Approve_Student_Leave()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "LeaveFlow");
        var leaveType = await CreateLeaveTypeAsync();
        var leaveDate = DateTimeOffset.UtcNow.Date.AddDays(2);

        var createInput = new CreateStudentLeaveDto
        {
            StudentId = student.StudentId,
            StudentLeaveTypeId = leaveType.Id,
            IsHalfDay = false,
            LeaveDate = leaveDate,
            EndDate = leaveDate,
            Description = "Medical appointment regression leave"
        };

        var createResponse = await Client.PostAsJsonAsync(LeaveBaseUrl, createInput, JsonOptions);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<StudentLeaveDto>(JsonOptions);
        created.ShouldNotBeNull();
        created.StudentId.ShouldBe(student.StudentId);
        created.Status.ShouldBe(StudentLeaveStatusEnum.Pending);
        created.TotalDays.ShouldBe(1);

        var pendingList = await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Pending);
        pendingList.Items.ShouldContain(l => l.Id == created.Id);

        var approveResponse = await Client.PostAsJsonAsync(
            $"{LeaveBaseUrl}/{created.Id}/approve",
            new ApproveRejectStudentLeaveDto
            {
                Status = StudentLeaveStatusEnum.Approved,
                Remarks = "Approved by API regression"
            },
            JsonOptions);

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = await approveResponse.Content.ReadFromJsonAsync<StudentLeaveDto>(JsonOptions);
        approved.ShouldNotBeNull();
        approved.Status.ShouldBe(StudentLeaveStatusEnum.Approved);
        approved.Remarks.ShouldBe("Approved by API regression");

        var approvedList = await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Approved);
        approvedList.Items.Single(l => l.Id == created.Id).Status.ShouldBe(StudentLeaveStatusEnum.Approved);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Page_And_Sort_Student_Leave_List_Server_Side()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "LeavePaging");
        var leaveType = await CreateLeaveTypeAsync();
        var earlierDate = DateTimeOffset.UtcNow.Date.AddDays(4);
        var laterDate = earlierDate.AddDays(5);
        var earlier = await CreateLeaveAsync(student.StudentId, leaveType.Id, earlierDate, "Earlier server sorted leave");
        var later = await CreateLeaveAsync(student.StudentId, leaveType.Id, laterDate, "Later server sorted leave");

        var ascendingFirstPage = await GetLeaveListAsync(
            student.StudentId,
            sorting: "formattedLeaveDate ASC",
            skipCount: 0,
            maxResultCount: 1);
        var ascendingSecondPage = await GetLeaveListAsync(
            student.StudentId,
            sorting: "formattedLeaveDate ASC",
            skipCount: 1,
            maxResultCount: 1);
        var descendingFirstPage = await GetLeaveListAsync(
            student.StudentId,
            sorting: "formattedLeaveDate DESC",
            skipCount: 0,
            maxResultCount: 1);

        ascendingFirstPage.TotalCount.ShouldBe(2);
        ascendingFirstPage.Items.Single().Id.ShouldBe(earlier.Id);
        ascendingSecondPage.Items.Single().Id.ShouldBe(later.Id);
        descendingFirstPage.Items.Single().Id.ShouldBe(later.Id);
    }

    private async Task<StudentLeaveTypeDto> CreateLeaveTypeAsync()
    {
        var response = await Client.PostAsJsonAsync(
            LeaveTypeBaseUrl,
            new CreateUpdateStudentLeaveTypeDto
            {
                LeaveTypeName = $"API Sick {Guid.NewGuid():N}"[..20],
                Color = "#2F80ED",
                IsActive = true
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<StudentLeaveTypeDto>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave type response");
    }

    private async Task<PagedResultDto<StudentLeaveDto>> GetLeaveListAsync(
        Guid studentId,
        StudentLeaveStatusEnum status)
    {
        var response = await Client.GetAsync(
            $"{LeaveBaseUrl}?studentId={studentId}&status={status:D}&skipCount=0&maxResultCount=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<PagedResultDto<StudentLeaveDto>>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave list response");
    }

    private async Task<PagedResultDto<StudentLeaveDto>> GetLeaveListAsync(
        Guid studentId,
        string sorting,
        int skipCount,
        int maxResultCount)
    {
        var response = await Client.GetAsync(
            $"{LeaveBaseUrl}?studentId={studentId}&sorting={Uri.EscapeDataString(sorting)}&skipCount={skipCount}&maxResultCount={maxResultCount}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<PagedResultDto<StudentLeaveDto>>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave list response");
    }

    private async Task<StudentLeaveDto> CreateLeaveAsync(
        Guid studentId,
        Guid leaveTypeId,
        DateTimeOffset leaveDate,
        string description)
    {
        var response = await Client.PostAsJsonAsync(
            LeaveBaseUrl,
            new CreateStudentLeaveDto
            {
                StudentId = studentId,
                StudentLeaveTypeId = leaveTypeId,
                IsHalfDay = false,
                LeaveDate = leaveDate,
                EndDate = leaveDate,
                Description = description
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<StudentLeaveDto>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave create response");
    }

    private async Task<StudentGeneralDetailsDto> CreateEnrolledStudentAsync(
        SeededDivisionContext context,
        string suffix)
    {
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(
            Client, suffix, Factory.SeededDivisionId);

        await MoveRegistrationToEnrolledAsync(student.StudentId);
        return student;
    }

    private async Task MoveRegistrationToEnrolledAsync(Guid studentId)
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
            student.MoveToAdmission();
            student.AssignDivision(Factory.SeededDivisionId);
            student.ApproveAdmission();

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task<SeededDivisionContext> ResolveSeededDivisionContextAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var divisionRepository = scope.ServiceProvider.GetRequiredService<IDivisionRepository>();
        var standardRepository = scope.ServiceProvider.GetRequiredService<IStandardRepository>();
        var mediumStageRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumStageRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var division = await divisionRepository.GetAsync(Factory.SeededDivisionId);
            var standard = await standardRepository.GetAsync(division.StandardId);
            var mediumStage = await mediumStageRepository.GetAsync(standard.EducationMediumStageId);
            await uow.CompleteAsync();

            return new SeededDivisionContext(division.StandardId, mediumStage.EducationMediumId);
        }
    }

    private sealed record SeededDivisionContext(Guid StandardId, Guid AcademicMediumId);
}
