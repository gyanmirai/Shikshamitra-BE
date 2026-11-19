using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Configurations.Standards;

public class EfCoreStandardRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Standard, Guid>(dbContextProvider), IStandardRepository
{
	/// <summary>
	/// Includes the full navigation chain needed by student registration:
	/// Standard → EducationMediumStage → EducationMedium → EducationBoard.
	/// <see cref="IgnoreAutoIncludes"/> prevents the circular auto-include cascade
	/// (EducationMediumStage.Standards → EducationMedium.Stages → ...) that would
	/// otherwise load the entire academic hierarchy for a single Standard lookup.
	/// Divisions and Shift are suppressed by IgnoreAutoIncludes so they are included explicitly.
	/// </summary>
	public override async Task<IQueryable<Standard>> WithDetailsAsync()
	{
		return (await GetDbSetAsync())
			.IgnoreAutoIncludes()
			.Include(s => s.Divisions)
			.Include(s => s.Shift)
			.Include(s => s.EducationMediumStage)
				.ThenInclude((EducationMediumStage ms) => ms.EducationMedium)
					.ThenInclude((EducationMedium m) => m.EducationBoard);
	}

	public async Task<Standard?> FindByNameAndEducationMediumStageIdAsync(string name, Guid educationMediumStageId, Guid? excludedId = null)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();
		var normalizedName = name.Trim().ToLower();
		return await dbSet.FirstOrDefaultAsync(standard =>
			standard.Id != excludedId &&
			standard.Name.Trim().ToLower() == normalizedName &&
			standard.EducationMediumStageId == educationMediumStageId
		);
	}

	public async Task<List<Standard>> GetStandardsByEducationMediumStageIdAsync(Guid educationMediumStageId)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(standard => standard.EducationMediumStageId == educationMediumStageId)
			.OrderBy(standard => standard.SortOrder).ThenBy(standard => standard.Name).ThenBy(standard => standard.Id)
			.ToListAsync();
	}

	public async Task<List<Standard>> GetStandardsByEducationMediumIdAsync(Guid educationMediumStageId, int skipCount,
		int maxResultCount)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();
		return await dbSet.Where(standard => standard.EducationMediumStageId == educationMediumStageId)
			.OrderBy(standard => standard.SortOrder).ThenBy(standard => standard.Name).ThenBy(standard => standard.Id)
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToListAsync();
	}
}
