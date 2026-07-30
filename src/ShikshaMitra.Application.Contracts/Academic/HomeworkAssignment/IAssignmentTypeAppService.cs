using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for managing assignment types
/// </summary>
public interface IAssignmentTypeAppService : IApplicationService
{
	/// <summary>
	/// Creates a new assignment type
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Assignment type creation data</param>
	/// <returns>Created assignment type wrapped in response</returns>
	Task<AssignmentTypeCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateAssignmentTypeDto input
	);

	/// <summary>
	/// Gets all assignment types for a specific academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <returns>List of assignment types</returns>
	Task<AssignmentTypeListResponseDto> GetAllAsync(Guid academicMediumId, bool includeArchived = false);

	/// <summary>
	/// Gets a single assignment type by ID
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <returns>Assignment type details</returns>
	Task<AssignmentTypeGetResponseDto> GetAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Updates an existing assignment type
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <param name="input">Updated assignment type data</param>
	/// <returns>Success message</returns>
	Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateAssignmentTypeDto input
	);

	/// <summary>
	/// Deletes an assignment type
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <returns>Success message</returns>
	Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId);

	Task<MessageResponseDto> ArchiveAsync(Guid id, Guid academicMediumId);

	Task<MessageResponseDto> RestoreAsync(Guid id, Guid academicMediumId);
}
