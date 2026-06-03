using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeeIdentityUniquenessChecker(
	IEmployeeRepository employeeRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task EnsurePanAndAdharAreUniqueAsync(
		string? panNo,
		string? adharNo,
		Guid? currentEmployeeId = null)
	{
		var query = await employeeRepository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(panNo))
		{
			var panExists = await asyncExecuter.AnyAsync(
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
			var adharExists = await asyncExecuter.AnyAsync(
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

	public static string? NormalizePan(string? panNo) =>
		string.IsNullOrWhiteSpace(panNo) ? panNo : panNo.Trim().ToUpperInvariant();
}
