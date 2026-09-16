using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicDashboardAggregation_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Principal_Summaries_Should_Count_Rows_Not_Status_Groups_And_Exclude_Other_Tenants()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			var tenant = GetRequiredService<ICurrentTenant>();
			var plans = GetRequiredService<IRepository<LessonPlan, Guid>>();
			using (tenant.Change(tenantId))
			{
				for (var index = 0; index < 3; index++)
				{
					var plan = CreatePlan(tenantId, $"Submitted {index}");
					plan.Submit();
					await plans.InsertAsync(plan, autoSave: true);
				}
				await plans.InsertAsync(CreatePlan(tenantId, "Draft"), autoSave: true);
				await GetRequiredService<IRepository<AcademicCalendarSetting, Guid>>().InsertAsync(
					new AcademicCalendarSetting(Guid.NewGuid(), tenantId, Guid.NewGuid()), autoSave: true);
			}
			using (tenant.Change(Guid.NewGuid()))
			{
				var unrelated = CreatePlan(tenant.Id!.Value, "Other school");
				unrelated.Submit();
				await plans.InsertAsync(unrelated, autoSave: true);
			}

			using (tenant.Change(tenantId))
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var result = await GetRequiredService<IAcademicDashboardAppService>().GetPrincipalDashboardAsync();
				result.ActionCenterItems.Single(item => item.Id == "lesson-review").Count.ShouldBe(3);
				var diaryReadiness = result.ReadinessItems.Single(item => item.Key == "lesson-diary-readiness");
				diaryReadiness.TotalCount.ShouldBe(3);
				diaryReadiness.CompletedCount.ShouldBe(0);
				diaryReadiness.MissingCount.ShouldBe(3);
				result.ReadinessItems.Single(item => item.Key == "calendar-rules").Status.ShouldBe("complete");
				result.SyllabusProgress.ShouldBe(82);
				db.ChangeTracker.Entries<LessonPlan>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AcademicCalendarSetting>().ShouldBeEmpty();
			}
		});
	}

	private static LessonPlan CreatePlan(Guid tenantId, string topic) => new(
		Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 16, 3, 30, 0, DateTimeKind.Utc), topic, 25);
}
