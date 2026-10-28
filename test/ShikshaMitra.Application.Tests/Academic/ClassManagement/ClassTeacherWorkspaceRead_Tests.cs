using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public partial class ClassTeacherAssignmentAppService_Tests
{
    [Fact]
    public async Task Workspace_Should_Share_Narrow_Context_And_Preserve_All_Three_Responses()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
                var standard = await _standardRepository.InsertAsync(
                    new Standard(Guid.NewGuid(), TestTenantId, "Workspace class", 8, stageId, shiftId: null), autoSave: true);
                await _divisionRepository.InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "W-101"), autoSave: true);
                await CreateEmployeeAsync("Workspace Teacher", EmployeeStatusType.Active, EmployeeGroupType.Teaching);
                await CreateEmployeeAsync("Inactive Teacher", EmployeeStatusType.Inactive, EmployeeGroupType.Teaching);

                var expected = new ClassTeacherWorkspaceDto
                {
                    Items = await _service.GetClassTeacherAssignmentsAsync(stageId),
                    Summary = await _service.GetClassTeacherAssignmentSummaryAsync(stageId),
                    EligibleTeachers = await _service.GetEligibleTeachersAsync(stageId)
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
                var actual = await _service.GetWorkspaceAsync(stageId);
                JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected));
                db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
                db.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
                capture.CommandsFor("ClassTeachers").ShouldBeLessThanOrEqualTo(1);
            });
        }
    }
}
