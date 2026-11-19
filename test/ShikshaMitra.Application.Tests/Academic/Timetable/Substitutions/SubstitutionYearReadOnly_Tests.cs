using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionYearReadOnly_Tests : LessonPlanProgressTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Explicit_Year_Repair_Should_Be_Bounded_Audited_And_Require_Tenant_Wide_Scope(bool restricted)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				var year = new AcademicYear(Guid.NewGuid(), TenantId, "Repair year", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31));
				await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
					new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId,
						academicYearId: restricted ? year.Id : null, standardId: restricted ? StandardId : null,
						divisionId: restricted ? DivisionId : null), autoSave: true);
				var repository = GetRequiredService<IRepository<SubstitutionRequest, Guid>>();
				var rows = Enumerable.Range(0, 2).Select(index => new SubstitutionRequest(Guid.NewGuid(), TenantId,
					new DateTime(2026, 9, 2 + index), "P1", new TimeOnly(8, 0), new TimeOnly(9, 0), "Class", "B", "Science",
					"Teacher", "Room", "Repair", SubstitutionPriority.Normal, SubstitutionSource.Manual, false)).ToArray();
				await repository.InsertManyAsync(rows, autoSave: true);
				var otherTenant = Guid.NewGuid();
				var other = new SubstitutionRequest(Guid.NewGuid(), otherTenant, new DateTime(2026, 9, 2), "P1", new TimeOnly(8, 0),
					new TimeOnly(9, 0), "Class", "B", "Science", "Teacher", "Room", "Other tenant", SubstitutionPriority.Normal, SubstitutionSource.Manual, false);
				using (Tenant.Change(otherTenant)) await repository.InsertAsync(other, autoSave: true);
				if (restricted)
				{
					var error = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => InvokeYearRepairAsync(year.Id));
					error.Code.ShouldBe("Substitution:AcademicYearRepairRequiresTenantWideScope");
					rows.ShouldAllBe(row => row.AcademicYearId == null);
					return;
				}
				var first = await InvokeYearRepairAsync(year.Id);
				first.GetProperty("updatedCount").GetInt32().ShouldBe(1);
				first.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
				var second = await InvokeYearRepairAsync(year.Id);
				second.GetProperty("updatedCount").GetInt32().ShouldBe(1);
				second.GetProperty("hasMore").GetBoolean().ShouldBeFalse();
				var logs = await GetRequiredService<IRepository<SubstitutionAuditLog, Guid>>().GetListAsync(log => log.Action == "Academic Year Repaired");
				logs.Count.ShouldBe(2);
				logs.ShouldAllBe(log => log.ActorUserId == TeacherUserId && log.Details!.Contains(year.Id.ToString()));
				using (Tenant.Change(otherTenant)) (await repository.GetAsync(other.Id)).AcademicYearId.ShouldBeNull();
			}
		});
	}

	private async Task<JsonElement> InvokeYearRepairAsync(Guid yearId)
	{
		var type = typeof(SubstitutionAppService).Assembly.GetType("ShikshaMitra.Academic.Timetable.Substitutions.SubstitutionAcademicYearRepair");
		type.ShouldNotBeNull("An explicit maintenance repair must replace read-time writes.");
		var service = ServiceProvider.GetService(type!);
		service.ShouldNotBeNull();
		var task = (Task)type!.GetMethod("RepairAsync")!.Invoke(service, new object[] { yearId, 1 })!;
		await task;
		return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task), new JsonSerializerOptions(JsonSerializerDefaults.Web));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Legacy_Year_Inference_Should_Remain_Visible_Without_Writing_On_Read(bool dashboard)
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
				var year = new AcademicYear(Guid.NewGuid(), TenantId, "Legacy read year", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31));
				await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
				var date = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
				var legacy = new SubstitutionRequest(Guid.NewGuid(), TenantId, date, "P1", new TimeOnly(8, 0), new TimeOnly(9, 0),
					"Class", "B", "Science", "Teacher", "Room", "Legacy year", SubstitutionPriority.Normal, SubstitutionSource.Manual,
					false, standardId: StandardId, divisionId: DivisionId, subjectId: SubjectId, originalTeacherId: TeacherEmployeeId);
				var repository = GetRequiredService<IRepository<SubstitutionRequest, Guid>>();
				await repository.InsertAsync(legacy, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var service = GetRequiredService<ISubstitutionAppService>();
				if (dashboard) (await service.GetDashboardAsync(new DateTimeOffset(date), year.Id)).Pending.ShouldBe(1);
				else (await service.GetListAsync(new GetSubstitutionsInput { AcademicYearId = year.Id, MaxResultCount = 10 }))
					.Items.ShouldHaveSingleItem().Id.ShouldBe(legacy.Id);
				db.ChangeTracker.Clear();
				(await repository.GetAsync(legacy.Id)).AcademicYearId.ShouldBeNull();
				capture.CommandsFor("UPDATE").ShouldBe(0);
			}
		});
	}
}
