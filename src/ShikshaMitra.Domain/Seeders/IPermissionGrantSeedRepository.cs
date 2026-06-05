using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public interface IPermissionGrantSeedRepository
{
	Task<int> InsertGrantsIgnoringDuplicatesAsync(
		string providerName,
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken = default);

	Task<int> InsertRoleGrantsIgnoringDuplicatesAsync(
		string providerKey,
		IReadOnlyCollection<string> permissionNames,
		Guid? tenantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// For each role that already has a legacy permission grant, inserts the mapped target grants.
	/// </summary>
	Task<int> MirrorRoleGrantsIgnoringDuplicatesAsync(
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> legacyToTargets,
		Guid? tenantId,
		CancellationToken cancellationToken = default);
}
