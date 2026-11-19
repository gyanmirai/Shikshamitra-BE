using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Dashboard;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Application.Tests.Dashboard;

public class DashboardReadinessProjection_Tests
{
	[Theory]
	[InlineData("leadership")]
	[InlineData("principalAdmin")]
	[InlineData("teacher")]
	[InlineData("financeHr")]
	public async Task Unenriched_Readiness_Does_Not_Invent_Counts_Dates_Or_Health(string template)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		var user = Substitute.For<ICurrentUser>();
		user.Roles.Returns(Array.Empty<string>());
		var focus = new DashboardFocusReader(permissions, null!, null!, null!, NullLogger<DashboardFocusReader>.Instance);
		var reader = new DashboardSummaryReader(DashboardSummarySlicePipeline.CreateForTesting([]), permissions,
			NullLogger<DashboardSummaryReader>.Instance, focus, null!, user);
		var result = await reader.GetSummaryAsync(new() { DashboardTemplate = template }, new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));
		result.ActionItems.ShouldAllBe(x => x.Count == null && x.Status == "info");
		result.CalendarItems.ShouldAllBe(x => x.Count == null && x.DueLabel == null && x.Status == "info");
		result.ModuleSnapshots.ShouldAllBe(x => x.Health == null && x.Status == "info");
		result.Announcements.ShouldAllBe(x => x.Title != "Board practical timetable published" && x.Title != "PTM circular pending acknowledgement");
	}
}
