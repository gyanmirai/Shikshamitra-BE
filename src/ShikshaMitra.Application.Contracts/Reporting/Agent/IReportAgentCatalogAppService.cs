using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Reporting.Agent;

public interface IReportAgentCatalogAppService : IApplicationService
{
	Task<ReportAgentCatalogDto> GetAccessibleModulesAsync();
}
