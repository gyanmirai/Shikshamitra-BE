using ShikshaMitra.Academic.Curriculum.Setting;
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
	Task<ElectiveGroupDto> GetelectivegroupAsync(Guid id);

	Task<List<ElectiveGroupDto>> GetListelectivegroupAsync(Guid academicMediumId);

	Task<ElectiveGroupDto> CreateelectivegroupAsync(CreateElectiveGroupDto createElectiveGroupDto);

	Task<ElectiveGroupDto> UpdateelectivegroupAsync(Guid id, UpdateElectiveGroupDto updateElectiveGroupDto);

	Task<bool> DeleteelectivegroupAsync(Guid id);
}