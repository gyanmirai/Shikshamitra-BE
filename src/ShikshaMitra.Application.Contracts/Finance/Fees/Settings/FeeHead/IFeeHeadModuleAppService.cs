using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeHeadModuleAppService : IApplicationService
{
    Task<List<FeeHeadModuleDto>> GetListAsync();
}
