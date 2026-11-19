using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public interface ISchoolIdentityAssetRepository : IRepository<SchoolIdentityAsset, Guid>
{
	Task<bool> TryClaimExpiredDraftAsync(Guid id, DateTime cutoff);
	Task CompleteDraftCleanupAsync(Guid id);
}
