using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings;

public class InstituteSetupAppService(IRepository<InstituteSetupExtraProperties, Guid> instituteSetupRepository) : ApplicationService, IInstituteSetupAppService
{
	public async Task<InstituteSetupState> GetCurrentInstituteSetupState()
	{
		try
		{
			Guid? tenantId = CurrentTenant?.Id;

			var instituteSetup = await instituteSetupRepository.FindAsync(x => x.TenantId == tenantId);

			if (instituteSetup == null)
			{
				return InstituteSetupState.WelcomeScreen;
			}
			else
			{
				return instituteSetup.State;
			}
		}
		catch (Exception ex)
		{
			throw new Exception("Error retrieving current institute setup state", ex);
		}
	}

	public async Task<InstituteSetupState> UpdateInstituteSetupStateAsync(InstituteSetupState newState)
	{
		try
		{
			Guid? tenantId = CurrentTenant?.Id;
			var instituteSetup = await instituteSetupRepository.FindAsync(x => x.TenantId == tenantId);
			if (instituteSetup == null)
			{
				instituteSetup = new InstituteSetupExtraProperties(tenantId, newState);
				await instituteSetupRepository.InsertAsync(instituteSetup, autoSave: true);
			}
			else
			{
				instituteSetup.UpdateState(newState);
				await instituteSetupRepository.UpdateAsync(instituteSetup, autoSave: true);
			}
			return instituteSetup.State;
		}
		catch (Exception ex)
		{
			throw new Exception("Error updating institute setup state", ex);
		}
	}
}
