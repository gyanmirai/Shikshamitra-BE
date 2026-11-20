using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task AssertDashboardConsumerAuthorityAsync(Fixture owner, string[] denied) => WithUnitOfWorkAsync(async () =>
	{
		var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Dashboard.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(denied)).Result.Values.ShouldNotContain(PermissionGrantResult.Granted);
		(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
	});

	private static JsonElement DashboardFinanceSummary(LibraryDashboardOverviewDto overview)
	{
		var json = JsonSerializer.SerializeToElement(overview, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		json.TryGetProperty("financePosition", out var summary).ShouldBeTrue(
			"Verified Finance positions must remain separate from original assessment/local declaration fields.");
		return summary;
	}
}
