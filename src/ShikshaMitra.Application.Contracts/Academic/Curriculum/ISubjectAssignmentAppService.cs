using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for Subject Assignment operations
/// </summary>
public interface ISubjectAssignmentAppService : IApplicationService
{
	/// <summary>
	/// Creates a new subject assignment group
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (from query parameter)</param>
	/// <param name="input">Create input DTO</param>
	/// <returns>Created assignment group</returns>
	Task<SubjectAssignmentDto> CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentGroupDto input
	);

	/// <summary>
	/// Gets all subject assignment groups for a standard with optional filters
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="classId">Standard/Class ID</param>
	/// <param name="status">Optional status filter (0=Draft, 1=Published)</param>
	/// <returns>List of assignment groups</returns>
	Task<List<SubjectAssignmentDto>> GetListAsync(
		Guid academicMediumId,
		Guid classId
	);

	/// <summary>
	/// Gets a single assignment group by ID
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Assignment group details</returns>
	Task<SubjectAssignmentDto> GetAsync(Guid assignmentId);

	/// <summary>
	/// Updates an existing subject assignment group
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Update input DTO</param>
	/// <returns>Updated assignment group</returns>
	Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentGroupDto input
	);

	/// <summary>
	/// Publishes a draft assignment (changes status to Published)
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Publish response with new status</returns>
	Task<PublishResponseDto> PublishAsync(List<Guid> assignmentId);

	/// <summary>
	/// Deletes an assignment group and all its subjects
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	Task DeleteAsync(Guid assignmentId);
}
