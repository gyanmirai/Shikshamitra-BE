using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Repository interface for AssignmentType entity
/// </summary>
public interface IAssignmentTypeRepository : IRepository<AssignmentType, Guid>
{
	/// <summary>
	/// Finds an assignment type by name and academic medium ID
	/// </summary>
	/// <param name="name">Assignment type name</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>AssignmentType if found, null otherwise</returns>
	Task<AssignmentType?> FindByNameAndAcademicMediumIdAsync(
		string name,
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Gets all assignment types for a specific academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of assignment types ordered by name</returns>
	Task<List<AssignmentType>> GetListByAcademicMediumIdAsync(
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	);
}
