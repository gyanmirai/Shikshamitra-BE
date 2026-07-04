using System.Threading.Tasks;

namespace ShikshaMitra.Dashboard;

public sealed record DashboardSummarySliceRegistration(
	string Name,
	int Order,
	string AffectedZone,
	string FailureMessage,
	IDashboardSummarySliceProvider Provider);

public sealed class DashboardSummarySliceContext(
	DashboardSummaryDto summary,
	DashboardDateRangeDto dateRange)
{
	public DashboardSummaryDto Summary { get; } = summary;
	public DashboardDateRangeDto DateRange { get; } = dateRange;
}

public interface IDashboardSummarySliceProvider
{
	Task ApplyAsync(DashboardSummarySliceContext context);
}
