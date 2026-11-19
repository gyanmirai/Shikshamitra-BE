using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupReadiness(
	InitialSetupStatusReader reader, ITenantRepository tenants,
	InitialSetupStructureReader structure,
	IRepository<IdentityUser, Guid> users) : ShikshaMitraAppService
{
	public async Task CheckAsync(InstituteSetupProgress progress)
	{
		var tenantId = reader.RequireTenant();
		if (InitialSetupDraftState.PendingSteps(progress).Length > 0)
			throw new BusinessException(InitialSetupErrorCodes.UnconfirmedChanges);
		var tenant = await tenants.GetAsync(tenantId);
		foreach (var property in new[] { "InstituteName", "Email", "ContactNumber", "Address1", "City", "State", "Country", "PostalCode" })
			if (string.IsNullOrWhiteSpace(InitialSetupStatusReader.ReadTenantText(tenant, property)))
				throw new BusinessException(InitialSetupErrorCodes.ProfileRequired);
		if (progress.AcademicYearId == null || progress.DivisionId == null ||
			!await structure.IsReadyAsync(tenantId, progress.AcademicYearId, progress.DivisionId))
			throw new BusinessException(InitialSetupErrorCodes.CompleteStructureRequired);
		if (!await users.AnyAsync(x => x.TenantId == tenantId && x.Id == CurrentUser.Id && x.IsActive))
			throw new BusinessException(InitialSetupErrorCodes.ActiveAdministratorRequired);
	}
}
