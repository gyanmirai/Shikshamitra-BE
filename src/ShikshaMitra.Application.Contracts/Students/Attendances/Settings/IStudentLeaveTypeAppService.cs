using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// Application service interface for Student Leave Type operations
/// </summary>
public interface IStudentLeaveTypeAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of student leave types
	/// </summary>
	Task<PagedResultDto<StudentLeaveTypeDto>> GetListAsync(GetStudentLeaveTypeListInput input);

	/// <summary>
	/// Gets a student leave type by ID
	/// </summary>
	Task<StudentLeaveTypeDto> GetAsync(Guid id);

	/// <summary>
	/// Creates a new student leave type
	/// </summary>
	Task<StudentLeaveTypeDto> CreateAsync(CreateUpdateStudentLeaveTypeDto input);

	/// <summary>
	/// Updates an existing student leave type
	/// </summary>
	Task<StudentLeaveTypeDto> UpdateAsync(Guid id, CreateUpdateStudentLeaveTypeDto input);

	/// <summary>
	/// Deletes a student leave type
	/// </summary>
	Task DeleteAsync(Guid id);

	/// <summary>
	/// Gets all active student leave types (for dropdowns)
	/// </summary>
	Task<ListResultDto<StudentLeaveTypeDto>> GetActiveListAsync();
}
