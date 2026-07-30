using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for managing homework types
/// </summary>
public interface IHomeworkTypeAppService : IApplicationService
{
	/// <summary>
	/// Creates a new homework type
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Homework type creation data</param>
	/// <returns>Created homework type wrapped in response</returns>
	Task<HomeworkTypeCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateHomeworkTypeDto input
	);

	/// <summary>
	/// Gets all homework types for a specific academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <returns>List of homework types</returns>
	Task<HomeworkTypeListResponseDto> GetAllAsync(Guid academicMediumId, bool includeArchived = false);

	/// <summary>
	/// Gets a single homework type by ID
	/// </summary>
	/// <param name="id">Homework type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <returns>Homework type details</returns>
	Task<HomeworkTypeGetResponseDto> GetAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Updates an existing homework type
	/// </summary>
	/// <param name="id">Homework type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <param name="input">Updated homework type data</param>
	/// <returns>Success message</returns>
	Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateHomeworkTypeDto input
	);

	/// <summary>
	/// Deletes a homework type
	/// </summary>
	/// <param name="id">Homework type ID</param>
	/// <param name="academicMediumId">Academic medium ID for validation</param>
	/// <returns>Success message</returns>
	Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId);

	Task<MessageResponseDto> ArchiveAsync(Guid id, Guid academicMediumId);

	Task<MessageResponseDto> RestoreAsync(Guid id, Guid academicMediumId);
}
