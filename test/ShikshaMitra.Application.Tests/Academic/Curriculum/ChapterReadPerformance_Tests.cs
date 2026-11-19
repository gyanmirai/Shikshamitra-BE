using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class ChapterReadPerformance_Tests : LessonPlanProgressTestBase
{
    [Fact]
    public async Task Lookup_Should_Preserve_The_Direct_Teacher_Grant_And_Validate_Division()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsLessonPlanOwner())
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
                var assigned = await assignments.SingleAsync(row => row.StandardId == StandardId && row.SubjectId == SubjectId);
                assigned.SetPrimaryTeacher(TeacherEmployeeId);
                await assignments.UpdateAsync(assigned, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var service = GetRequiredService<IChapterAppService>();
                var rows = await service.GetLookupAsync(StandardId, SubjectId, DivisionId);
                rows.Select(row => row.Id).ShouldBe(new[] { ChapterId });
                var invalidDivision = await Should.ThrowAsync<BusinessException>(() => service.GetLookupAsync(StandardId, SubjectId, Guid.NewGuid()));
                invalidDivision.Code.ShouldBe("Academic:Chapter:006");
                db.ChangeTracker.Entries<AssignedSubjectTeacher>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chapter_Reads_Should_Keep_Legacy_And_Json_Links_Without_Hydrating_Task_Content(bool lookup)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsLessonPlanOwner())
            {
                await SeedContextAsync(); await SeedLessonPlanOwnerAsync();
                await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
                    new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId), autoSave: true);
                var assigned = await GetRequiredService<IRepository<AssignedSubject, Guid>>().SingleAsync(row => row.StandardId == StandardId && row.SubjectId == SubjectId);
                var secondId = Guid.NewGuid();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(new AssignedSubjectChapter(
                    secondId, assigned.Id, 2, "Second chapter", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                    { TenantId = TenantId, AssignedSubjectId = assigned.Id }, autoSave: true);
                var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Chapter practice");
                var assignmentType = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "Chapter assessment",
                    defaultMarks: 100, defaultEvaluationMethod: EvaluationMethod.Marks);
                var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Linked practice", new string('b', 12000),
                    homeworkType.Id, SubmissionType.OnlineText, TenantId)
                {
                    ChapterId = ChapterId,
                    ChapterIdsJson = $"legacy: {ChapterId.ToString().ToUpperInvariant()} and {secondId}"
                };
                var assignment = new AssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Linked assessment", new string('b', 12000),
                    assignmentType.Id, SubmissionType.OnlineText, 100, TenantId)
                {
                    ChapterIdsJson = JsonSerializer.Serialize(new[] { ChapterId.ToString(), secondId.ToString().ToUpperInvariant(), secondId.ToString() })
                };
                await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
                await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
                var service = GetRequiredService<IChapterAppService>();
                if (lookup)
                {
                    var rows = await service.GetLookupAsync(StandardId, SubjectId);
                    rows.Select(row => row.Id).ShouldBe(new[] { ChapterId, secondId });
                    capture.ReadCommands.ShouldAllBe(sql => !sql.Contains("\"academic_HomeworkAssignments\"", StringComparison.Ordinal) &&
                        !sql.Contains("\"academic_Assignments\"", StringComparison.Ordinal) && !sql.Contains("LessonPlans", StringComparison.Ordinal) &&
                        !sql.Contains("TeacherDiaries", StringComparison.Ordinal));
                }
                else
                {
                    var rows = await service.GetByStandardAndSubjectAsync(StandardId, SubjectId);
                    rows.Count.ShouldBe(2);
                    rows.ShouldAllBe(row => row.HomeworkStatus == "Mapped" && row.AssessmentStatus == "Mapped");
                }
                db.ChangeTracker.Entries<HomeworkAssignmentEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignmentEntity>().ShouldBeEmpty();
            }
        });
    }
}
