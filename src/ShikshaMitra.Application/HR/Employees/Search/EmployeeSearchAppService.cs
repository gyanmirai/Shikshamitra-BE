using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Search;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeSearchAppService(EmployeeSearchReader reader)
	: ApplicationService, IEmployeeSearchAppService
{
	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		return await reader.GetFilteredListAsync(input);
	}

	public Task<EmployeeOptionsResponse> GetOptionsAsync(GetEmployeeListInput input)
	{
		return reader.GetOptionsAsync(input);
	}
}
