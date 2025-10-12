using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public interface IAssigmentTypeAppService : IApplicationService
{
    Task<AssigmentTypeDto> CreateAssigmentTypeAsync(CreateAssigmentTypeDto createAssigmentTypeDto);
    Task<List<AssigmentTypeDto>> GetListAsync(Guid? academicMediumId);
    Task<AssigmentTypeDto> UpdateAsync(Guid id, UpdateAssigmentTypeDto updateAssigmentTypeDto);
    Task<bool> DeleteAsync(Guid id);
}
