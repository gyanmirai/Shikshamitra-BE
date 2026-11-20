using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Dashboard;
using Shouldly;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<FinanceExternalPositionQueryRow> DashboardAlertPositionAsync(Fixture owner, Guid? year) =>
		WithUnitOfWorkAsync(async () => await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(
			await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Dashboard, year))));

	private static void AssertNoDashboardCollectionAlerts(LibraryDashboardOverviewDto overview) =>
		overview.Alerts.ShouldNotContain(alert => alert.Type == "pendingCharges" || alert.Type == "overdueCharges");

	private static void AssertDashboardFinancialReviewNotice(LibraryDashboardOverviewDto overview, string type)
	{
		var notice = overview.Alerts.Where(alert => alert.Type == type).ShouldHaveSingleItem();
		notice.Count.ShouldBe(1); notice.Severity.ShouldBe("warning");
		notice.Title.Contains("review", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}
}
