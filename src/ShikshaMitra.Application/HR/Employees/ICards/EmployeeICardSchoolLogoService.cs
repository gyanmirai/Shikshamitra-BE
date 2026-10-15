using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardSchoolLogoService(SchoolIdentityDocumentService documents,
	SchoolIdentityScopeAccess scope, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<SchoolIdentityAsset, Guid> assets) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeICardAssetDownloadResult?> GetAsync(Guid? mediumStageId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View);
		if (mediumStageId.HasValue)
		{
			var stage = await stages.GetAsync(mediumStageId.Value);
			var medium = await mediums.GetAsync(stage.EducationMediumId);
			if (stage.TenantId != CurrentTenant.Id || medium.TenantId != CurrentTenant.Id)
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
			await scope.EnsureAsync(medium.EducationBoardId, medium.Id);
		}
		else await scope.EnsureAsync(null, null);
		var snapshot = await documents.CaptureForStageAsync(mediumStageId);
		var logo = (await documents.LoadAsync(snapshot)).Logo;
		if (logo == null || !snapshot.LogoAssetId.HasValue) return null;
		var asset = await assets.GetAsync(snapshot.LogoAssetId.Value);
		return new() { Content = logo, ContentType = asset.ContentType, FileName = asset.FileName };
	}
}
