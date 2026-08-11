using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Records.Profiles;
using ShikshaMitra.Students.StudentEnrollments;
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

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentRecordsListApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/students/records/list";

    public StudentRecordsListApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Forbidden_When_Student_List_Permission_Is_Missing()
    {
        using var client = CreatePermissionClient();

        var response = await client.GetAsync(
            $"{BaseUrl}/{Factory.SeededDivisionId}?skipCount=0&maxResultCount=1");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Require_Student_Management_View_For_Management_Reads()
    {
        using var listOnlyClient = CreatePermissionClient(
            ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);

        var forbiddenResponse = await listOnlyClient.GetAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/gr-projections");
        forbiddenResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var managementClient = CreatePermissionClient(
            ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View);

        var allowedResponse = await managementClient.GetAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/gr-projections");
        allowedResponse.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Not_Expose_Removed_Bulk_Upload_Endpoint()
    {
        using var viewOnlyClient = CreatePermissionClient(
            ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View);
        using var content = new StringContent("removed endpoint probe");

        var response = await viewOnlyClient.PostAsync("/api/students/bulk-upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Duplicate_Gr_Numbers_Before_Save()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrDupA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrDupB");
        var grBase = Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 700000;

        var okResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase },
                new() { StudentId = second.StudentId, GrNo = grBase + 1 },
            },
            JsonOptions);
        okResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await okResponse.Content.ReadAsStringAsync());

        var duplicateBatchResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase + 2 },
                new() { StudentId = second.StudentId, GrNo = grBase + 2 },
            },
            JsonOptions);
        duplicateBatchResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var duplicateBatchContent = await duplicateBatchResponse.Content.ReadAsStringAsync();
        duplicateBatchContent.ShouldContain(StudentErrorCodes.DuplicateGrNo);
        duplicateBatchContent.ShouldContain($"GR Number {grBase + 2} is already assigned to another student.");
        ReadProblemDetail(duplicateBatchContent).ShouldBe(
            $"GR Number {grBase + 2} is already assigned to another student.");
        var afterDuplicateBatch = await GetGrNumbersAsync(first.StudentId, second.StudentId);
        afterDuplicateBatch[first.StudentId].ShouldBe(grBase);
        afterDuplicateBatch[second.StudentId].ShouldBe(grBase + 1);

        var existingConflictResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase + 1 },
            },
            JsonOptions);
        existingConflictResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var existingConflictContent = await existingConflictResponse.Content.ReadAsStringAsync();
        existingConflictContent.ShouldContain(StudentErrorCodes.DuplicateGrNo);
        existingConflictContent.ShouldContain($"GR Number {grBase + 1} is already assigned to another student.");
        ReadProblemDetail(existingConflictContent).ShouldBe(
            $"GR Number {grBase + 1} is already assigned to another student.");
        var afterExistingConflict = await GetGrNumbersAsync(first.StudentId, second.StudentId);
        afterExistingConflict[first.StudentId].ShouldBe(grBase);
        afterExistingConflict[second.StudentId].ShouldBe(grBase + 1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Gr_Number_Transfer_From_Student_In_Same_Batch()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrTransferA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrTransferB");
        var grBase = Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 800000;

        var initialResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase },
                new() { StudentId = second.StudentId, GrNo = grBase + 1 },
            },
            JsonOptions);
        initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await initialResponse.Content.ReadAsStringAsync());

        var transferResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase + 1 },
                new() { StudentId = second.StudentId, GrNo = grBase + 2 },
            },
            JsonOptions);

        transferResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var transferContent = await transferResponse.Content.ReadAsStringAsync();
        transferContent.ShouldContain(StudentErrorCodes.DuplicateGrNo);
        transferContent.ShouldContain($"GR Number {grBase + 1} is already assigned to another student.");
        var grNumbers = await GetGrNumbersAsync(first.StudentId, second.StudentId);
        grNumbers[first.StudentId].ShouldBe(grBase);
        grNumbers[second.StudentId].ShouldBe(grBase + 1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Allow_Student_To_Keep_Own_Gr_Number_In_Batch()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrKeepA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "GrKeepB");
        var grBase = Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 900000;

        var initialResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase },
                new() { StudentId = second.StudentId, GrNo = grBase + 1 },
            },
            JsonOptions);
        initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await initialResponse.Content.ReadAsStringAsync());

        var retainResponse = await Client.PostAsJsonAsync(
            "/api/students/records/gr-numbers",
            new List<UpdateStudentGrNumberDto>
            {
                new() { StudentId = first.StudentId, GrNo = grBase },
                new() { StudentId = second.StudentId, GrNo = grBase + 2 },
            },
            JsonOptions);

        retainResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await retainResponse.Content.ReadAsStringAsync());
        var grNumbers = await GetGrNumbersAsync(first.StudentId, second.StudentId);
        grNumbers[first.StudentId].ShouldBe(grBase);
        grNumbers[second.StudentId].ShouldBe(grBase + 2);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public void Should_Not_Keep_Bulk_Upload_App_Service()
    {
        typeof(StudentRecordsListApi_Tests).Assembly.GetType(
            "ShikshaMitra.Students.Records.StudentBulkUploadAppService").ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_List_Enrolled_Students_By_Division_With_Search()
    {
        var included = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsListIncluded");
        var excluded = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsListExcluded");

        await MoveRegistrationToEnrolledAsync(included.StudentId);
        await MoveRegistrationToEnrolledAsync(excluded.StudentId);

        var response = await Client.GetAsync(
            $"{BaseUrl}/{Factory.SeededDivisionId}?skipCount=0&maxResultCount=20&searchTerm={Uri.EscapeDataString(included.FirstName)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);

        body.ShouldNotBeNull();
        body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        body.Items.ShouldContain(student => student.StudentId == included.StudentId);
        body.Items.ShouldNotContain(student => student.StudentId == excluded.StudentId);
        body.Items.ShouldAllBe(student => student.DivisionId == Factory.SeededDivisionId);
        body.Items.ShouldAllBe(student => student.HasPhoto == false);
        body.Items.ShouldAllBe(student => student.PhotoUrl == null);
        body.Items.ShouldAllBe(student => student.RequiredDocumentsCount >= 0);
        body.Items.ShouldAllBe(student => student.UploadedDocumentsCount >= 0);
        var includedStudent = body.Items.Single(student => student.StudentId == included.StudentId);
        includedStudent.FormNo.ShouldBe(included.FormNo);
        includedStudent.DateOfBirth.ShouldNotBeNull();
        includedStudent.Age.ShouldNotBeNullOrWhiteSpace();
        includedStudent.Gender.ShouldNotBeNull();
        includedStudent.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
        includedStudent.StudentEnrollmentState.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
        includedStudent.OperationalStatus.ShouldBe(ShikshaMitra.Students.StudentRecords.StudentStatus.Active);
        includedStudent.PrimaryContactNo.ShouldNotBeNullOrWhiteSpace();
        includedStudent.City.ShouldBe("Pune");
        includedStudent.State.ShouldNotBeNull();
        includedStudent.Country.ShouldNotBeNull();
        includedStudent.Address.ShouldContain("Pune");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_List_Inactive_Students_With_Source_Of_Truth_Status()
    {
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsInactive");
        await MoveRegistrationToEnrolledAsync(student.StudentId);

        var statusResponse = await Client.PutAsJsonAsync(
            $"/api/students/records/list/{student.StudentId}/status",
            new ChangeStudentStatusDto
            {
                Status = ShikshaMitra.Students.StudentRecords.StudentStatus.Inactive,
                Remarks = "Temporary inactive for regression test."
            },
            JsonOptions);
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await statusResponse.Content.ReadAsStringAsync());

        var response = await Client.GetAsync(
            $"{BaseUrl}/{Factory.SeededDivisionId}?skipCount=0&maxResultCount=20&searchTerm={Uri.EscapeDataString(student.FirstName)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        body.ShouldNotBeNull();
        var row = body.Items.Single(item => item.StudentId == student.StudentId);
        row.Status.ShouldBe(StudentLifeCycleStatusEnum.InActive);
        row.StudentEnrollmentState.ShouldBe(StudentLifeCycleStatusEnum.InActive);
        row.OperationalStatus.ShouldBe(ShikshaMitra.Students.StudentRecords.StudentStatus.Inactive);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_List_And_Summarize_Student_With_Feb_29_Birthday()
    {
        var leapDayStudent = await CreateStudentFixtureWithBirthdayAsync(
            "LeapDay",
            new DateTimeOffset(2012, 2, 29, 0, 0, 0, TimeSpan.Zero));
        await MoveRegistrationToEnrolledAsync(leapDayStudent.StudentId);

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{Factory.SeededDivisionId}?skipCount=0&maxResultCount=20&searchTerm={Uri.EscapeDataString(leapDayStudent.FirstName)}");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);
        list.ShouldNotBeNull();
        list.Items.Single(student => student.StudentId == leapDayStudent.StudentId).Age.ShouldNotBeNullOrWhiteSpace();

        var dashboardResponse = await Client.GetAsync("/api/students/records/dashboard-summary");
        dashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await dashboardResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Student_Dashboard_Summaries_From_Tenant_Scope()
    {
        var enrolled = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsSummary");
        await MoveRegistrationToEnrolledAsync(enrolled.StudentId);

        var dashboardResponse = await Client.GetAsync("/api/students/records/dashboard-summary");
        dashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<StudentDashboardSummaryDto>(JsonOptions);

        dashboard.ShouldNotBeNull();
        dashboard.TotalStudents.ShouldBeGreaterThanOrEqualTo(1);
        dashboard.RecentActivities.ShouldContain(activity => activity.Title.Contains(enrolled.FirstName));

        var attendanceResponse = await Client.GetAsync("/api/students/records/attendance-dashboard-summary");
        attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var attendance = await attendanceResponse.Content
            .ReadFromJsonAsync<StudentAttendanceDashboardSummaryDto>(JsonOptions);

        attendance.ShouldNotBeNull();
        attendance.Classes.ShouldContain(item => item.StandardId == Factory.SeededStandardId);
        attendance.Classes.ShouldContain(item =>
            item.Divisions.Any(division => division.DivisionId == Factory.SeededDivisionId));
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Show_Upcoming_Birthday_For_Student_Outside_Recent_Activity_Window()
    {
        var nextBirthday = DateTimeOffset.UtcNow.Date.AddDays(1);
        var birthday = new DateTimeOffset(
            nextBirthday.Year - 10,
            nextBirthday.Month,
            nextBirthday.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        var olderStudent = await CreateStudentFixtureWithBirthdayAsync("BirthdayOlder", birthday);
        await MoveRegistrationToEnrolledAsync(olderStudent.StudentId);
        await SetStudentCreationTimeAsync(olderStudent.StudentId, DateTime.UtcNow.AddDays(-90));

        var nonMatchingBirthday = birthday.AddDays(-14);
        for (var index = 0; index < 31; index++)
        {
            var newerStudent = await CreateStudentFixtureWithBirthdayAsync(
                $"BirthdayWindow{index:00}",
                nonMatchingBirthday);
            await MoveRegistrationToEnrolledAsync(newerStudent.StudentId);
        }

        var response = await Client.GetAsync("/api/students/records/dashboard-summary");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<StudentDashboardSummaryDto>(JsonOptions);
        dashboard.ShouldNotBeNull();
        dashboard.UpcomingBirthdays.ShouldContain(student => student.Name.Contains(olderStudent.FirstName));
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Calculate_Attendance_Dashboard_Percentage_From_Marked_Attendance()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var contextStudentA = await CreateStudentFixtureForDivisionAsync(
            context,
            "RecordsAttendanceA",
            Factory.SeededDivisionId);
        var contextStudentB = await CreateStudentFixtureForDivisionAsync(
            context,
            "RecordsAttendanceB",
            Factory.SeededDivisionId);
        await MoveRegistrationToEnrolledAsync(contextStudentA.StudentId);
        await MoveRegistrationToEnrolledAsync(contextStudentB.StudentId);
        var attendanceDate = DateTime.Today.AddDays(-3);

        var markResponse = await Client.PostAsJsonAsync(
            "/api/students/attendance/mark",
            new MarkAttendanceInput
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                StandardId = context.StandardId,
                DivisionId = Factory.SeededDivisionId,
                Date = attendanceDate,
                Entries =
                {
                    new AttendanceEntryDto
                    {
                        StudentId = contextStudentA.StudentId,
                        Status = AttendanceStatus.Present,
                        Remarks = "Dashboard percentage present"
                    },
                    new AttendanceEntryDto
                    {
                        StudentId = contextStudentB.StudentId,
                        Status = AttendanceStatus.Absent,
                        Remarks = "Dashboard percentage absent"
                    }
                }
            },
            JsonOptions);
        markResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await markResponse.Content.ReadAsStringAsync());

        var response = await Client.GetAsync(
            $"/api/students/records/attendance-dashboard-summary?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<StudentAttendanceDashboardSummaryDto>(JsonOptions);
        dashboard.ShouldNotBeNull();
        var expectedSchoolPercentage = await CalculateExpectedSchoolAttendancePercentageAsync(
            attendanceDate,
            attendanceDate);
        var expectedDivisionPercentage = await CalculateExpectedDivisionAttendancePercentageAsync(
            Factory.SeededDivisionId,
            attendanceDate,
            attendanceDate);

        expectedSchoolPercentage.ShouldBeGreaterThan(0);
        dashboard.SchoolAttendancePercentage.ShouldBe(expectedSchoolPercentage);
        var divisionSummary = dashboard.Classes
            .Single(item => item.StandardId == context.StandardId)
            .Divisions
            .Single(item => item.DivisionId == Factory.SeededDivisionId);
        divisionSummary.Percentage.ShouldBe(expectedDivisionPercentage);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Filter_Attendance_Dashboard_Summary_By_Date_Range()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var studentA = await CreateStudentFixtureForDivisionAsync(
            context,
            "RecordsRangeA",
            Factory.SeededDivisionId);
        var studentB = await CreateStudentFixtureForDivisionAsync(
            context,
            "RecordsRangeB",
            Factory.SeededDivisionId);
        await MoveRegistrationToEnrolledAsync(studentA.StudentId);
        await MoveRegistrationToEnrolledAsync(studentB.StudentId);
        var includedDate = DateTime.Today.AddDays(-37);
        var excludedDate = includedDate.AddDays(-1);

        var includedResponse = await Client.PostAsJsonAsync(
            "/api/students/attendance/mark",
            new MarkAttendanceInput
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                StandardId = context.StandardId,
                DivisionId = Factory.SeededDivisionId,
                Date = includedDate,
                Entries =
                {
                    new AttendanceEntryDto
                    {
                        StudentId = studentA.StudentId,
                        Status = AttendanceStatus.Present,
                        Remarks = "Range included present"
                    },
                    new AttendanceEntryDto
                    {
                        StudentId = studentB.StudentId,
                        Status = AttendanceStatus.Absent,
                        Remarks = "Range included absent"
                    }
                }
            },
            JsonOptions);
        includedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await includedResponse.Content.ReadAsStringAsync());

        var excludedResponse = await Client.PostAsJsonAsync(
            "/api/students/attendance/mark",
            new MarkAttendanceInput
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                StandardId = context.StandardId,
                DivisionId = Factory.SeededDivisionId,
                Date = excludedDate,
                Entries =
                {
                    new AttendanceEntryDto
                    {
                        StudentId = studentA.StudentId,
                        Status = AttendanceStatus.Absent,
                        Remarks = "Range excluded absent"
                    }
                }
            },
            JsonOptions);
        excludedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await excludedResponse.Content.ReadAsStringAsync());

        var response = await Client.GetAsync(
            $"/api/students/records/attendance-dashboard-summary?startDate={includedDate:yyyy-MM-dd}&endDate={includedDate:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<StudentAttendanceDashboardSummaryDto>(JsonOptions);
        dashboard.ShouldNotBeNull();
        dashboard.SchoolAttendancePercentage.ShouldBe(50);
        var divisionSummary = dashboard.Classes
            .Single(item => item.StandardId == context.StandardId)
            .Divisions
            .Single(item => item.DivisionId == Factory.SeededDivisionId);
        divisionSummary.Percentage.ShouldBe(50);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Keep_Moved_Student_Attendance_Division_In_Dashboard_Summary()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var originalDivisionId = await CreateDivisionAsync(context.StandardId, "DashOriginal");
        var currentDivisionId = await CreateDivisionAsync(context.StandardId, "DashCurrent");
        var student = await CreateStudentFixtureForDivisionAsync(
            context,
            "RecordsMovedDash",
            originalDivisionId);
        await MoveRegistrationToEnrolledAsync(student.StudentId, originalDivisionId);
        var attendanceDate = DateTime.Today.AddDays(-180);

        var markResponse = await Client.PostAsJsonAsync(
            "/api/students/attendance/mark",
            new MarkAttendanceInput
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                StandardId = context.StandardId,
                DivisionId = originalDivisionId,
                Date = attendanceDate,
                Entries =
                {
                    new AttendanceEntryDto
                    {
                        StudentId = student.StudentId,
                        Status = AttendanceStatus.Present,
                        Remarks = "Moved student original division"
                    }
                }
            },
            JsonOptions);
        markResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await markResponse.Content.ReadAsStringAsync());

        await MoveStudentToDivisionAsync(student.StudentId, currentDivisionId);

        var response = await Client.GetAsync(
            $"/api/students/records/attendance-dashboard-summary?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<StudentAttendanceDashboardSummaryDto>(JsonOptions);
        dashboard.ShouldNotBeNull();
        dashboard.SchoolAttendancePercentage.ShouldBe(100);
        var classSummary = dashboard.Classes.Single(item => item.StandardId == context.StandardId);
        classSummary.Percentage.ShouldBe(100);
        classSummary.Divisions.ShouldContain(division =>
            division.DivisionId == originalDivisionId && division.Percentage == 100);
        classSummary.Divisions.ShouldContain(division =>
            division.DivisionId == currentDivisionId && division.Percentage == 0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Upload_And_Report_Student_Photo()
    {
        var enrolled = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsPhoto");
        await MoveRegistrationToEnrolledAsync(enrolled.StudentId);

        using var content = BuildPhotoUpload("student-photo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var uploadResponse = await Client.PostAsync(
            $"/api/students/records/{enrolled.StudentId}/photo",
            content);

        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<StudentPhotoUploadResultDto>(JsonOptions);
        uploadResult.ShouldNotBeNull();
        uploadResult.StudentId.ShouldBe(enrolled.StudentId);
        uploadResult.HasPhoto.ShouldBeTrue();
        uploadResult.PhotoUrl.ShouldNotBeNullOrWhiteSpace();

        var listResponse = await Client.GetAsync(
            $"{BaseUrl}/{Factory.SeededDivisionId}?skipCount=0&maxResultCount=20&searchTerm={Uri.EscapeDataString(enrolled.FirstName)}");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<StudentDto>>(JsonOptions);

        var student = list!.Items.Single(item => item.StudentId == enrolled.StudentId);
        student.HasPhoto.ShouldBeTrue();
        student.PhotoUrl.ShouldBe(uploadResult.PhotoUrl);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Invalid_Student_Photo_File()
    {
        var enrolled = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordsBadPhoto");
        await MoveRegistrationToEnrolledAsync(enrolled.StudentId);

        using var content = BuildPhotoUpload("student-photo.txt", new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var response = await Client.PostAsync($"/api/students/records/{enrolled.StudentId}/photo", content);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Retrieve_Students_By_Academic_Scope()
    {
        var studentDetails = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "ScopeTest");
        await MoveRegistrationToEnrolledAsync(studentDetails.StudentId);

        Guid academicYearId;
        Guid boardId;
        Guid educationMediumId;
        Guid standardId;
        Guid divisionId;

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
			using var tenantScope = currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
			var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			var student = query
				.Where(s => s.Id == studentDetails.StudentId)
				.Select(s => new
				{
					s.AcademicYearId,
					s.AcademicBoardId,
					s.AcademicMediumId,
					s.StandardId,
					s.DivisionId
				})
				.Single();
			academicYearId = student.AcademicYearId!.Value;
			boardId = student.AcademicBoardId!.Value;
			educationMediumId = student.AcademicMediumId;
			standardId = student.StandardId!.Value;
			divisionId = student.DivisionId!.Value;
			await uow.CompleteAsync();
		}

        var url = $"/api/students/records/list/by-scope?academicYearId={academicYearId}&boardId={boardId}&educationMediumId={educationMediumId}&classIds={standardId}&divisionIds={divisionId}";
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var students = await response.Content.ReadFromJsonAsync<System.Collections.Generic.List<StudentLookupDto>>(JsonOptions);

        students.ShouldNotBeNull();
        students.Count.ShouldBeGreaterThanOrEqualTo(1);
        students.Any(s => s.StudentId == studentDetails.StudentId).ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Roll_Number_Save_Summary()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollSummaryA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollSummaryB");
        await MoveRegistrationToEnrolledAsync(first.StudentId);
        await MoveRegistrationToEnrolledAsync(second.StudentId);
        var term = await CreateAcademicTermAsync(
            "Roll Summary",
            Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 100000,
            isCurrent: false);

        var response = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={term.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 1 },
                new() { StudentId = second.StudentId, RollNo = 2 },
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Result<RollNumberSaveResultDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.TotalRequested.ShouldBe(2);
        body.Data.AppliedCount.ShouldBe(2);
        body.Data.ConflictCount.ShouldBe(0);
        body.Data.Results.Select(x => x.StudentId).ShouldBe(new[] { first.StudentId, second.StudentId }, ignoreOrder: true);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Roll_Number_Arrangement_Metadata_In_Response_Body()
    {
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollMetadata");
        await MoveRegistrationToEnrolledAsync(student.StudentId);

        var response = await Client.GetAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-number-projections?arrangementType={(int)ArrangementTypeEnum.BoysFirst}&arrangementBy={(int)ArrangementByEnum.LastName}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<Result<RollNumberProjectionResponseDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.ArrangementType.ShouldBe(ArrangementTypeEnum.BoysFirst);
        body.Data.ArrangementBy.ShouldBe(ArrangementByEnum.LastName);
        body.Data.Items.ShouldContain(item => item.StudentId == student.StudentId);
        body.Data.TotalCount.ShouldBe(body.Data.Items.Count);
        body.Data.CurrentPage.ShouldBe(1);
        body.Data.PageSize.ShouldBe(body.Data.Items.Count);
        body.Data.TotalPages.ShouldBe(body.Data.Items.Count == 0 ? 0 : 1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Record_List_Arrangement_Metadata_In_Response_Body()
    {
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RecordMetadata");
        await MoveRegistrationToEnrolledAsync(student.StudentId);

        var settingsResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?arrangementType={(int)ArrangementTypeEnum.GirlsFirst}&arrangementBy={(int)ArrangementByEnum.LastName}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = student.StudentId, RollNo = null }
            },
            JsonOptions);
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await settingsResponse.Content.ReadAsStringAsync());

        var response = await Client.GetAsync($"/api/students/records/{Factory.SeededDivisionId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<Result<StudentRecordListResponseDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.ArrangementType.ShouldBe(ArrangementTypeEnum.GirlsFirst);
        body.Data.ArrangementBy.ShouldBe(ArrangementByEnum.LastName);
        body.Data.Items.ShouldContain(item => item.StudentId == student.StudentId);
        body.Data.TotalCount.ShouldBe(body.Data.Items.Count);
        body.Data.CurrentPage.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Pending_Gr_And_Roll_Assignment_Students()
    {
        var pending = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "PendingAssign");
        await MoveRegistrationToEnrolledAsync(pending.StudentId);

        var response = await Client.GetAsync(
            $"/api/students/records/pending-assignments?standardId={Factory.SeededStandardId}&divisionId={Factory.SeededDivisionId}&maxResultCount=50");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<Result<PendingAssignmentResponseDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        body.Data.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        var row = body.Data.Items.Single(item => item.StudentId == pending.StudentId);
        row.GrNo.ShouldBeNull();
        row.RollNo.ShouldBeNull();
        row.StandardName.ShouldNotBeNullOrWhiteSpace();
        row.DivisionName.ShouldNotBeNullOrWhiteSpace();
        row.AdmissionDate.ShouldNotBe(default);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Division_Capacity_Distribution_For_Assign_Division_Lookup()
    {
        var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "DivisionCapacity");
        await MoveRegistrationToEnrolledAsync(student.StudentId);

        var response = await Client.GetAsync(
            $"/api/education-medium/{StudentEnrollmentTestHelper.DefaultAcademicMediumId}/standards");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var standards = await response.Content.ReadFromJsonAsync<List<StandardDto>>(JsonOptions);
        standards.ShouldNotBeNull();
        var division = standards
            .SelectMany(item => item.Divisions)
            .Single(item => item.Id == StudentEnrollmentTestHelper.DefaultDivisionId);
        division.CurrentStudentCount.ShouldBeGreaterThanOrEqualTo(1);
        division.HeadCount.ShouldBe(division.CurrentStudentCount);
        division.AvailableSeats.ShouldBe(Math.Max(division.Strength - division.CurrentStudentCount, 0));
        division.MaleCount.ShouldBeGreaterThanOrEqualTo(0);
        division.FemaleCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Duplicate_Roll_Number_Batch_Without_Partial_Save()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollDupA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollDupB");
        await MoveRegistrationToEnrolledAsync(first.StudentId);
        await MoveRegistrationToEnrolledAsync(second.StudentId);
        var term = await CreateAcademicTermAsync(
            "Roll Duplicate Guard",
            Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 200000,
            isCurrent: false);

        var response = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={term.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 1 },
                new() { StudentId = second.StudentId, RollNo = 1 },
            },
            JsonOptions);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var rollNumbers = await GetRollNumbersAsync(first.StudentId, second.StudentId);
        rollNumbers[first.StudentId].ShouldBeNull();
        rollNumbers[second.StudentId].ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Roll_Number_For_Non_Enrolled_Student()
    {
        var registration = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollNotEnrolled");
        var term = await CreateAcademicTermAsync(
            "Roll Enrollment Guard",
            Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 240000,
            isCurrent: false);

        var response = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={term.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = registration.StudentId, RollNo = 1 },
            },
            JsonOptions);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain(StudentErrorCodes.RollNumberBatchInvalid);
        content.ShouldContain("are not enrolled");

        var assignments = await GetRollAssignmentNumbersAsync(term.Id, registration.StudentId);
        assignments[registration.StudentId].ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Roll_Number_Above_Division_Student_Count_Without_Partial_Save()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollMaxA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollMaxB");
        await MoveRegistrationToEnrolledAsync(first.StudentId);
        await MoveRegistrationToEnrolledAsync(second.StudentId);
        var term = await CreateAcademicTermAsync(
            "Roll Max Guard",
            Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 250000,
            isCurrent: false);
        var divisionStudentCount = await GetDivisionStudentCountAsync(Factory.SeededDivisionId);

        var validResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={term.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = divisionStudentCount },
            },
            JsonOptions);
        validResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await validResponse.Content.ReadAsStringAsync());

        var rejectedResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={term.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = divisionStudentCount + 1 },
                new() { StudentId = second.StudentId, RollNo = 1 },
            },
            JsonOptions);

        rejectedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var rejectedContent = await rejectedResponse.Content.ReadAsStringAsync();
        rejectedContent.ShouldContain(StudentErrorCodes.RollNumberBatchInvalid);
        rejectedContent.ShouldContain($"Roll numbers cannot exceed total students ({divisionStudentCount}).");

        var assignments = await GetRollAssignmentNumbersAsync(term.Id, first.StudentId, second.StudentId);
        assignments[first.StudentId].ShouldBe(divisionStudentCount);
        assignments[second.StudentId].ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Assign_Different_Roll_Numbers_By_Academic_Term()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollSemA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollSemB");
        await MoveRegistrationToEnrolledAsync(first.StudentId);
        await MoveRegistrationToEnrolledAsync(second.StudentId);
        var sequenceBase = Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 300000;
        var currentTerm = await CreateAcademicTermAsync("Semester Current", sequenceBase, isCurrent: true);
        var nextTerm = await CreateAcademicTermAsync("Semester Next", sequenceBase + 1, isCurrent: false);

        var currentResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={currentTerm.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 1 },
                new() { StudentId = second.StudentId, RollNo = 2 },
            },
            JsonOptions);
        currentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var nextResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={nextTerm.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 2 },
                new() { StudentId = second.StudentId, RollNo = 1 },
            },
            JsonOptions);
        nextResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var currentProjection = await GetRollProjectionsAsync(currentTerm.Id);
        var nextProjection = await GetRollProjectionsAsync(nextTerm.Id);

        currentProjection.Single(item => item.StudentId == first.StudentId).RollNo.ShouldBe(1);
        nextProjection.Single(item => item.StudentId == first.StudentId).RollNo.ShouldBe(2);
        currentProjection.Single(item => item.StudentId == first.StudentId).AcademicTermId.ShouldBe(currentTerm.Id);
        nextProjection.Single(item => item.StudentId == first.StudentId).AcademicTermId.ShouldBe(nextTerm.Id);

        var rollNumbers = await GetRollNumbersAsync(first.StudentId, second.StudentId);
        rollNumbers[first.StudentId].ShouldBe(1);
        rollNumbers[second.StudentId].ShouldBe(2);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Duplicate_Roll_Number_Per_Term_Without_Touching_Other_Terms()
    {
        var first = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollTermDupA");
        var second = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "RollTermDupB");
        await MoveRegistrationToEnrolledAsync(first.StudentId);
        await MoveRegistrationToEnrolledAsync(second.StudentId);
        var sequenceBase = Math.Abs(Guid.NewGuid().GetHashCode() % 100000) + 400000;
        var firstTerm = await CreateAcademicTermAsync("Duplicate Guard A", sequenceBase, isCurrent: true);
        var secondTerm = await CreateAcademicTermAsync("Duplicate Guard B", sequenceBase + 1, isCurrent: false);

        var okResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={firstTerm.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 1 },
                new() { StudentId = second.StudentId, RollNo = 2 },
            },
            JsonOptions);
        okResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var duplicateResponse = await Client.PostAsJsonAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-numbers?academicTermId={secondTerm.Id}",
            new List<UpdateStudentRollNumberDto>
            {
                new() { StudentId = first.StudentId, RollNo = 2 },
                new() { StudentId = second.StudentId, RollNo = 2 },
            },
            JsonOptions);

        duplicateResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var firstTermProjection = await GetRollProjectionsAsync(firstTerm.Id);
        var secondTermProjection = await GetRollProjectionsAsync(secondTerm.Id);

        firstTermProjection.Single(item => item.StudentId == first.StudentId).RollNo.ShouldBe(1);
        secondTermProjection.Single(item => item.StudentId == first.StudentId).RollNo.ShouldNotBe(2);
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

    private async Task<StudentGeneralDetailsDto> CreateStudentFixtureWithBirthdayAsync(
        string suffix,
        DateTimeOffset dateOfBirth)
    {
        return await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(
            Client, suffix, dateOfBirth: dateOfBirth);
    }

    private async Task<StudentGeneralDetailsDto> CreateStudentFixtureForDivisionAsync(
        SeededDivisionContext context,
        string suffix,
        Guid divisionId)
    {
        return await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(
            Client, suffix, divisionId);
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

    private async Task SetStudentCreationTimeAsync(Guid studentId, DateTime creationTime)
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
            typeof(Student)
                .GetProperty(nameof(Student.CreationTime))!
                .SetValue(student, creationTime);

            await studentRepository.UpdateAsync(student);
            await uow.CompleteAsync();
        }
    }

    private async Task<Dictionary<Guid, int?>> GetRollNumbersAsync(params Guid[] studentIds)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var ids = studentIds.ToList();
			var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			var result = query
				.Where(student => ids.Contains(student.Id))
				.ToDictionary(student => student.Id, student => student.RollNo);
			await uow.CompleteAsync();
            return result;
        }
    }

    private async Task<Dictionary<Guid, int?>> GetGrNumbersAsync(params Guid[] studentIds)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var ids = studentIds.ToList();
			var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			var result = query
				.Where(student => ids.Contains(student.Id))
				.ToDictionary(student => student.Id, student => student.GrNo);
			await uow.CompleteAsync();
            return result;
        }
    }

    private async Task<AcademicTermDto> CreateAcademicTermAsync(string prefix, int sequence, bool isCurrent)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/students/records/academic-terms",
            new CreateAcademicTermDto
            {
                AcademicYearId = Factory.SeededAcademicYearId,
                Name = $"{prefix} {Guid.NewGuid():N}",
                Sequence = sequence,
                IsCurrent = isCurrent
            },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Result<AcademicTermDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        return body.Data;
    }

    private async Task<List<RollNumberProjectionDto>> GetRollProjectionsAsync(Guid academicTermId)
    {
        var response = await Client.GetAsync(
            $"/api/students/records/{Factory.SeededDivisionId}/roll-number-projections?academicTermId={academicTermId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Result<RollNumberProjectionResponseDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        return body.Data.Items;
    }

    private async Task<Dictionary<Guid, int?>> GetRollAssignmentNumbersAsync(Guid academicTermId, params Guid[] studentIds)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var assignmentRepository = scope.ServiceProvider
            .GetRequiredService<IRepository<StudentRollNumberAssignment, Guid>>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var ids = studentIds.ToList();
            var query = await assignmentRepository.GetQueryableAsync();
            var existing = query
                .Where(assignment =>
                    assignment.AcademicTermId == academicTermId &&
                    ids.Contains(assignment.StudentId))
                .ToDictionary(assignment => assignment.StudentId, assignment => assignment.RollNo);
            var result = ids.ToDictionary(id => id, id => existing.TryGetValue(id, out var rollNo) ? rollNo : null);
            await uow.CompleteAsync();
            return result;
        }
    }

    private async Task<int> GetDivisionStudentCountAsync(Guid divisionId)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
            var count = query.Count(student =>
                student.DivisionId == divisionId &&
                student.Status == StudentLifeCycleStatusEnum.Enrolled);
            await uow.CompleteAsync();
            return count;
        }
    }

    private async Task<decimal> CalculateExpectedSchoolAttendancePercentageAsync(
        DateTime startDate,
        DateTime endDate)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var attendanceEntryRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendanceEntry, Guid>>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var enrolledStudentIds = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
                .Where(student => student.Status == StudentLifeCycleStatusEnum.Enrolled)
                .Select(student => student.Id)
                .ToList();
            var statuses = (await attendanceEntryRepository.GetQueryableAsync())
                .Where(entry => enrolledStudentIds.Contains(entry.StudentId))
                .Where(entry => entry.AttendanceDay.Date >= startDate.Date && entry.AttendanceDay.Date <= endDate.Date)
                .Where(entry => entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Absent)
                .Select(entry => entry.Status)
                .ToList();
            await uow.CompleteAsync();

            return CalculateAttendancePercentage(statuses);
        }
    }

    private async Task<decimal> CalculateExpectedDivisionAttendancePercentageAsync(
        Guid divisionId,
        DateTime startDate,
        DateTime endDate)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var attendanceEntryRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendanceEntry, Guid>>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var enrolledStudentIds = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
                .Where(student => student.Status == StudentLifeCycleStatusEnum.Enrolled)
                .Select(student => student.Id)
                .ToList();
            var statuses = (await attendanceEntryRepository.GetQueryableAsync())
                .Where(entry => enrolledStudentIds.Contains(entry.StudentId))
                .Where(entry => entry.AttendanceDay.DivisionId == divisionId)
                .Where(entry => entry.AttendanceDay.Date >= startDate.Date && entry.AttendanceDay.Date <= endDate.Date)
                .Where(entry => entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Absent)
                .Select(entry => entry.Status)
                .ToList();
            await uow.CompleteAsync();

            return CalculateAttendancePercentage(statuses);
        }
    }

    private HttpClient CreatePermissionClient(params string[] permissions)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AlwaysGrantPermissionChecker.TestPermissionsHeader,
            permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
        return client;
    }

    private static decimal CalculateAttendancePercentage(IReadOnlyCollection<AttendanceStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return 0;
        }

        return Math.Round(statuses.Count(status => status == AttendanceStatus.Present) * 100m / statuses.Count, 2);
    }

    private sealed record SeededDivisionContext(Guid StandardId, Guid AcademicMediumId);

    private static string? ReadProblemDetail(string content)
    {
        using var document = JsonDocument.Parse(content);
        return document.RootElement.TryGetProperty("detail", out var detail)
            ? detail.GetString()
            : null;
    }

    private static MultipartFormDataContent BuildPhotoUpload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", fileName);
        return content;
    }
}
