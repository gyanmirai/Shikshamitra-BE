using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Setting;
public interface IGradeScaleAppService : IApplicationService
{
	Task<GradeScaleDto> GetGradeScaleByIdAsync(Guid id);
	Task<List<GradeScaleDto>> GetGradeScaleListAsync(Guid educationMediumId);
	Task<GradeScaleDto> CreateGradeScaleAsync(CreateGradeScaleDto createGradeScaleDto);
	Task<GradeScaleDto> UpdateGradeScaleAsync(Guid id, UpdateGradeScaleDto updateGradeScaleDto);
	Task<bool> DeleteGradeScaleAsync(Guid id);
}
