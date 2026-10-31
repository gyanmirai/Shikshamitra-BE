using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using ShikshaMitra.Students.Enrollments.Registrations;
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
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_List_Enrolled_Students_By_Division_With_Search()
    {
        var included = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsListIncluded");
        var excluded = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsListExcluded");

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
        includedStudent.Address.ShouldNotBeNull().ShouldContain("Pune");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_List_Inactive_Students_With_Source_Of_Truth_Status()
    {
        var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsInactive");
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
        var leapDayStudent = await CreateDirectRegistrationWithBirthdayAsync(
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
        var enrolled = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsSummary");
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
        var olderStudent = await CreateDirectRegistrationWithBirthdayAsync("BirthdayOlder", birthday);
        await MoveRegistrationToEnrolledAsync(olderStudent.StudentId);
        await SetStudentCreationTimeAsync(olderStudent.StudentId, DateTime.UtcNow.AddDays(-90));

        var nonMatchingBirthday = birthday.AddDays(-14);
        for (var index = 0; index < 31; index++)
        {
            var newerStudent = await CreateDirectRegistrationWithBirthdayAsync(
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
        var contextStudentA = await CreateDirectRegistrationForDivisionAsync(
            context,
            "RecordsAttendanceA",
            Factory.SeededDivisionId);
        var contextStudentB = await CreateDirectRegistrationForDivisionAsync(
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
        var studentA = await CreateDirectRegistrationForDivisionAsync(
            context,
            "RecordsRangeA",
            Factory.SeededDivisionId);
        var studentB = await CreateDirectRegistrationForDivisionAsync(
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
        var student = await CreateDirectRegistrationForDivisionAsync(
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
        var enrolled = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsPhoto");
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
        var enrolled = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "RecordsBadPhoto");
        await MoveRegistrationToEnrolledAsync(enrolled.StudentId);

        using var content = BuildPhotoUpload("student-photo.txt", new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var response = await Client.PostAsync($"/api/students/records/{enrolled.StudentId}/photo", content);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Retrieve_Students_By_Academic_Scope()
    {
        var studentDetails = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "ScopeTest");
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
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Division_Capacity_Distribution_For_Assign_Division_Lookup()
    {
        var student = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "DivisionCapacity");
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

    private async Task<StudentGeneralDetailsDto> CreateDirectRegistrationWithBirthdayAsync(
        string suffix,
        DateTimeOffset dateOfBirth)
    {
        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto(suffix);
        dto.DateOfBirth = dateOfBirth;

        var response = await Client.PostAsJsonAsync(
            "/api/student/enrollment/registrations/general-details",
            dto,
            JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        return body.Data;
    }

    private async Task<StudentGeneralDetailsDto> CreateDirectRegistrationForDivisionAsync(
        SeededDivisionContext context,
        string suffix,
        Guid divisionId)
    {
        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto(suffix);
        dto.AcademicMediumId = context.AcademicMediumId;
        dto.StandardId = context.StandardId;
        dto.DivisionId = divisionId;

        var response = await Client.PostAsJsonAsync(
            "/api/student/enrollment/registrations/general-details",
            dto,
            JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Data.ShouldNotBeNull();
        return body.Data;
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

    private static MultipartFormDataContent BuildPhotoUpload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", fileName);
        return content;
    }
}
