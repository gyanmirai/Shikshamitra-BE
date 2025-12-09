using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for Assignment V2 API
/// </summary>
public interface IAssignmentV2AppService : IApplicationService
{
	/// <summary>
	/// Create a new assignment
	/// </summary>
	Task<AssignmentV2CreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateAssignmentV2Dto input
	);

	/// <summary>
	/// Get all assignments with optional filters
	/// </summary>
	Task<AssignmentV2ListResponseDto> GetAllAsync(
		Guid academicMediumId,
		GetAssignmentFilterDto filter
	);

	/// <summary>
	/// Get assignment by ID
	/// </summary>
	Task<AssignmentV2GetResponseDto> GetAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Update an existing assignment
	/// </summary>
	Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateAssignmentV2Dto input
	);

	/// <summary>
	/// Manually publish assignment for all divisions
	/// </summary>
	Task<AssignmentV2PublishResponseDto> PublishAsync(Guid id, Guid academicMediumId);

	/// <summary>
	/// Delete an assignment
	/// </summary>
	Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId);
}
