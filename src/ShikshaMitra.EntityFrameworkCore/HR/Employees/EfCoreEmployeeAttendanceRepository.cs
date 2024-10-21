using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Employees;

public class EfCoreEmployeeAttendanceRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EmployeeAttendance, Guid>(dbContextProvider),
		IEmployeeAttendanceRepository
{
	// override the GetListAsync method to include the Employee navigation property
	public override async Task<List<EmployeeAttendance>> GetListAsync(
		Expression<Func<EmployeeAttendance, bool>> predicate,
		bool includeDetails = false,
		CancellationToken cancellationToken = default)
	{
		// if includeDetails is true, include the Employee navigation property
		if (includeDetails)
		{
			return await (await GetQueryableAsync())
				.Include(x => x.Employee)
				.Where(predicate)
				.ToListAsync(GetCancellationToken(cancellationToken));
		}

		return await base.GetListAsync(predicate, includeDetails, cancellationToken);
	}
}