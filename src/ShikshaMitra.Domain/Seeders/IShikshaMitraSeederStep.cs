using System;
using System.Threading.Tasks;
using Volo.Abp.Data;

namespace ShikshaMitra.Seeders;

public interface IShikshaMitraSeederStep
{
	string Name { get; }
	string Version { get; }
	int Order { get; }
	ShikshaMitraSeederTenantMode TenantMode { get; }
	bool UseCompletionMarker { get; }
	bool RunsInProfile(string profile);
	Task<bool> CanSkipAsync(DataSeedContext context, Guid? tenantId, string profile);
	Task SeedAsync(DataSeedContext context, Guid? tenantId, string profile);
}
