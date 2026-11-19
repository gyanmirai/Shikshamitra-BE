using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Setup;

public interface IFinanceSetupAppService : IApplicationService
{
	Task<FinanceSetupStateDto> GetAsync(Guid academicYearId);
	Task<FinanceSetupStateDto> SaveProgressAsync(Guid academicYearId, FinanceSetupProgressInput input);
}
