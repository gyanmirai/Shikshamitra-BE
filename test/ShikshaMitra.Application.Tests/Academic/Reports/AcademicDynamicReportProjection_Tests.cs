using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Fact]
    public async Task Dynamic_Exam_Schedule_Should_Project_Cross_Product_Within_Limit_And_Keep_Tenant_And_Token_Filters()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var scope = await DynamicScopeAsync(assignment);
                var subject = new Subject(Guid.NewGuid(), tenantId, "Second exam subject", "#112233", scope.EducationMediumId);
                await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
                var divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
                var divisions = new List<Division>();
                foreach (var name in new[] { "A", "B" })
                {
                    var division = await GetRequiredService<DivisionManager>().CreateAsync(assignment.StandardId, name, 30);
                    await divisionRepository.InsertAsync(division, autoSave: true);
                    divisions.Add(division);
                }
                var type = new ExamType(Guid.NewGuid(), tenantId, "Dynamic schedule", null, scope.EducationMediumId);
                await GetRequiredService<IRepository<ExamType, Guid>>().InsertAsync(type, autoSave: true);
                var date = new DateTime(2026, 9, 16);
                foreach (var examTenant in new[] { tenantId, Guid.NewGuid() })
                {
                    var exam = new Exam(Guid.NewGuid(), type.Id, "Projected schedule", assignment.StandardId, examTenant);
                    exam.ChangeStatus(ExamStatus.Published);
                    foreach (var division in divisions) exam.AddDivision(Guid.NewGuid(), division.Id);
                    foreach (var subjectId in new[] { assignment.SubjectId, subject.Id })
                        exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subjectId, date,
                            date.AddHours(9), date.AddHours(10), 100, 35, "Schedule notes"));
                    await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
                }
                var snapshot = new AcademicDynamicReportCatalogSnapshot(
                    [new(assignment.StandardId, "Class", "class_1")],
                    divisions.Select((division, index) => new CatalogDivision($"division_{index + 1}", division.Id,
                        division.Name, assignment.StandardId, "class_1")).ToList(),
                    [new(assignment.SubjectId, "First exam subject", "subject_1"), new(subject.Id, subject.Name, "subject_2")], []);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var reader = GetRequiredService<ExaminationDynamicReportRowReader>();
                var plan = new AcademicDynamicReportSpecDto { Module = AcademicReportModules.Examination, Dataset = AcademicDynamicDatasets.ExaminationSchedule };
                (await reader.ReadAsync(plan, scope, snapshot, maximumRows: 2)).Count.ShouldBe(3);
                (await reader.ReadAsync(plan, scope, snapshot, maximumRows: 10)).Count.ShouldBe(4);
                plan.Filters.DivisionTokens.Add("division_2");
                plan.Filters.SubjectTokens.Add("subject_2");
                var row = (await reader.ReadAsync(plan, scope, snapshot, maximumRows: 10)).Single();
                row["division"].ShouldBe("B");
                row["subject"].ShouldBe("Second exam subject");
                row["examDate"].ShouldBe("2026-09-16");
                row["startTime"].ShouldBe("09:00");
                row["endTime"].ShouldBe("10:00");
                row["totalMarks"].ShouldBe("100");
                row["passingMarks"].ShouldBe("35");
                row["examStatus"].ShouldBe("Published");
                row["notes"].ShouldBe("Schedule notes");
                db.ChangeTracker.Entries<Exam>().ShouldBeEmpty();
                db.ChangeTracker.Entries<ExamSubject>().ShouldBeEmpty();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dynamic_Catalog_And_Children_Should_Exclude_Invisible_Subjects_And_Keep_Class_Token_Ties(bool otherTenant)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            var currentTenant = GetRequiredService<ICurrentTenant>();
            using (currentTenant.Change(tenantId))
            {
                var visible = await SeedAssignmentAsync(tenantId);
                var scope = await DynamicScopeAsync(visible);
                var standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
                var standard = await standardRepository.GetAsync(visible.StandardId);
                var tiedStandard = new Standard(Guid.Parse("01000000-0000-0000-0000-000000000009"), tenantId,
                    standard.Name.ToUpperInvariant(), 8, standard.EducationMediumStageId, null);
                await standardRepository.InsertAsync(tiedStandard, autoSave: true);
                scope = scope with { ClassIds = new List<Guid> { standard.Id, tiedStandard.Id } };
                var subjects = GetRequiredService<IRepository<Subject, Guid>>();
                var invisible = new Subject(Guid.NewGuid(), otherTenant ? Guid.NewGuid() : tenantId,
                    "Invisible subject", "#00A76F", scope.EducationMediumId);
                using (currentTenant.Change(invisible.TenantId))
                    await subjects.InsertAsync(invisible, autoSave: true);
                var hiddenAssignment = new AssignedSubject(Guid.NewGuid(), tenantId, visible.StandardId, invisible.Id, "HIDDEN");
                await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(hiddenAssignment, autoSave: true);
                var chapters = new[] { visible, hiddenAssignment }.Select(assignment => new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id,
                    1, assignment == visible ? "Visible chapter" : "Invisible chapter", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                    { AssignedSubjectId = assignment.Id, TenantId = tenantId }).ToList();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertManyAsync(chapters, autoSave: true);
                if (!otherTenant) await subjects.DeleteAsync(invisible, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var snapshot = await GetRequiredService<AcademicDynamicReportCatalog>().BuildSnapshotAsync(scope);
                snapshot.Classes.Select(item => item.Id).ShouldBe(new[] { standard.Id, tiedStandard.Id }.OrderBy(id => id));
                snapshot.Classes.Select(item => item.Token).ShouldBe(["class_1", "class_2"]);
                snapshot.Subjects.Select(item => item.Id).ShouldBe([visible.SubjectId]);
                snapshot.Chapters.Select(item => item.Name).ShouldBe(["Visible chapter"]);
                var report = await GetRequiredService<AcademicDynamicReportQueryService>().GenerateAsync(new AcademicDynamicReportSpecDto
                {
                    Dataset = AcademicDynamicDatasets.CurriculumChapters, Columns = ["chapterName"]
                }, scope);
                report.Rows.Select(row => row["chapterName"]).ShouldBe(["Visible chapter"]);
                db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Dynamic_Curriculum_Should_Keep_Aggregates_Names_Chapter_Tokens_Where_And_Sort()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var primary = await SeedTeacherAsync(tenantId, " Ada ", " Lovelace ");
                var secondary = await SeedTeacherAsync(tenantId, "Grace", "Hopper", "BBCDE1234F", "223456789012");
                assignment.Teachers.Add(new AssignedSubjectTeacher(assignment.Id, assignment.SubjectId, primary.Id, SubjectTeacherType.Primary, tenantId));
                assignment.Teachers.Add(new AssignedSubjectTeacher(assignment.Id, assignment.SubjectId, secondary.Id, SubjectTeacherType.Secondary, tenantId));
                await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(assignment, autoSave: true);
                var chapters = new[] { 0, 50, 51 }.Select((progress, index) => new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id,
                    index + 1, $"Chapter {index + 1}", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero), $"Description {index + 1}")
                    { AssignedSubjectId = assignment.Id, TenantId = tenantId, ProgressPercentage = progress, EstimatedPeriods = index == 0 ? null : index }).ToList();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertManyAsync(chapters, autoSave: true);
                var scope = await DynamicScopeAsync(assignment);
                var catalog = GetRequiredService<AcademicDynamicReportCatalog>();
                var service = GetRequiredService<AcademicDynamicReportQueryService>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var snapshot = await catalog.BuildSnapshotAsync(scope);
                snapshot.Chapters.Select(chapter => chapter.Name).ShouldBe(["Chapter 1", "Chapter 2", "Chapter 3"]);
                var result = await service.GenerateAsync(new AcademicDynamicReportSpecDto
                {
                    Dataset = AcademicDynamicDatasets.CurriculumSubjects,
                    Columns = ["class", "subject", "chapterCount", "averageProgress", "primaryTeacher", "secondaryTeachers"]
                }, scope);
                var row = result.Rows.Single();
                row["chapterCount"].ShouldBe("3");
                row["averageProgress"].ShouldBe("33.67%");
                row["primaryTeacher"].ShouldBe(" Ada   Lovelace ");
                row["secondaryTeachers"].ShouldBe("Grace Hopper");

                var teacherResult = await service.GenerateAsync(new AcademicDynamicReportSpecDto
                {
                    Dataset = AcademicDynamicDatasets.CurriculumTeachers,
                    Columns = ["teacherName", "teacherCode", "teacherType"],
                    Sort = [new() { Field = "teacherName" }]
                }, scope);
                teacherResult.Rows.Select(item => item["teacherName"]).ShouldBe([" Ada   Lovelace ", "Grace Hopper"]);
                teacherResult.Rows[0]["teacherCode"].ShouldBe(primary.EmployeeCode);
                teacherResult.Rows[1]["teacherType"].ShouldBe("Secondary");

                var chapterResult = await service.GenerateAsync(new AcademicDynamicReportSpecDto
                {
                    Dataset = AcademicDynamicDatasets.CurriculumChapters,
                    Columns = ["chapterName", "progressPercentage", "plannedStart", "estimatedPeriods", "description"],
                    Filters = new() { ChapterTokens = [snapshot.Chapters[0].Token, snapshot.Chapters[2].Token] },
                    Where = new() { Conditions = [new() { Field = "progressPercentage", Operator = AcademicDynamicFilterOperators.GreaterThan, Values = ["0"] }] },
                    Sort = [new() { Field = "chapterName", Direction = AcademicDynamicSortDirections.Descending }]
                }, scope);
                chapterResult.TotalCount.ShouldBe(1);
                chapterResult.Rows.Single()["chapterName"].ShouldBe("Chapter 3");
                chapterResult.Rows.Single()["progressPercentage"].ShouldBe("51%");
                chapterResult.Rows.Single()["plannedStart"].ShouldBe(new DateTime(2026, 9, 1).ToString("dd MMM yyyy"));
                chapterResult.Rows.Single()["description"].ShouldBe("Description 3");
                db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
                db.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Dynamic_Chapter_Limit_Should_Apply_Before_Where_And_Catalog_Should_Retain_Ordinal_Top_1000()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var assignments = new List<AssignedSubject> { assignment };
                var originalSubject = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(assignment.SubjectId);
                for (var index = 1; index < 6; index++)
                {
                    var subject = new Subject(Guid.NewGuid(), tenantId, $"Limit subject {index}", "#00A76F", originalSubject.EducationMediumId);
                    await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
                    var additional = new AssignedSubject(Guid.NewGuid(), tenantId, assignment.StandardId, subject.Id, $"LIM{index}");
                    await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(additional, autoSave: true);
                    assignments.Add(additional);
                }
                var chapters = Enumerable.Range(1, 5000).Select(index => new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id,
                    (index - 1) % 1000 + 1, $"{(index % 2 == 0 ? "A" : "a")} chapter {5001 - index:0000}",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                    { AssignedSubjectId = assignments[(index - 1) / 1000].Id, TenantId = tenantId }).ToList();
                var chapterRepository = GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
                await chapterRepository.InsertManyAsync(chapters, autoSave: true);
                var scope = await DynamicScopeAsync(assignment);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var snapshot = await GetRequiredService<AcademicDynamicReportCatalog>().BuildSnapshotAsync(scope);
                snapshot.Chapters.Select(chapter => chapter.Id).ShouldBe(chapters.OrderBy(chapter => chapter.ChapterName,
                    StringComparer.OrdinalIgnoreCase).Take(1000).Select(chapter => chapter.Id));
                snapshot.Chapters.Select(chapter => chapter.Token).ShouldBe(Enumerable.Range(1, 1000).Select(index => $"chapter_{index}"));
                var service = GetRequiredService<AcademicDynamicReportQueryService>();
                var request = new AcademicDynamicReportSpecDto
                {
                    Dataset = AcademicDynamicDatasets.CurriculumChapters, Columns = ["chapterName"]
                };
                (await service.GenerateAsync(request, scope)).TotalCount.ShouldBe(5000);
                await chapterRepository.InsertAsync(new AssignedSubjectChapter(Guid.NewGuid(), assignments[5].Id, 1, "Z extra chapter",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)) { AssignedSubjectId = assignments[5].Id, TenantId = tenantId }, autoSave: true);
                db.ChangeTracker.Clear();
                request.Where.Conditions.Add(new AcademicDynamicReportConditionDto
                {
                    Field = "chapterName", Operator = AcademicDynamicFilterOperators.Equal, Values = ["No matching chapter"]
                });
                var error = await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(request, scope));
                error.Code.ShouldBe("Academic:Reports:AiResultTooLarge");
                error.Data["MaximumRows"].ShouldBe(5000);
                db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
                db.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Dynamic_Timetable_Should_Limit_Expanded_Rows_And_Preserve_Empty_Slots_And_Subject_Filter()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var division = await GetRequiredService<DivisionManager>().CreateAsync(assignment.StandardId, "A", 30);
                await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
                var userId = Guid.NewGuid();
                await GetRequiredService<IIdentityUserRepository>().InsertAsync(
                    new IdentityUser(userId, $"dynamic-report-{userId:N}", $"{userId:N}@example.test", tenantId), autoSave: true);
                using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test")));
                await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
                    new UserScopeAssignment(Guid.NewGuid(), tenantId, userId, description: "Dynamic timetable test tenant scope"), autoSave: true);
                await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
                var timetable = new TimetableEntity(Guid.NewGuid(), tenantId, assignment.StandardId, division.Id);
                var empty = new TimetableSlot(Guid.Parse("01000000-0000-0000-0000-000000000001"), timetable.Id,
                    TimetableDayOfWeek.Monday, 1, new TimeOnly(8, 0), new TimeOnly(8, 15), SlotType.Break);
                timetable.AddSlot(empty);
                var scheduled = new TimetableSlot(Guid.Parse("02000000-0000-0000-0000-000000000001"), timetable.Id,
                    TimetableDayOfWeek.Monday, 2, new TimeOnly(8, 15), new TimeOnly(9, 0), SlotType.Period);
                for (var index = 0; index < 8; index++) scheduled.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), scheduled.Id, assignment.SubjectId));
                timetable.AddSlot(scheduled);
                await GetRequiredService<IRepository<TimetableEntity, Guid>>().InsertAsync(timetable, autoSave: true);
                var scope = await DynamicScopeAsync(assignment);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var snapshot = await GetRequiredService<AcademicDynamicReportCatalog>().BuildSnapshotAsync(scope);
                var reader = GetRequiredService<TimetableDynamicReportRowReader>();
                var plan = new AcademicDynamicReportSpecDto { Module = AcademicReportModules.Timetable, Dataset = AcademicDynamicDatasets.TimetableSlots };
                var limited = await reader.ReadAsync(plan, scope, snapshot, maximumRows: 2);
                limited.Count.ShouldBe(3);
                limited[0]["slotType"].ShouldBe("Break");
                limited[0]["subject"].ShouldBeNull();
                limited[0]["durationMinutes"].ShouldBe("15");
                plan.Filters.SubjectTokens.Add(snapshot.Subjects.Single().Token);
                var filtered = await reader.ReadAsync(plan, scope, snapshot, maximumRows: 2);
                filtered.Count.ShouldBe(3);
                filtered.ShouldAllBe(row => row["slotType"] == "Period" && row["durationMinutes"] == "45");
                db.ChangeTracker.Entries<TimetableEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<TimetableSlot>().ShouldBeEmpty();
                db.ChangeTracker.Entries<TimetableSlotSubject>().ShouldBeEmpty();
            }
        });
    }

    private async Task<AcademicAiReportCatalogScope> DynamicScopeAsync(AssignedSubject assignment)
    {
        var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(assignment.StandardId);
        var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(standard.EducationMediumStageId);
        var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(stage.EducationMediumId);
        var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
        return new AcademicAiReportCatalogScope(board.AcademicYearId, board.Id, medium.Id, [standard.Id]);
    }
}
