using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public interface IEmployeeAssignableRoleAppService : IApplicationService
{
	Task<ListResultDto<EmployeeAssignableRoleDto>> GetListAsync();
}
