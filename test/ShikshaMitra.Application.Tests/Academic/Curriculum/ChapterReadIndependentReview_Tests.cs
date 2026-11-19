using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class ChapterReadIndependentReview_Tests : LessonPlanProgressTestBase
{
    [Fact]
    public async Task Chapter_Links_Should_Retain_Overlapping_Guid_Substrings_In_Legacy_Text()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsLessonPlanOwner())
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
                    new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId), autoSave: true);
                var assigned = await GetRequiredService<IRepository<AssignedSubject, Guid>>().SingleAsync(row =>
                    row.StandardId == StandardId && row.SubjectId == SubjectId);
                var firstId = Guid.Parse("11111111-2222-3333-4444-555555555555");
                var secondId = Guid.Parse("55555555-6666-7777-8888-999999999999");
                foreach (var (id, number) in new[] { (firstId, 2), (secondId, 3) })
                    await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(new AssignedSubjectChapter(
                        id, assigned.Id, number, $"Overlap {number}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                        { TenantId = TenantId, AssignedSubjectId = assigned.Id }, autoSave: true);
                var legacyLinks = firstId + "-6666-7777-8888-999999999999";
                legacyLinks.Contains(firstId.ToString(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
                legacyLinks.Contains(secondId.ToString(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
                var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Legacy links");
                await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(new HomeworkAssignmentEntity(
                    Guid.NewGuid(), StandardId, SubjectId, "Overlapping links", "Legacy link text", type.Id, SubmissionType.OnlineText, TenantId)
                    { ChapterIdsJson = legacyLinks }, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var rows = await GetRequiredService<IChapterAppService>().GetByStandardAndSubjectAsync(StandardId, SubjectId);
                var affected = rows.Where(row => row.Id == firstId || row.Id == secondId).ToList();
                affected.Count.ShouldBe(2);
                affected.ShouldAllBe(row => row.HomeworkStatus == "Mapped");
            }
        });
    }
}
