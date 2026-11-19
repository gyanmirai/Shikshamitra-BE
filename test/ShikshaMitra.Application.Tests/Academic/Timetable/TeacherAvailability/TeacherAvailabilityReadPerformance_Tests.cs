using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class TeacherAvailabilityAppService_Tests
{
	[Fact]
	public async Task Availability_History_Should_Filter_And_Page_Before_Materializing_Rows()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
			{
				var date = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
				for (var index = 0; index < 55; index++)
					await _availabilityRepository.InsertAsync(new TeacherAvailability(Guid.NewGuid(), null,
						$"Teacher {index}", index < 40 ? date : date.AddDays(-10), new TimeOnly(8, 0).AddMinutes(index * 3),
						new TimeOnly(8, 0).AddMinutes(index * 3 + 2), TeacherAvailabilityStatus.Unavailable,
						TeacherAvailabilitySource.Manual, "Coverage", false, false), autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var page = await _teacherAvailabilityAppService.GetListAsync(new GetTeacherAvailabilityInput
				{ Date = date, SkipCount = 3, MaxResultCount = 10 });
				page.TotalCount.ShouldBe(40);
				page.Items.Select(item => item.TeacherName).ShouldBe(Enumerable.Range(3, 10).Select(index => $"Teacher {index}"));
				capture.ReadsFor("TeacherAvailabilit").ShouldBeGreaterThan(0);
				capture.ReadsFor("TeacherAvailabilit").ShouldBeLessThanOrEqualTo(15);
			}
		});
	}
}
