using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectAppService
{
	Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto, Guid academicMediumId);
	Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto, Guid academicMediumId);
	Task DeleteAsync(Guid id);
	Task<List<SubjectDto>> GetListAsync(Guid mediumId);
}
