using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeIdentifierUniquenessService(IEmployeeRepository employeeRepository)
	: ApplicationService, ITransientDependency
{
	public async Task EnsurePanAndAdharAreUniqueAsync(string? panNo, string? adharNo, Guid? currentEmployeeId = null)
	{
		var query = await employeeRepository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(panNo))
		{
			var panExists = await AsyncExecuter.AnyAsync(
				query.Where(employee =>
					employee.PanNo == panNo &&
					(!currentEmployeeId.HasValue || employee.Id != currentEmployeeId.Value)));

			if (panExists)
			{
				throw new BusinessException(HRErrorCodes.EmployeePanAlreadyExists)
					.WithData("panNo", panNo);
			}
		}

		if (!string.IsNullOrWhiteSpace(adharNo))
		{
			var adharExists = await AsyncExecuter.AnyAsync(
				query.Where(employee =>
					employee.AdharNo == adharNo &&
					(!currentEmployeeId.HasValue || employee.Id != currentEmployeeId.Value)));

			if (adharExists)
			{
				throw new BusinessException(HRErrorCodes.EmployeeAdharAlreadyExists)
					.WithData("adharNo", adharNo);
			}
		}
	}
}
