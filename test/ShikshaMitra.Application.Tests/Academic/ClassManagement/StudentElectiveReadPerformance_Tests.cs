using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveReadPerformance_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Elective_Reads_Should_Project_Rosters_And_Return_Shared_Options_Or_Grouped_Counts(bool countsOnly)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var stage = await db.Set<EducationMediumStage>().SingleAsync(row => row.Id == stageId);
                var medium = await db.Set<EducationMedium>().SingleAsync(row => row.Id == stage.EducationMediumId);
                var board = await db.Set<EducationBoard>().SingleAsync(row => row.Id == medium.EducationBoardId);
                var standard = new Standard(Guid.NewGuid(), tenantId, "Elective class", 8, stageId, null);
                var divisions = new[] { new Division(Guid.NewGuid(), tenantId, "A", standard.Id, 30, "E-A"), new Division(Guid.NewGuid(), tenantId, "B", standard.Id, 30, "E-B") };
                var subjects = new[] { new Subject(Guid.NewGuid(), tenantId, "Music", "#00A76F", medium.Id), new Subject(Guid.NewGuid(), tenantId, "Art", "#00A76F", medium.Id) };
                var groups = Enumerable.Range(1, 2).Select(index => new ElectiveGroup(Guid.NewGuid(), tenantId, $"Group {index}", "#00A76F", medium.Id)).ToArray();
                foreach (var group in groups) foreach (var subject in subjects) group.AddSubject(subject.Id);
                var students = Enumerable.Range(1, 4).Select(index => new Student(Guid.NewGuid(), tenantId,
                    "Student", null, index.ToString(), board.AcademicYearId, board.Id, medium.Id, stageId,
                    standard.Id, divisions[index == 4 ? 1 : 0].Id, rollNo: index)).ToArray();
                db.Add(standard); db.AddRange(divisions); db.AddRange(subjects); db.AddRange(groups); db.AddRange(students);
                await db.SaveChangesAsync();
                db.AddRange(new[] {
                    new StudentElectiveSelection(Guid.NewGuid(), tenantId, students[0].Id, groups[0].Id, subjects[0].Id, medium.Id),
                    new StudentElectiveSelection(Guid.NewGuid(), tenantId, students[1].Id, groups[0].Id, subjects[1].Id, medium.Id),
                    new StudentElectiveSelection(Guid.NewGuid(), tenantId, students[3].Id, groups[0].Id, subjects[1].Id, medium.Id),
                    new StudentElectiveSelection(Guid.NewGuid(), tenantId, students[1].Id, groups[1].Id, subjects[0].Id, medium.Id),
                    new StudentElectiveSelection(Guid.NewGuid(), tenantId, students[2].Id, groups[1].Id, subjects[0].Id, medium.Id)
                });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                var service = GetRequiredService<IStudentElectiveAssignmentAppService>();
                using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
                if (countsOnly)
                {
                    var counts = await service.GetSelectionCountsAsync(new ElectiveSelectionCountsRequestDto
                    {
                        AcademicMediumId = medium.Id, ClassId = standard.Id,
                        DivisionIds = divisions.Select(row => row.Id).ToList(), ElectiveGroupIds = groups.Select(row => row.Id).ToList()
                    });
                    counts.Sum(row => row.Count).ShouldBe(5);
                    counts.Where(row => row.ElectiveGroupId == groups[0].Id && row.SubjectId == subjects[1].Id).Sum(row => row.Count).ShouldBe(2);
                    counts.Where(row => row.ElectiveGroupId == groups[1].Id).Sum(row => row.Count).ShouldBe(2);
                    capture.CommandsFor("StudentElectiveSelections").ShouldBeLessThanOrEqualTo(1);
                }
                else
                {
                    var rows = await service.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, divisions[0].Id, groups[0].Id, includeSelectableSubjects: false);
                    rows.TotalRecords.ShouldBe(3);
                    rows.Records.Select(row => row.RollNo).ShouldBe(new[] { "1", "2", "3" });
                    rows.Records[0].SelectedSubjectName.ShouldBe("Music");
                    rows.SubjectOptions.Count.ShouldBe(2);
                    rows.Records.ShouldAllBe(row => row.SelectableSubjects.Count == 0);
                }
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
            }
        });
    }
}
