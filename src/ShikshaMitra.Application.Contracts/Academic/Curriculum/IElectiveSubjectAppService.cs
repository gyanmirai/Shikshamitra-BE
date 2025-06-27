using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for managing Elective Subjects.
/// </summary>
public interface IElectiveSubjectAppService : IApplicationService
{
	Task<ElectiveSubjectDto> GetAsync(Guid id);

	Task<List<ElectiveSubjectDto>> GetListAsync();

	Task<ElectiveSubjectDto> CreateAsync(CreateUpdateElectiveSubjectDto input);

	Task<ElectiveSubjectDto> UpdateAsync(Guid id, CreateUpdateElectiveSubjectDto input);

	Task DeleteAsync(Guid id);
}
