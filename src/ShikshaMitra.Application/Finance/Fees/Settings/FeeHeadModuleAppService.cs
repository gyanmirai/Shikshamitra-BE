using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeHeadModuleAppService : ApplicationService, IFeeHeadModuleAppService
{
    public Task<List<FeeHeadModuleDto>> GetListAsync()
    {
        var modules = System.Enum.GetValues<FeeHeadModule>()
            .Select(m => new FeeHeadModuleDto
            {
                Value = m.ToString(),
                DisplayName = ToDisplayName(m)
            })
            .ToList();

        return Task.FromResult(modules);
    }

    private static string ToDisplayName(FeeHeadModule module) => module switch
    {
        FeeHeadModule.ClassManagement => "Class Management",
        FeeHeadModule.HRStaff => "HR/Staff",
        FeeHeadModule.ResourceManagement => "Resource Management",
        FeeHeadModule.SecurityGuard => "Security Guard",
        FeeHeadModule.InstituteSetup => "Institute Setup",
        _ => module.ToString()
    };
}
