using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for managing Elective Subjects.
/// </summary>
public interface IElectiveGroupAppService : IApplicationService
{
	Task<ElectiveGroupDto> GetAsync(Guid id);

	Task<List<ElectiveGroupDto>> GetListAsync();

	Task<ElectiveGroupDto> CreateAsync(CreateUpdateElectiveGroupDto input);

	Task<ElectiveGroupDto> UpdateAsync(Guid id, CreateUpdateElectiveGroupDto input);

	Task DeleteAsync(Guid id);
}
