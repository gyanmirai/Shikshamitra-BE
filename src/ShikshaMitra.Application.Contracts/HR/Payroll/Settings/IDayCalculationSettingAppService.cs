using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IDayCalculationSettingAppService : IApplicationService
{
	Task<DayCalculationResponseDto> SaveAsync(DayCalculationRequestDto input);
}
