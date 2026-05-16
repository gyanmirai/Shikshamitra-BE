using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IDayCalculationSettingAppService : IApplicationService
{
	Task<DayCalculationSettingDto> GetAsync(Guid academicYearId);
	Task<DayCalculationResponseDto> SaveAsync(DayCalculationRequestDto input);
	Task<DayCalculationSettingDto> PreviewAsync(DayCalculationRequestDto input);
}
