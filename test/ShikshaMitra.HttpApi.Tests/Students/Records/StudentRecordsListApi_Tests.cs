using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Application.Dtos;
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

    private static MultipartFormDataContent BuildPhotoUpload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", fileName);
        return content;
    }
}
