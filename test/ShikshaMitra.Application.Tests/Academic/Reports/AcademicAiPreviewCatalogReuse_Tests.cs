using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Fact]
    public async Task Ai_Preview_Should_Build_Curriculum_Catalogue_Once_And_Keep_Chapter_Options()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var scope = await DynamicScopeAsync(assignment);
                var chapter = new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id, 1, "Selected chapter",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                { AssignedSubjectId = assignment.Id, TenantId = tenantId };
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(chapter, autoSave: true);
                var userId = Guid.NewGuid();
                await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(
                    userId, $"report-preview-{userId:N}", $"{userId:N}@example.test", tenantId), autoSave: true);
                using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test")));
                await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
                    Guid.NewGuid(), tenantId, userId, standardId: assignment.StandardId), autoSave: true);
                await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
                var snapshot = await GetRequiredService<AcademicDynamicReportCatalog>().BuildSnapshotAsync(scope);
                var chapterToken = snapshot.Chapters.Single().Token;
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new ReportQueryCapture(db.Database.GetDbConnection());
                var result = await GetRequiredService<IAcademicReportAppService>().CreateAiPreviewAsync(new()
                {
                    Module = AcademicReportModules.Curriculum, AcademicYearId = scope.AcademicYearId,
                    EducationBoardId = scope.EducationBoardId, EducationMediumId = scope.EducationMediumId,
                    Plan = new()
                    {
                        Module = AcademicReportModules.Curriculum, Dataset = AcademicDynamicDatasets.CurriculumChapters,
                        Title = "Selected curriculum chapter", Columns = ["chapterName"],
                        Filters = new() { ChapterTokens = [chapterToken] }
                    }
                });
                result.Preview.TotalCount.ShouldBe(1);
                result.Preview.Rows.Single()["chapterName"].ShouldBe(chapter.ChapterName);
                result.Plan.Filters.ChapterTokens.ShouldBe([chapterToken]);
                result.ApprovalToken.ShouldNotBeNullOrWhiteSpace();
                // One candidate page and its empty terminating page, without a second catalogue traversal.
                capture.Commands.Count(sql => sql.Contains("\"ChapterName\"", StringComparison.Ordinal) &&
                    !sql.Contains("\"ChapterNumber\"", StringComparison.Ordinal)).ShouldBe(2);
            }
        });
    }
}
