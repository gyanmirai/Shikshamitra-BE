using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
    [Fact, TestTier(TestTraits.Tiers.Critical)]
    public async Task Register_filters_board_and_medium_before_paging_and_rejects_mismatched_scope()
    {
        var context = await ResolveSeededDivisionContextAsync();
        var student = await CreateEnrolledStudentAsync(context, "ReportScope");
        var range = $"academicYearId={context.AcademicYearId}&startDate={context.ReferenceDate:yyyy-MM-dd}&endDate={context.ReferenceDate:yyyy-MM-dd}&maxResultCount=1000";
        async Task<AttendanceRegisterDto> Read(string scope)
        {
            var response = await Client.GetAsync($"{AttendanceBaseUrl}/register?{range}&{scope}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!;
        }
        var valid = await Read($"boardId={context.AcademicBoardId}&educationMediumId={context.AcademicMediumId}");
        valid.Rows.ShouldContain(row => row.StudentId == student.StudentId);
        foreach (var mismatch in new[] {
            $"boardId={Guid.NewGuid()}",
            $"educationMediumId={Guid.NewGuid()}",
            $"boardId={Guid.NewGuid()}&educationMediumId={context.AcademicMediumId}",
            $"boardId={Guid.NewGuid()}&standardId={context.StandardId}&divisionId={context.DivisionId}" })
        {
            var result = await Read(mismatch);
            result.TotalCount.ShouldBe(0);
            result.Rows.ShouldBeEmpty();
        }
    }
}
