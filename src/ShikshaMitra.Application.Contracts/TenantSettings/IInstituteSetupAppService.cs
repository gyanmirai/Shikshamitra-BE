using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings;

public interface IInstituteSetupAppService
{
	Task<InstituteSetupState> GetCurrentInstituteSetupState();
	Task<InstituteSetupState> UpdateInstituteSetupStateAsync(InstituteSetupState newState);
}
