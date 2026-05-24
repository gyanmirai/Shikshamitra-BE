using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Seeders;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Seeders;

public class EfCorePermissionGrantSeedRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	IGuidGenerator guidGenerator)
	: IPermissionGrantSeedRepository, ITransientDependency
{
	private const string RoleProviderName = "R";

	public Task<int> InsertRoleGrantsIgnoringDuplicatesAsync(
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken = default) =>
		InsertGrantsIgnoringDuplicatesAsync(RoleProviderName, providerKey, permissionNames, tenantId, cancellationToken);

	public async Task<int> InsertGrantsIgnoringDuplicatesAsync(
		string providerName,
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken = default)
	{
		var distinctPermissionNames = permissionNames
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (distinctPermissionNames.Length == 0)
		{
			return 0;
		}

		var dbContext = await dbContextProvider.GetDbContextAsync();
		if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
		{
			return await InsertPostgresAsync(dbContext, providerName, providerKey, distinctPermissionNames, tenantId, cancellationToken);
		}

		return await InsertEfAsync(dbContext, providerName, providerKey, distinctPermissionNames, tenantId, cancellationToken);
	}

	private async Task<int> InsertPostgresAsync(
		ShikshaMitraDbContext dbContext,
		string providerName,
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken)
	{
		var affectedRows = 0;
		foreach (var permissionName in permissionNames)
		{
			var id = guidGenerator.Create();
			affectedRows += await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AbpPermissionGrants"
					("Id", "TenantId", "Name", "ProviderName", "ProviderKey")
				SELECT
					{id}, {tenantId}, {permissionName}, {providerName}, {providerKey}
				WHERE NOT EXISTS (
					SELECT 1
					FROM "AbpPermissionGrants"
					WHERE "TenantId" IS NOT DISTINCT FROM {tenantId}
						AND "Name" = {permissionName}
						AND "ProviderName" = {providerName}
						AND "ProviderKey" = {providerKey}
				)
				""", cancellationToken);
		}

		return affectedRows;
	}

	private async Task<int> InsertEfAsync(
		ShikshaMitraDbContext dbContext,
		string providerName,
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken)
	{
		var insertedCount = 0;
		foreach (var permissionName in permissionNames)
		{
			var exists = await dbContext.Set<PermissionGrant>().AnyAsync(x =>
				x.TenantId == tenantId &&
				x.Name == permissionName &&
				x.ProviderName == providerName &&
				x.ProviderKey == providerKey,
				cancellationToken);
			if (exists)
			{
				continue;
			}

			await dbContext.Set<PermissionGrant>().AddAsync(
				new PermissionGrant(
					guidGenerator.Create(),
					permissionName,
					providerName,
					providerKey,
					tenantId),
				cancellationToken);
			insertedCount++;
		}

		if (insertedCount > 0)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}

		return insertedCount;
	}
}
