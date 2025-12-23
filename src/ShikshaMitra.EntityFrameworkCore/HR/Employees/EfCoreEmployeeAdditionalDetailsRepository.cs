using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Employees;

/// <summary>
/// Entity Framework Core repository implementation for EmployeeAdditionalDetails.
/// </summary>
public class EfCoreEmployeeAdditionalDetailsRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EmployeeAdditionalDetails, Guid>(dbContextProvider),
		IEmployeeAdditionalDetailsRepository
{
	/// <inheritdoc />
	public async Task<EmployeeAdditionalDetails?> GetByEmployeeIdAsync(
		Guid employeeId,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> ExistsForEmployeeAsync(
		Guid employeeId,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.AnyAsync(e => e.EmployeeId == employeeId, cancellationToken);
	}
}



