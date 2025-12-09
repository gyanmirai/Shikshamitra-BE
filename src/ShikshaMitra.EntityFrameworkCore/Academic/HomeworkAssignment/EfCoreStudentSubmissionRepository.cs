using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class EfCoreStudentSubmissionRepository
	: EfCoreRepository<ShikshaMitraDbContext, StudentSubmissionEntity, Guid>,
		IStudentSubmissionRepository
{
	public EfCoreStudentSubmissionRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
	)
		: base(dbContextProvider) { }

	public async Task<List<StudentSubmissionEntity>> GetSubmissionsWithDetailsAsync(
		Guid homeworkId,
		Guid divisionId
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(s => s.HomeworkAssignmentId == homeworkId && s.DivisionId == divisionId)
			.ToListAsync();
	}

	public async Task<StudentSubmissionEntity?> GetSubmissionByStudentAndHomeworkAsync(
		Guid studentId,
		Guid homeworkId
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.FirstOrDefaultAsync(s =>
				s.StudentId == studentId && s.HomeworkAssignmentId == homeworkId
			);
	}

	public async Task<List<StudentSubmissionEntity>> GetSubmissionsByHomeworkAsync(
		Guid homeworkId
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(s => s.HomeworkAssignmentId == homeworkId)
			.ToListAsync();
	}

	public async Task<StudentSubmissionEntity?> GetSubmissionByStudentAndAssignmentAsync(
		Guid studentId,
		Guid assignmentId
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.FirstOrDefaultAsync(s =>
				s.StudentId == studentId && s.AssignmentId == assignmentId
			);
	}

	public async Task<List<StudentSubmissionEntity>> GetSubmissionsByAssignmentAndDivisionAsync(
		Guid assignmentId,
		Guid divisionId
	)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(s => s.AssignmentId == assignmentId && s.DivisionId == divisionId)
			.ToListAsync();
	}
}
