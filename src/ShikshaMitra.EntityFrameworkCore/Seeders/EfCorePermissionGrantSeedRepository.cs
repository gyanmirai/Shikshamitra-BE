using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
	private const int PostgreSqlInsertBatchSize = 500;

	public Task<int> InsertRoleGrantsIgnoringDuplicatesAsync(
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken = default) =>
		InsertGrantsIgnoringDuplicatesAsync(RoleProviderName, providerKey, permissionNames, tenantId, cancellationToken);

	public async Task<int> MirrorRoleGrantsIgnoringDuplicatesAsync(
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> legacyToTargets,
		Guid? tenantId,
		CancellationToken cancellationToken = default)
	{
		if (legacyToTargets.Count == 0)
		{
			return 0;
		}

		var dbContext = await dbContextProvider.GetDbContextAsync();
		var legacyNames = legacyToTargets.Keys.ToArray();

		var roleKeys = await dbContext.Set<PermissionGrant>()
			.Where(g =>
				g.TenantId == tenantId &&
				g.ProviderName == RoleProviderName &&
				legacyNames.Contains(g.Name))
			.Select(g => g.ProviderKey)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (roleKeys.Count == 0)
		{
			return 0;
		}

		var grantsByRole = await dbContext.Set<PermissionGrant>()
			.Where(g =>
				g.TenantId == tenantId &&
				g.ProviderName == RoleProviderName &&
				roleKeys.Contains(g.ProviderKey))
			.Select(g => new { g.ProviderKey, g.Name })
			.ToListAsync(cancellationToken);

		var grantedByRole = grantsByRole
			.GroupBy(g => g.ProviderKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				g => g.Key,
				g => g.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase),
				StringComparer.OrdinalIgnoreCase);

		var insertsByRole = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var roleKey in roleKeys)
		{
			if (!grantedByRole.TryGetValue(roleKey, out var granted))
			{
				continue;
			}

			var roleTargets = new List<string>();
			foreach (var (legacyName, targets) in legacyToTargets)
			{
				if (!granted.Contains(legacyName))
				{
					continue;
				}

				roleTargets.AddRange(targets);
			}

			if (roleTargets.Count > 0)
			{
				insertsByRole[roleKey] = roleTargets;
			}
		}

		var affectedRows = 0;
		foreach (var (roleKey, permissionNames) in insertsByRole)
		{
			affectedRows += await InsertRoleGrantsIgnoringDuplicatesAsync(
				roleKey,
				permissionNames,
				tenantId,
				cancellationToken);
		}

		return affectedRows;
	}

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
		foreach (var batch in permissionNames.Chunk(PostgreSqlInsertBatchSize))
		{
			affectedRows += await ExecutePostgreSqlInsertBatchAsync(
				dbContext,
				providerName,
				providerKey,
				batch,
				tenantId,
				cancellationToken);
		}

		return affectedRows;
	}

	private async Task<int> ExecutePostgreSqlInsertBatchAsync(
		ShikshaMitraDbContext dbContext,
		string providerName,
		string providerKey,
		IReadOnlyList<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken)
	{
		var values = new List<string>(permissionNames.Count);
		var parameters = new List<NpgsqlParameter>(permissionNames.Count * 2 + 3)
		{
			new NpgsqlParameter<Guid?>("tenant_id", tenantId),
			new NpgsqlParameter<string>("provider_name", providerName),
			new NpgsqlParameter<string>("provider_key", providerKey)
		};

		for (var i = 0; i < permissionNames.Count; i++)
		{
			values.Add($"(@p{i}_id, @p{i}_name)");
			parameters.Add(new NpgsqlParameter<Guid>($"p{i}_id", guidGenerator.Create()));
			parameters.Add(new NpgsqlParameter<string>($"p{i}_name", permissionNames[i]));
		}

		var sql = $"""
			WITH input("Id", "Name") AS (
				VALUES {string.Join(",\n\t\t", values)}
			)
			INSERT INTO "AbpPermissionGrants"
				("Id", "TenantId", "Name", "ProviderName", "ProviderKey")
			SELECT
				input."Id", @tenant_id, input."Name", @provider_name, @provider_key
			FROM input
			WHERE NOT EXISTS (
				SELECT 1
				FROM "AbpPermissionGrants" existing
				WHERE existing."TenantId" IS NOT DISTINCT FROM @tenant_id
					AND existing."Name" = input."Name"
					AND existing."ProviderName" = @provider_name
					AND existing."ProviderKey" = @provider_key
			)
			""";

		return await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
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
