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

    Task CreateExamTypeAsync(Guid academicMediumId, CreateExamTypeDto input);

    Task<ExamTypeListResponseDto> GetExamTypesAsync(GetExamTypesInput input);

    Task<ExamTypeSingleResponseDto> GetExamTypeByIdAsync(Guid id, Guid academicMediumId);

    Task UpdateExamTypeAsync(Guid id, Guid academicMediumId, UpdateExamTypeDto input);

    Task DeleteExamTypeAsync(Guid id, Guid academicMediumId);
}
