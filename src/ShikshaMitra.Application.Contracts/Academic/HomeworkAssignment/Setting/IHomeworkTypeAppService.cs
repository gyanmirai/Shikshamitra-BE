using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public interface IHomeworkTypeAppService : IApplicationService
{
	Task<HomeWorkTypeDto> CreateHomeWorkTypeAsync(CreateHomeworkTypeDto createHomeworkTypeDto);
	Task<List<HomeWorkTypeDto>> GetListAsync(Guid? academicMediumId);
	Task<HomeWorkTypeDto> UpdateAsync(Guid id, UpdateHomeworkTypeDto updateHomeworkTypeDto);
	Task<bool> DeleteAsync(Guid id);
}
