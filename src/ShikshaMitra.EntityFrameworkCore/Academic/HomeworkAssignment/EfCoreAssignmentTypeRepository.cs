using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Homework;

/// <summary>
/// EF Core repository implementation for AssignmentType
/// </summary>
public class EfCoreAssignmentTypeRepository
	: EfCoreRepository<ShikshaMitraDbContext, AssignmentType, Guid>,
		IAssignmentTypeRepository
{
	public EfCoreAssignmentTypeRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
	)
		: base(dbContextProvider) { }

	/// <summary>
	/// Finds an assignment type by name and academic medium ID
	/// </summary>
	public async Task<AssignmentType?> FindByNameAndAcademicMediumIdAsync(
		string name,
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(
			x => x.Name == name && x.AcademicMediumId == academicMediumId,
			GetCancellationToken(cancellationToken)
		);
	}

	/// <summary>
	/// Gets all assignment types for a specific academic medium, ordered by name
	/// </summary>
	public async Task<List<AssignmentType>> GetListByAcademicMediumIdAsync(
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.AcademicMediumId == academicMediumId)
			.OrderBy(x => x.Name)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}
}
