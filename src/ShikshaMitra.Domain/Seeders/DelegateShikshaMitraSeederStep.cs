using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;

namespace ShikshaMitra.Seeders;

public sealed class DelegateShikshaMitraSeederStep(
	string name,
	string version,
	int order,
	ShikshaMitraSeederTenantMode tenantMode,
	Func<DataSeedContext, Guid?, string, Task> seedAsync,
	Func<DataSeedContext, Guid?, string, Task<bool>>? canSkipAsync = null,
	bool useCompletionMarker = true,
	IReadOnlySet<string>? profiles = null)
	: IShikshaMitraSeederStep
{
	public string Name { get; } = name;
	public string Version { get; } = version;
	public int Order { get; } = order;
	public ShikshaMitraSeederTenantMode TenantMode { get; } = tenantMode;
	public bool UseCompletionMarker { get; } = useCompletionMarker;

	public bool RunsInProfile(string profile) =>
		profiles == null || profiles.Contains(profile);

	public Task<bool> CanSkipAsync(DataSeedContext context, Guid? tenantId, string profile) =>
		canSkipAsync?.Invoke(context, tenantId, profile) ?? Task.FromResult(false);

	public Task SeedAsync(DataSeedContext context, Guid? tenantId, string profile) =>
		seedAsync(context, tenantId, profile);
}
