using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Setting;
public interface IExamTypeAppService : IApplicationService
{
    Task<ExamTypeDto> CreateExamTypeAsync(CreateExamTypeDto createExamTypeDto);
    Task<ExamTypeDto> UpdateExamTypeAsync(Guid id, UpdateExamTypeDto updateExamTypeDto);
    Task<bool> DeleteExamTypeAsync(Guid id);
    Task<List<ExamTypeDto>> GetExamTypeListAsync(Guid educationMediumId);
}
