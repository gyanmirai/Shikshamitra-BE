using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanWorkspaceFilterRegression_Tests : LessonPlanProgressTestBase
{
    [Theory]
    [InlineData("lessonList", "plannedDate")]
    [InlineData("teacherDiary", "plannedDate")]
    [InlineData("reviewQueue", "submittedDate")]
    [InlineData("reviewQueue", "pendingSince")]
    public async Task Workspace_Date_Filter_Should_Use_The_Shared_Table_Day_First_Format(string view, string column)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsLessonPlanOwner())
            {
                await SeedContextAsync();
                await SeedLessonPlanOwnerAsync();
                var dates = new[]
                {
                    new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)
                };
                var plans = dates.Select(date =>
                {
                    var plan = new LessonPlan(Guid.NewGuid(), TenantId, YearId, TeacherEmployeeId,
                        StandardId, DivisionId, SubjectId, ChapterId, date, $"Lesson {date:yyyy-MM-dd}", 30);
                    plan.Submit();
                    return plan;
                }).ToList();
                await Plans.InsertManyAsync(plans, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();

                foreach (var plan in plans.Take(2))
                {
                    foreach (var operation in new[] { "equals", "after", "before" })
                    {
                        var result = await Service.GetWorkspaceAsync(new GetLessonPlanWorkspaceInput
                        {
                            AcademicYearId = YearId,
                            View = view,
                            MaxResultCount = 10,
                            UiCriteria = JsonSerializer.Serialize(new { todayIso = "2026-10-28" }),
                            Filters = JsonSerializer.Serialize(new[]
                            {
                                new { column, type = operation, value = plan.PlannedDate.ToString("dd-MM-yyyy"), valueType = "date" }
                            })
                        });
                        var expectedIds = plans.Where(candidate => operation switch
                        {
                            "after" => candidate.PlannedDate.Date >= plan.PlannedDate.Date,
                            "before" => candidate.PlannedDate.Date <= plan.PlannedDate.Date,
                            _ => candidate.Id == plan.Id
                        }).Select(candidate => candidate.Id).ToArray();

                        result.TotalCount.ShouldBe(expectedIds.Length);
                        result.Items.Select(item => item.Id).ShouldBe(expectedIds, ignoreOrder: true);
                    }

                    foreach (var format in new[] { "yyyy-MM-dd", "dd-MM-yyyy" })
                    {
                        var result = await Service.GetWorkspaceAsync(new GetLessonPlanWorkspaceInput
                        {
                            AcademicYearId = YearId,
                            View = view,
                            MaxResultCount = 10,
                            UiCriteria = JsonSerializer.Serialize(new { todayIso = "2026-10-28" }),
                            SearchColumns = new[] { column },
                            SearchTerm = plan.PlannedDate.ToString(format)
                        });

                        result.TotalCount.ShouldBe(1);
                        result.Items.Single().Id.ShouldBe(plan.Id);
                    }
                }
            }
        });
    }
}
