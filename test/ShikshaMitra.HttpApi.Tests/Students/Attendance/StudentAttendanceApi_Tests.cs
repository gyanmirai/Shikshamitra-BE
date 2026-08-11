using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Records.Profiles;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentAttendanceApi_Tests : ShikshaMitraApiTestBase
{
    private const string AttendanceBaseUrl = "/api/students/attendance";

    public StudentAttendanceApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Mark_And_Read_Attendance_For_Enrolled_Student()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "AttendanceMark");
        var attendanceDate = DateTime.Today;

        var beforeResponse = await Client.GetAsync(
            $"{AttendanceBaseUrl}/students?academicYearId={Factory.SeededAcademicYearId}&standardId={context.StandardId}&divisionId={Factory.SeededDivisionId}&date={attendanceDate:yyyy-MM-dd}");

        beforeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var before = await beforeResponse.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions);
        before.ShouldNotBeNull();
        before.Students.ShouldContain(s => s.StudentId == student.StudentId);

        var markInput = new MarkAttendanceInput
        {
            AcademicYearId = Factory.SeededAcademicYearId,
            StandardId = context.StandardId,
            DivisionId = Factory.SeededDivisionId,
            Date = attendanceDate,
            Entries =
            {
                new AttendanceEntryDto
                {
                    StudentId = student.StudentId,
                    Status = AttendanceStatus.Present,
                    Remarks = "API regression mark"
                }
            }
        };

        var markResponse = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", markInput, JsonOptions);

        markResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var markOutput = await markResponse.Content.ReadFromJsonAsync<MarkAttendanceOutput>(JsonOptions);
        markOutput.ShouldNotBeNull();
        markOutput.TotalMarked.ShouldBe(1);
        markOutput.PresentCount.ShouldBe(1);

        var afterResponse = await Client.GetAsync(
            $"{AttendanceBaseUrl}/students?academicYearId={Factory.SeededAcademicYearId}&standardId={context.StandardId}&divisionId={Factory.SeededDivisionId}&date={attendanceDate:yyyy-MM-dd}");

        afterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await afterResponse.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions);
        after.ShouldNotBeNull();
        var markedStudent = after.Students.Single(s => s.StudentId == student.StudentId);
        markedStudent.Status.ShouldBe(AttendanceStatus.Present);
        markedStudent.Remarks.ShouldBe("API regression mark");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Cumulative_Present_Days_And_Attendance_Percentage()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "AttendanceCumulative");
        var firstDate = DateTime.Today.AddDays(-8);
        var secondDate = DateTime.Today.AddDays(-7);
        var thirdDate = DateTime.Today.AddDays(-6);

        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Present, firstDate, "Cumulative present 1");
        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Absent, secondDate, "Cumulative absent");
        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Present, thirdDate, "Cumulative present 2");

        var response = await Client.GetAsync(
            $"{AttendanceBaseUrl}/students?academicYearId={Factory.SeededAcademicYearId}&standardId={context.StandardId}&divisionId={Factory.SeededDivisionId}&date={DateTime.Today:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions);
        body.ShouldNotBeNull();
        var attendanceStudent = body.Students.Single(s => s.StudentId == student.StudentId);
        attendanceStudent.PresentDays.ShouldBe(2);
        attendanceStudent.AttendancePercentage.ShouldBe(66.67, 0.01);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Attendance_History_For_Selected_Student_Range_With_Paging()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "AttendanceHistory");
        var otherStudent = await CreateEnrolledStudentAsync(context, "AttendanceHistoryOther");
        var earlierDate = DateTime.Today.AddDays(-12);
        var laterDate = earlierDate.AddDays(2);

        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Absent, earlierDate, "History earlier");
        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Present, laterDate, "History later");
        await MarkAttendanceAsync(otherStudent.StudentId, AttendanceStatus.Present, laterDate, "Other student");

        var firstPageResponse = await Client.GetAsync(
            $"{AttendanceBaseUrl}/history?studentId={student.StudentId}&startDate={earlierDate:yyyy-MM-dd}&endDate={laterDate:yyyy-MM-dd}&sorting=date%20asc&skipCount=0&maxResultCount=1");
        firstPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPage = await firstPageResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions);

        firstPage.ShouldNotBeNull();
        firstPage.TotalCount.ShouldBe(2);
        firstPage.Items.Single().Date.Date.ShouldBe(earlierDate.Date);
        firstPage.Items.Single().Status.ShouldBe(AttendanceStatus.Absent);

        var secondPageResponse = await Client.GetAsync(
            $"{AttendanceBaseUrl}/history?studentId={student.StudentId}&startDate={earlierDate:yyyy-MM-dd}&endDate={laterDate:yyyy-MM-dd}&sorting=date%20asc&skipCount=1&maxResultCount=1");
        secondPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPage = await secondPageResponse.Content
            .ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions);

        secondPage.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(2);
        secondPage.Items.Single().Date.Date.ShouldBe(laterDate.Date);
        secondPage.Items.Single().Status.ShouldBe(AttendanceStatus.Present);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Filter_Attendance_History_By_Attendance_Day_Scope()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var currentDivisionId = await CreateDivisionAsync(context.StandardId, "HistoryCurrent");
        var student = await CreateEnrolledStudentAsync(context, "AttendanceHistoryScope");
        var attendanceDate = DateTime.Today.AddDays(-40);
        await MarkAttendanceAsync(
            student.StudentId,
            AttendanceStatus.Present,
            attendanceDate,
            "Old division history");
        await MoveStudentToDivisionAsync(student.StudentId, currentDivisionId);

        var currentDivisionUserId = Guid.NewGuid();
        await SeedDivisionScopeAsync(currentDivisionUserId, currentDivisionId);
        using var currentDivisionClient = CreateClientAs(currentDivisionUserId);

        var response = await currentDivisionClient.GetAsync(
            $"{AttendanceBaseUrl}/history?studentId={student.StudentId}&startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&skipCount=0&maxResultCount=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content
            .ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(0);
        body.Items.ShouldBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Attendance_History_When_Attendance_Day_Is_In_Scope()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var currentDivisionId = await CreateDivisionAsync(context.StandardId, "HistoryAllowed");
        var student = await CreateEnrolledStudentAsync(context, "AttendanceHistoryAllowed");
        var attendanceDate = DateTime.Today.AddDays(-41);
        await MarkAttendanceAsync(
            student.StudentId,
            AttendanceStatus.Absent,
            attendanceDate,
            "Allowed old division history");
        await MoveStudentToDivisionAsync(student.StudentId, currentDivisionId);

        var originalDivisionUserId = Guid.NewGuid();
        await SeedDivisionScopeAsync(originalDivisionUserId, Factory.SeededDivisionId);
        await SeedDivisionScopeAsync(originalDivisionUserId, currentDivisionId);
        using var originalDivisionClient = CreateClientAs(originalDivisionUserId);

        var response = await originalDivisionClient.GetAsync(
            $"{AttendanceBaseUrl}/history?studentId={student.StudentId}&startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&skipCount=0&maxResultCount=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content
            .ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(1);
        body.Items.Single().Status.ShouldBe(AttendanceStatus.Absent);
        body.Items.Single().Remarks.ShouldBe("Allowed old division history");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Require_Delete_Permission_To_Lock_Attendance()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "AttendanceLockPermission");
        var attendanceDate = DateTime.Today.AddDays(-2);
        await MarkAttendanceAsync(student.StudentId, AttendanceStatus.Present, attendanceDate, "Lock auth");

        var input = new LockAttendanceDayInput
        {
            AcademicYearId = Factory.SeededAcademicYearId,
            StandardId = context.StandardId,
            DivisionId = Factory.SeededDivisionId,
            Date = attendanceDate
        };

        using var noPermissionClient = CreatePermissionClient();
        var noPermissionResponse = await noPermissionClient.PostAsJsonAsync(
            $"{AttendanceBaseUrl}/lock",
            input,
            JsonOptions);
        noPermissionResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var addOnlyClient = CreatePermissionClient(
            ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add);
        var addOnlyResponse = await addOnlyClient.PostAsJsonAsync(
            $"{AttendanceBaseUrl}/lock",
            input,
            JsonOptions);
        addOnlyResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var deleteClient = CreatePermissionClient(
            ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete);
        var deleteResponse = await deleteClient.PostAsJsonAsync(
            $"{AttendanceBaseUrl}/lock",
            input,
            JsonOptions);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<StudentGeneralDetailsDto> CreateEnrolledStudentAsync(
        SeededDivisionContext context,
        string suffix,
        Guid? divisionId = null)
    {
        var targetDivisionId = divisionId ?? Factory.SeededDivisionId;
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(
            Client, suffix, targetDivisionId);

        await MoveRegistrationToEnrolledAsync(student.StudentId, targetDivisionId);
        return student;
    }

    private async Task MarkAttendanceAsync(
        Guid studentId,
        AttendanceStatus status,
        DateTime date,
        string remarks,
        Guid? divisionId = null)
    {
        var context = await ResolveSeededDivisionContextAsync();
        var targetDivisionId = divisionId ?? Factory.SeededDivisionId;
        var response = await Client.PostAsJsonAsync(
            $"{AttendanceBaseUrl}/mark",
            new MarkAttendanceInput
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                StandardId = context.StandardId,
                DivisionId = targetDivisionId,
                Date = date,
                Entries =
                {
                    new AttendanceEntryDto
                    {
                        StudentId = studentId,
                        Status = status,
                        Remarks = remarks
                    }
                }
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task MoveRegistrationToEnrolledAsync(Guid studentId, Guid? divisionId = null)
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
            student.AssignDivision(divisionId ?? Factory.SeededDivisionId);
            student.ApproveAdmission();

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task MoveStudentToDivisionAsync(Guid studentId, Guid divisionId)
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
            student.AssignDivision(divisionId);

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task<Guid> CreateDivisionAsync(Guid standardId, string namePrefix)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var divisionManager = scope.ServiceProvider.GetRequiredService<DivisionManager>();
        var divisionRepository = scope.ServiceProvider.GetRequiredService<IDivisionRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var division = await divisionManager.CreateAsync(
                standardId,
                $"{namePrefix}-{Guid.NewGuid():N}",
                60);
            await divisionRepository.InsertAsync(division, autoSave: false);
            await uow.CompleteAsync();
            return division.Id;
        }
    }

    private async Task SeedDivisionScopeAsync(Guid userId, Guid divisionId)
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
                    academicYearId: Factory.SeededAcademicYearId,
                    divisionId: divisionId,
                    description: "Student attendance scope regression test"),
                autoSave: false);

            await userScopeService.InvalidateUserScopeCacheAsync(userId);
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

    private HttpClient CreatePermissionClient(params string[] permissions)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AlwaysGrantPermissionChecker.TestPermissionsHeader,
            permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
        return client;
    }
}
