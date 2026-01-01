using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Settings;

public interface IExaminationSettingsAppService : IApplicationService
{
	Task CreateGradingScaleAsync(Guid academicMediumId, CreateGradingScaleDto input);
	
	Task<GradingScaleListResponseDto> GetAllGradingScalesAsync(Guid academicMediumId);
	
	Task<GradingScaleSingleResponseDto> GetGradingScaleByIdAsync(Guid id, Guid academicMediumId);
	
	Task UpdateGradingScaleAsync(Guid id, Guid academicMediumId, UpdateGradingScaleDto input);
	
	Task DeleteGradingScaleAsync(Guid id, Guid academicMediumId);
}
