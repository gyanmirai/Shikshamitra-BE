using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class EfCoreSchoolIdentityProfileRepository(IDbContextProvider<ShikshaMitraDbContext> provider)
	: EfCoreRepository<ShikshaMitraDbContext, SchoolIdentityProfile, Guid>(provider)
{
	public override async Task<SchoolIdentityProfile> InsertAsync(SchoolIdentityProfile entity,
		bool autoSave = false, CancellationToken cancellationToken = default)
	{
		try { return await base.InsertAsync(entity, autoSave, cancellationToken); }
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException
			{ SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AppSchoolIdentityProfiles_TenantId_ScopeKey" })
		{
			throw new AbpDbConcurrencyException();
		}
	}
}
