using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectAppService
{
	Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto);
	Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto);
	Task DeleteAsync(Guid id);
	Task<List<SubjectDto>> GetListAsync(Guid mediumId);
}
