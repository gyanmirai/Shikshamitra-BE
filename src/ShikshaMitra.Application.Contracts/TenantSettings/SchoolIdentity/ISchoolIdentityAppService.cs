using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public interface ISchoolIdentityAppService : IApplicationService
{
	Task<SchoolIdentityProfileDto> GetAsync(SchoolIdentityContextDto input);
	Task<SchoolIdentityProfileDto> GetBrandingAsync();
	Task<SchoolIdentityAssetDownloadDto> GetBrandingAssetAsync(SchoolIdentityAssetKind kind);
	Task<SchoolIdentityAssetDownloadDto> PreviewAsync(SchoolIdentityPreviewDto input);
	Task<SchoolIdentityProfileDto> UpdateAsync(SchoolIdentityUpdateDto input);
	Task<SchoolIdentityProfileDto> CopyAsync(SchoolIdentityCopyDto input);
	Task<List<SchoolIdentityRevisionDto>> GetHistoryAsync(SchoolIdentityContextDto input);
	Task<SchoolIdentityAssetDto> UploadAsync(SchoolIdentityAssetKind kind, Guid? boardId, Guid? mediumId,
		string fileName, byte[] bytes);
	Task<SchoolIdentityAssetDownloadDto> DownloadAsync(Guid id, SchoolIdentityContextDto input);
}
