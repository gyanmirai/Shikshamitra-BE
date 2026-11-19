using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanWorkspacePerformance_Tests : LessonPlanProgressTestBase
{
    [Theory]
    [InlineData("lessonList")]
    [InlineData("reviewQueue")]
    public async Task Workspace_Should_Page_Before_Hydrating_Plans_And_Diaries_And_Keep_Global_Counts(string view)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsLessonPlanOwner())
            {
                await SeedContextAsync(); await SeedLessonPlanOwnerAsync();
                var plans = new List<LessonPlan>();
                for (var index = 0; index < 1007; index++)
                {
                    var plan = new LessonPlan(Guid.NewGuid(), TenantId, YearId, TeacherEmployeeId,
                        StandardId, DivisionId, SubjectId, ChapterId, DateTime.UtcNow.Date,
                        index == 1006 ? "Rare topic after 1000" : $"Plan {index:D4}", 30);
                    plan.UpdatePlan(ChapterId, plan.Topic, new string('x', 1500), null, new string('y', 1500), 30);
                    plan.Submit();
                    plans.Add(plan);
                }
                await Plans.InsertManyAsync(plans, autoSave: true);
                await AddDiaryAsync(plans[0], 20);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var method = typeof(ILessonPlanAppService).GetMethod("GetWorkspaceAsync");
                method.ShouldNotBeNull("the active UI needs a bounded workspace read, rather than a 1000-row response");
                async Task<JsonElement> ReadAsync(int skip, string? search = null)
                {
                    var input = JsonSerializer.Deserialize(JsonSerializer.Serialize(new { AcademicYearId = YearId, View = view,
                        SkipCount = skip, MaxResultCount = 10, SearchTerm = search, SearchColumns = new[] { "topic" },
                        UiCriteria = JsonSerializer.Serialize(new { todayIso = DateTime.UtcNow.ToString("yyyy-MM-dd"), weekStartIso = DateTime.UtcNow.Date.ToString("yyyy-MM-dd") }) }), method.GetParameters()[0].ParameterType)!;
                    var task = (Task)method.Invoke(Service, new[] { input })!;
                    await task;
                    return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                }
                var page = await ReadAsync(10);
                page.GetProperty("totalCount").GetInt64().ShouldBe(1007);
                page.GetProperty("items").GetArrayLength().ShouldBe(10);
                page.GetProperty("summary").GetProperty("reviewStatusCounts").GetProperty("pending").GetInt32().ShouldBe(1007);
                page.GetProperty("summary").GetProperty("quickCounts").GetProperty("pendingDiaries").GetInt32().ShouldBe(1006);
                db.ChangeTracker.Entries<LessonPlan>().Count().ShouldBeLessThanOrEqualTo(10);
                db.ChangeTracker.Entries<TeacherDiary>().Count().ShouldBeLessThanOrEqualTo(10);
                var filtered = await ReadAsync(0, "Rare topic after 1000");
                filtered.GetProperty("totalCount").GetInt64().ShouldBe(1);
                filtered.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(plans[^1].Id);
            }
        });
    }
}
