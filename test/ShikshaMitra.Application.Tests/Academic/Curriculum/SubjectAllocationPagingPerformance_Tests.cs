using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAllocationPagingPerformance_Tests : SubjectTeacherReassignmentTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Subject_Allocation_Should_Page_Groups_Before_Reading_Assignment_And_Timetable_Graphs(bool teacherCriteria)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                await SeedAllocationAsync();
                var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
                var assignments = new List<AssignedSubject> { await repository.GetAsync(AssignmentId) };
                for (var index = 0; index < 24; index++)
                {
                    var assigned = await AddPagingAssignmentAsync($"Core {index}", null);
                    assigned.Status = index % 4 == 0 ? SubjectStatus.Archived : SubjectStatus.Published;
                    assigned.SetPrimaryTeacher(index % 2 == 0 ? OldTeacher : NewTeacher);
                    assignments.Add(assigned);
                }
                var groupIds = new List<Guid>();
                for (var groupIndex = 0; groupIndex < 2; groupIndex++)
                {
                    var group = new ElectiveGroup(Guid.NewGuid(), TenantId, $"Languages {groupIndex}", "#112233", AcademicMediumId);
                    await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
                    groupIds.Add(group.Id);
                    for (var subjectIndex = 0; subjectIndex < 2; subjectIndex++)
                    {
                        var assigned = await AddPagingAssignmentAsync($"Language {groupIndex}/{subjectIndex}", group.Id);
                        assigned.Status = SubjectStatus.Published;
                        assigned.SetPrimaryTeacher(groupIndex == 0 ? OldTeacher : NewTeacher);
                        assignments.Add(assigned);
                    }
                }
                foreach (var assignment in assignments)
                    for (var index = 0; index < 40; index++)
                        assignment.Chapters.Add(new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id, index + 10,
                            $"Large chapter {index}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                            { TenantId = TenantId, AssignedSubjectId = assignment.Id, Description = new string('d', 1000) });
                await repository.UpdateManyAsync(assignments, autoSave: true);
                var optional = await AddPagingAssignmentAsync("Ignored optional", null);
                optional.SubjectType = SubjectType.Optional;
                await repository.UpdateAsync(optional, autoSave: true);
                var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
                var otherStandard = await GetRequiredService<StandardManager>().CreateAsync(standard.EducationMediumStageId, $"Other class {Guid.NewGuid():N}"[..40], 14, null);
                await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(otherStandard, autoSave: true);
                await repository.InsertAsync(new AssignedSubject(Guid.NewGuid(), TenantId, otherStandard.Id, SubjectId, "OTHER"), autoSave: true);
                var otherDivision = await GetRequiredService<DivisionManager>().CreateAsync(otherStandard.Id, "A", 35);
                var secondDivision = await GetRequiredService<DivisionManager>().CreateAsync(StandardId, "C", 35);
                await GetRequiredService<IRepository<Division, Guid>>().InsertManyAsync(new[] { otherDivision, secondDivision }, autoSave: true);
                await AddTimetableAsync();
                await AddTimetableAsync(divisionId: secondDivision.Id);
                await AddTimetableAsync(divisionId: otherDivision.Id, standardId: otherStandard.Id);
                var weekly = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, StandardId);
                weekly.AddSubject(Guid.NewGuid(), SubjectId, null, 7);
                foreach (var groupId in groupIds) weekly.AddSubject(Guid.NewGuid(), null, groupId, 4);
                await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>().InsertAsync(weekly, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                SubjectStatus? status = teacherCriteria ? SubjectStatus.Published : null;
                var search = teacherCriteria ? "  ALLOC001  " : null;
                db.ChangeTracker.Clear();
                var all = await Allocations.GetListAsync(StandardId, 0, 1000, status, includeArchived: true, teacherSearch: search, divisionId: DivisionId);
                all.TotalCount.ShouldBe(teacherCriteria ? 7 : 27);
                var expectedAssignments = assignments.OrderBy(assignment => assignment.Id)
                    .Where(assignment => !status.HasValue || assignment.Status == status)
                    .ToList();
                var expectedGroups = expectedAssignments.Where(assignment => assignment.SubjectType == SubjectType.Core)
                    .Select(assignment => new[] { assignment })
                    .Concat(expectedAssignments.Where(assignment => assignment.SubjectType == SubjectType.Elective)
                        .GroupBy(assignment => assignment.ElectiveGroupId).Select(group => group.ToArray()))
                    .Where(group => !teacherCriteria || group.Any(assignment => assignment.Teachers.Any(teacher => teacher.TeacherId == OldTeacher)))
                    .ToList();
                all.Items.Select(item => item.Id).ShouldBe(expectedGroups.Select(group => group[0].Id));
                all.Items.Select(item => item.Subjects.Count).ShouldBe(expectedGroups.Select(group => group.Length));
                if (!teacherCriteria)
                {
                    var core = all.Items.Single(item => item.Id == AssignmentId);
                    core.ChaptersCount.ShouldBe(41);
                    core.WeeklyPeriods.ShouldBe(7);
                    core.TotalSlots.ShouldBe(7);
                    core.OccupiedSlots.ShouldBe(2);
                    core.SetupHealthScore.ShouldBe(70);
                }
                foreach (var pageIndex in new[] { 0, 1, 12, 13, 999 })
                {
                    db.ChangeTracker.Clear();
                    using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                    var page = await Allocations.GetListAsync(StandardId, pageIndex, 2, status, includeArchived: true, teacherSearch: search, divisionId: DivisionId);
                    page.TotalCount.ShouldBe(all.TotalCount);
                    page.TotalPages.ShouldBe((int)Math.Ceiling(all.TotalCount / 2.0));
                    JsonSerializer.Serialize(page.Items).ShouldBe(JsonSerializer.Serialize(all.Items.Skip(pageIndex * 2).Take(2)));
                    var pageSubjectCount = page.Items.Sum(item => item.Subjects.Count);
                    db.ChangeTracker.Entries<AssignedSubject>().Count().ShouldBeLessThanOrEqualTo(pageSubjectCount);
                    db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableSlot>().ShouldBeEmpty();
                    capture.Reads.ShouldNotBeEmpty();
                    capture.Commands.ShouldAllBe(sql => !sql.Contains("\"ChapterName\"", StringComparison.Ordinal) && !sql.Contains("\"StartTime\"", StringComparison.Ordinal));
                }
            }
        });
    }

    [Fact]
    public async Task Subject_Allocation_Teacher_Search_Should_Keep_Legacy_Primary_And_Employee_Secondary_Name_Semantics()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                await SeedAllocationAsync();
                var users = GetRequiredService<IIdentityUserRepository>();
                await users.InsertAsync(new IdentityUser(OldTeacher, $"legacy-{OldTeacher:N}", $"{OldTeacher:N}@test.local", TenantId)
                    { Name = "Ada", Surname = "Lovelace" }, autoSave: true);
                await users.InsertAsync(new IdentityUser(OtherTeacher, $"legacy-{OtherTeacher:N}", $"{OtherTeacher:N}@test.local", TenantId)
                    { Name = "Legacy", Surname = "Secondary" }, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                foreach (var search in new[] { "  aDA  ", "  alloc003  " })
                {
                    db.ChangeTracker.Clear();
                    var result = await Allocations.GetListAsync(StandardId, 0, 1, includeArchived: true, teacherSearch: search);
                    result.TotalCount.ShouldBe(1);
                    result.Items.Single().Id.ShouldBe(AssignmentId);
                    result.Items.Single().PrimaryTeacherNames.ShouldBe(["Ada Lovelace"]);
                    result.Items.Single().TeacherNames.ShouldContain("ALLOC003 - Other Teacher");
                }
                db.ChangeTracker.Clear();
                var noMatch = await Allocations.GetListAsync(StandardId, 0, 1, includeArchived: true, teacherSearch: "Legacy Secondary");
                noMatch.TotalCount.ShouldBe(0);
                noMatch.Items.ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
            }
        });
    }

    private async Task<AssignedSubject> AddPagingAssignmentAsync(string name, Guid? groupId)
    {
        var subject = new Subject(Guid.NewGuid(), TenantId, name, "#112233", AcademicMediumId);
        await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
        return await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(new AssignedSubject(Guid.NewGuid(), TenantId,
            StandardId, subject.Id, name, subjectType: groupId.HasValue ? SubjectType.Elective : SubjectType.Core, electiveGroupId: groupId), autoSave: true);
    }
}
