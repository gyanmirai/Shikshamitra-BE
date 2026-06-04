using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
public class HRDashboardPreviewPayloadBuilder(
	HRDashboardSummaryReader summaryReader)
	: ApplicationService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<string> BuildAsync(string dashboardKey, UserScopeContext scope)
	{
		var defaults = new HRDashboardSummaryRequestDto
		{
			Date = Clock.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Month = Clock.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			Range = 7
		};
		var summary = await summaryReader.GetSummaryAsync(defaults, scope);

		return dashboardKey switch
		{
			HrDashboardKeys.Main => JsonSerializer.Serialize(summary, JsonOptions),
			HrDashboardKeys.Attendance => JsonSerializer.Serialize(HRDashboardProjection.BuildAttendancePreview(summary), JsonOptions),
			HrDashboardKeys.Payroll => JsonSerializer.Serialize(HRDashboardProjection.BuildPayrollPreview(summary), JsonOptions),
			_ => throw new BusinessException(HRErrorCodes.DashboardInvalidKey)
		};
	}
}
