using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class EfCoreSchoolIdentityAssetRepository(IDbContextProvider<ShikshaMitraDbContext> provider)
	: EfCoreRepository<ShikshaMitraDbContext, SchoolIdentityAsset, Guid>(provider), ISchoolIdentityAssetRepository
{
	public async Task<bool> TryClaimExpiredDraftAsync(Guid id, DateTime cutoff) =>
		await (await GetQueryableAsync()).Where(asset => asset.Id == id && asset.TenantId == CurrentTenant.Id
			&& !asset.IsRetained && !asset.IsCleanupPending && asset.CreationTime <= cutoff)
			.ExecuteUpdateAsync(update => update.SetProperty(asset => asset.IsCleanupPending, true)) == 1;

	public async Task CompleteDraftCleanupAsync(Guid id) =>
		await (await GetQueryableAsync()).Where(asset => asset.Id == id && asset.TenantId == CurrentTenant.Id
			&& !asset.IsRetained && asset.IsCleanupPending).ExecuteDeleteAsync();
}
