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
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentAttendanceApi_Tests : ShikshaMitraApiTestBase
{
    private const string AttendanceBaseUrl = "/api/students/attendance";
    private const string RegistrationBaseUrl = "/api/student/enrollment/registrations";

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

    private async Task<StudentGeneralDetailsDto> CreateEnrolledStudentAsync(
        SeededDivisionContext context,
        string suffix)
    {
        var dto = StudentEnrollmentTestHelper.BuildDirectRegistrationDto(suffix);
        dto.AcademicMediumId = context.AcademicMediumId;
        dto.StandardId = context.StandardId;
        dto.DivisionId = Factory.SeededDivisionId;

        var response = await Client.PostAsJsonAsync(
            $"{RegistrationBaseUrl}/general-details", dto, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions)
                     ?? throw new InvalidOperationException("Failed to create direct registration");
        var student = result.Data ?? throw new InvalidOperationException("Null direct registration data");

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
