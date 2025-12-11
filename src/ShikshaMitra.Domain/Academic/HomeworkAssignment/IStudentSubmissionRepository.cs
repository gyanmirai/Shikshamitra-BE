using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IStudentSubmissionRepository : IRepository<StudentSubmissionEntity, Guid>
{
	/// <summary>
	/// Gets all submissions for a specific homework and division with full details
	/// </summary>
	Task<List<StudentSubmissionEntity>> GetSubmissionsWithDetailsAsync(
		Guid homeworkId,
		Guid divisionId
	);

	/// <summary>
	/// Gets a submission by student and homework
	/// </summary>
	Task<StudentSubmissionEntity?> GetSubmissionByStudentAndHomeworkAsync(
		Guid studentId,
		Guid homeworkId
	);

	/// <summary>
	/// Gets all submissions for a specific homework across all divisions
	/// </summary>
	Task<List<StudentSubmissionEntity>> GetSubmissionsByHomeworkAsync(Guid homeworkId);

	/// <summary>
	/// Gets a submission by student and assignment
	/// </summary>
	Task<StudentSubmissionEntity?> GetSubmissionByStudentAndAssignmentAsync(
		Guid studentId,
		Guid assignmentId
	);

	/// <summary>
	/// Gets all submissions for a specific assignment and division
	/// </summary>
	Task<List<StudentSubmissionEntity>> GetSubmissionsByAssignmentAndDivisionAsync(
		Guid assignmentId,
		Guid divisionId
	);
}
