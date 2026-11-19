using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.InitialSetup;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings;

/// <summary>Compatibility reads; legacy callers cannot bypass validated completion.</summary>
[Authorize]
public class InstituteSetupAppService(IInitialSetupAppService setup) : ShikshaMitraAppService, IInstituteSetupAppService
{
    public async Task<InstituteSetupState> GetCurrentInstituteSetupState()
    {
        var progress = await setup.GetAsync();
        return progress.IsCompleted ? InstituteSetupState.SetupCompleted : progress.CurrentStep switch
        {
            0 => InstituteSetupState.WelcomeScreen,
            1 => InstituteSetupState.TenantDetails,
            _ => InstituteSetupState.AcademicSetup
        };
    }
    public async Task<InstituteSetupState> UpdateInstituteSetupStateAsync(InstituteSetupState newState)
    {
        var current = await setup.GetAsync();
        if (newState == InstituteSetupState.SetupCompleted)
            await setup.CompleteAsync(new SetupConcurrencyInput { ConcurrencyStamp = current.ConcurrencyStamp });
        else if (newState == InstituteSetupState.TenantDetails && current.CurrentStep == 0)
            await setup.AdvanceAsync(new AdvanceSetupInput { Step = 0, ConcurrencyStamp = current.ConcurrencyStamp });
        else throw new BusinessException(InitialSetupErrorCodes.LegacyWriteUnsupported);
        return await GetCurrentInstituteSetupState();
    }
}
