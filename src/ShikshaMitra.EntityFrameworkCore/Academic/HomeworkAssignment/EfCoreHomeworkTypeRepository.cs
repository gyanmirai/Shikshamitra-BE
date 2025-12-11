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
/// EF Core repository implementation for HomeworkType
/// </summary>
public class EfCoreHomeworkTypeRepository
	: EfCoreRepository<ShikshaMitraDbContext, HomeworkType, Guid>,
		IHomeworkTypeRepository
{
	public EfCoreHomeworkTypeRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
	)
		: base(dbContextProvider) { }

	/// <summary>
	/// Finds a homework type by name and academic medium ID
	/// Uses case-insensitive comparison for name matching
	/// </summary>
	public async Task<HomeworkType?> FindByNameAndAcademicMediumIdAsync(
		string name,
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(
			x => EF.Functions.Like(x.Name, name) && x.AcademicMediumId == academicMediumId,
			GetCancellationToken(cancellationToken)
		);
	}

	/// <summary>
	/// Gets all homework types for a specific academic medium, ordered by name
	/// </summary>
	public async Task<List<HomeworkType>> GetListByAcademicMediumIdAsync(
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
