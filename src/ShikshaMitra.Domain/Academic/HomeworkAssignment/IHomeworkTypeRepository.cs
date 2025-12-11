using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Repository interface for HomeworkType entity
/// </summary>
public interface IHomeworkTypeRepository : IRepository<HomeworkType, Guid>
{
	/// <summary>
	/// Finds a homework type by name and academic medium ID
	/// </summary>
	/// <param name="name">Homework type name</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HomeworkType if found, null otherwise</returns>
	Task<HomeworkType?> FindByNameAndAcademicMediumIdAsync(
		string name,
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Gets all homework types for a specific academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of homework types ordered by name</returns>
	Task<List<HomeworkType>> GetListByAcademicMediumIdAsync(
		Guid academicMediumId,
		CancellationToken cancellationToken = default
	);
}
