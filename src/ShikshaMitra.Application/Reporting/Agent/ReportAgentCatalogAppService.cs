using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Reporting.Agent;

[Authorize]
public class ReportAgentCatalogAppService(IAuthorizationService authorizationService)
	: ApplicationService, IReportAgentCatalogAppService
{
	public async Task<ReportAgentCatalogDto> GetAccessibleModulesAsync()
	{
		var modules = new List<ReportAgentModuleDto>();
		foreach (var definition in ReportAgentModuleCatalog.All)
		{
			if (!await authorizationService.IsGrantedAsync(definition.ViewPermission))
			{
				continue;
			}
			modules.Add(new ReportAgentModuleDto
			{
				Id = definition.Id,
				Label = definition.Label,
				Description = definition.Description,
				CanBuild = await authorizationService.IsGrantedAsync(definition.BuildPermission),
				CanExport = await authorizationService.IsGrantedAsync(definition.ExportPermission),
				Availability = definition.Availability
			});
		}
		return new ReportAgentCatalogDto { Modules = modules };
	}
}
