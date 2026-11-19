using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class SubstitutionAppService_Tests
{
	[Fact]
	public async Task History_Should_Filter_And_Page_Before_Materializing_Substitutions()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actorId = Guid.NewGuid();
			await CreateEmployeeAsync(actorId, "History", "Administrator");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), null, actorId), autoSave: true);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actorId, "admin")))
			{
				var date = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
				for (var index = 0; index < 40; index++)
					await _substitutionRepository.InsertAsync(ReadFixture(date, index), autoSave: true);
				for (var index = 0; index < 15; index++)
					await _substitutionRepository.InsertAsync(ReadFixture(date.AddDays(-10), index), autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var page = await _substitutionAppService.GetListAsync(new GetSubstitutionsInput
				{ Date = new DateTimeOffset(date), SkipCount = 3, MaxResultCount = 10 });
				page.TotalCount.ShouldBe(40);
				page.Items.Select(item => item.Period).ShouldBe(Enumerable.Range(3, 10).Select(index => $"Period {index}"));
				capture.ReadsFor("SubstitutionRequests").ShouldBeGreaterThan(0);
				capture.ReadsFor("SubstitutionRequests").ShouldBeLessThanOrEqualTo(15,
					"History rows outside the requested page must remain in SQL.");
			}
		});
	}

	[Fact]
	public async Task History_Should_Resolve_Missing_Teacher_Names_Once_For_The_Page()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actorId = Guid.NewGuid();
			await CreateEmployeeAsync(actorId, "Names", "Administrator");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), null, actorId), autoSave: true);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actorId, "admin")))
			{
				var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "MissingName", "Teacher");
				var date = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
				for (var index = 0; index < 8; index++)
					await _substitutionRepository.InsertAsync(ReadFixture(date, index, teacher.Id, ""), autoSave: true);
				await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var page = await _substitutionAppService.GetListAsync(new GetSubstitutionsInput
				{ Date = new DateTimeOffset(date), MaxResultCount = 10 });
				page.Items.Count.ShouldBe(8);
				page.Items.ShouldAllBe(item => item.OriginalTeacherName == teacher.EmployeeName && item.OriginalTeacherId == teacher.Id);
				capture.CommandsFor("Employees").ShouldBeLessThanOrEqualTo(1);
			}
		});
	}

	private static SubstitutionRequest ReadFixture(DateTime date, int index, Guid? teacherId = null, string teacherName = "Teacher") =>
		new(Guid.NewGuid(), null, date, $"Period {index}", new TimeOnly(8, 0).AddMinutes(index * 3),
			new TimeOnly(8, 0).AddMinutes(index * 3 + 2), "Class", "A", "Science", teacherName, "Room",
			"Coverage", SubstitutionPriority.Normal, SubstitutionSource.Manual, false, originalTeacherId: teacherId);
}
