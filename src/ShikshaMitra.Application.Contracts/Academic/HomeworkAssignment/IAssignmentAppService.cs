using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for Assignment API
/// </summary>
public interface IAssignmentAppService : IApplicationService
{
	/// <summary>
	/// Create a new assignment
	/// </summary>
	Task<AssignmentCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateAssignmentDto input
	);

	/// <summary>
	/// Get all assignments with optional filters
	/// </summary>
	Task<AssignmentListResponseDto> GetAllAsync(
		Guid academicMediumId,
		GetAssignmentFilterDto filter
	);

	/// <summary>
	/// Get assignment by ID
	/// </summary>
	Task<AssignmentGetResponseDto> GetAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Update an existing assignment
	/// </summary>
	Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateAssignmentDto input
	);

	/// <summary>
	/// Manually publish assignment for all divisions
	/// </summary>
	Task<AssignmentPublishResponseDto> PublishAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Delete an assignment
	/// </summary>
	Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId);
}
