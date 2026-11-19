using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Dashboard;

[Authorize(ShikshaMitraModulePermissions.Dashboard)]
[RemoteService(false)]
public class MainDashboardAppService(DashboardSummaryReader summaryReader)
	: ApplicationService, IMainDashboardAppService
{
	public Task<DashboardSummaryDto> GetSummaryAsync(DashboardSummaryRequestDto input) =>
		summaryReader.GetSummaryAsync(input, Clock.Now);

	public Task<List<DashboardWidgetMetadataDto>> GetWidgetMetadataAsync() =>
		Task.FromResult(DashboardWidgetCatalog.WidgetDefinitions
			.OrderBy(widget => widget.Key)
			.Select(widget => new DashboardWidgetMetadataDto
			{
				Id = widget.Key,
				Zone = widget.Value.Zone,
				DrilldownUrl = widget.Value.DrilldownUrl,
				RequiredPermissions = widget.Value.RequiredPermissions.OrderBy(permission => permission).ToList(),
				Owner = widget.Value.Owner
			})
			.ToList());
}
