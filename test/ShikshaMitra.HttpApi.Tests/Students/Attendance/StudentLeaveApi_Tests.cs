using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
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
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var coherentStudent = new Student(student.StudentId, tenantId, "Scope fixture", null, "Included",
            context.AcademicYearId, context.AcademicBoardId, context.AcademicMediumId, context.AcademicMediumStageId,
            context.StandardId, context.DivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
        var otherDivisionStudent = new Student(Guid.NewGuid(), tenantId, "Scope fixture", null, "Other division",
            context.AcademicYearId, context.AcademicBoardId, context.AcademicMediumId, context.AcademicMediumStageId,
            context.StandardId, Guid.NewGuid(), status: StudentLifeCycleStatusEnum.Enrolled);
        var scopeCandidates = new[] { coherentStudent, otherDivisionStudent }.AsQueryable();
        var tenantWide = new UserScopeContext { TenantId = tenantId, HasWildcardAcademicYear = true };
        tenantWide.IsAdmin.ShouldBeFalse(); tenantWide.AcademicYearIds.ShouldBeEmpty();
        StudentScopeAccessService.ApplyScope(scopeCandidates, tenantWide).Count().ShouldBe(2);
        var narrow = new UserScopeContext { TenantId = tenantId, AcademicYearIds = [context.AcademicYearId], DivisionIds = [context.DivisionId] };
        StudentScopeAccessService.ApplyScope(scopeCandidates, narrow).Single().Id.ShouldBe(student.StudentId);
        StudentScopeAccessService.ApplyScope(scopeCandidates, new UserScopeContext { TenantId = tenantId }).ShouldBeEmpty();
        var leaveType = await CreateLeaveTypeAsync();
        var leaveDate = context.ReferenceDate.AddDays(2);

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

        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
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

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResponse.Content.ReadAsStringAsync());
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
        var earlierDate = context.ReferenceDate.AddDays(4);
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

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Overlapping_Approval_On_The_Same_School_Day_Without_Changing_Leave_State()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "LeaveOverlap");
        var type = await CreateLeaveTypeAsync();
        var schoolMorning = context.ReferenceDate.AddDays(6);
        var schoolAfternoonInUtc = schoolMorning.AddHours(13).ToUniversalTime();
        var first = await CreateLeaveAsync(student.StudentId, type.Id, schoolMorning, "School morning leave");
        var second = await CreateLeaveAsync(student.StudentId, type.Id, schoolAfternoonInUtc, "Same school day leave using UTC");
        var approved = await Client.PostAsJsonAsync($"{LeaveBaseUrl}/{first.Id}/approve",
            new ApproveRejectStudentLeaveDto { Status = StudentLeaveStatusEnum.Approved, Remarks = "First leave approval" }, JsonOptions);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        var conflict = await Client.PostAsJsonAsync($"{LeaveBaseUrl}/{second.Id}/approve",
            new ApproveRejectStudentLeaveDto { Status = StudentLeaveStatusEnum.Approved, Remarks = "Conflicting leave approval" }, JsonOptions);
        conflict.IsSuccessStatusCode.ShouldBeFalse();
        (await conflict.Content.ReadAsStringAsync()).ShouldContain("OverlappingApprovedLeave");
        var pending = await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Pending);
        pending.Items.ShouldContain(l => l.Id == second.Id);
        var approvedList = await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Approved);
        approvedList.Items.Single().Id.ShouldBe(first.Id);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Require_Add_Permission_And_Valid_Approval_Status_Without_Mutating_Pending_Leave()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "LeaveDeniedApproval");
        var type = await CreateLeaveTypeAsync();
        var leave = await CreateLeaveAsync(student.StudentId, type.Id, context.ReferenceDate.AddDays(7), "Protected leave approval");
        var input = new ApproveRejectStudentLeaveDto { Status = StudentLeaveStatusEnum.Approved };
        using var none = PermissionClient();
        (await none.PostAsJsonAsync($"{LeaveBaseUrl}/{leave.Id}/approve", input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var view = PermissionClient(ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View);
        (await view.PostAsJsonAsync($"{LeaveBaseUrl}/{leave.Id}/approve", input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        input.Status = StudentLeaveStatusEnum.Rejected;
        var invalid = await Client.PostAsJsonAsync($"{LeaveBaseUrl}/{leave.Id}/approve", input, JsonOptions);
        invalid.IsSuccessStatusCode.ShouldBeFalse();
        (await invalid.Content.ReadAsStringAsync()).ShouldContain("InvalidApproveStatus");
        (await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Pending)).Items.Single().Id.ShouldBe(leave.Id);
        (await GetLeaveListAsync(student.StudentId, StudentLeaveStatusEnum.Approved)).Items.ShouldBeEmpty();
    }

    private HttpClient PermissionClient(params string[] permissions)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
        return client;
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<StudentLeaveTypeDto>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave type response");
    }

    private async Task<PagedResultDto<StudentLeaveDto>> GetLeaveListAsync(
        Guid studentId,
        StudentLeaveStatusEnum status)
    {
        var response = await Client.GetAsync(
            $"{LeaveBaseUrl}?studentId={studentId}&status={status:D}&skipCount=0&maxResultCount=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<StudentLeaveDto>(JsonOptions)
               ?? throw new InvalidOperationException("Null leave create response");
    }

    private async Task<StudentGeneralDetailsDto> CreateEnrolledStudentAsync(
        SeededDivisionContext context,
        string suffix)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        using (services.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var student = new Student(Guid.NewGuid(), tenantId, suffix, null, "Leave fixture", context.AcademicYearId,
                context.AcademicBoardId, context.AcademicMediumId, context.AcademicMediumStageId, context.StandardId,
                context.DivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
            await services.GetRequiredService<IStudentRepository>().InsertAsync(student);
            var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenantId, student.Id, context.AcademicYearId,
                context.StandardId, context.DivisionId, student.RollNo);
            enrollment.ConfirmAttendanceStart(context.YearStartDate);
            await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment);
            await uow.CompleteAsync();
            return new StudentGeneralDetailsDto { StudentId = student.Id };
        }
    }

    private async Task<SeededDivisionContext> ResolveSeededDivisionContextAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).Date;
            var academic = await AttendanceApiSeedContext.ReadAsync(scope.ServiceProvider, tenantId, null, today);
            var referenceDate = today < academic.YearEnd.AddDays(-20).Date ? today : academic.YearEnd.AddDays(-20).Date;
            await uow.CompleteAsync();

            return new SeededDivisionContext(academic.StandardId, academic.MediumId, academic.StageId,
                academic.BoardId, academic.YearId, academic.DivisionId, academic.YearStart.Date, new DateTimeOffset(referenceDate, TimeSpan.FromHours(5.5)));
        }
    }

    private sealed record SeededDivisionContext(Guid StandardId, Guid AcademicMediumId, Guid AcademicMediumStageId,
        Guid AcademicBoardId, Guid AcademicYearId, Guid DivisionId, DateTime YearStartDate, DateTimeOffset ReferenceDate);
}
