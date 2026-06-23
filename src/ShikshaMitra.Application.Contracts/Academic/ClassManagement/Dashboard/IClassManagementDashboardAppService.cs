using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.ClassManagement.Dashboard;

public interface IClassManagementDashboardAppService : IApplicationService
{
	Task<ClassManagementDashboardDto> GetDashboardAsync(Guid academicMediumId, string? role = null);
}
