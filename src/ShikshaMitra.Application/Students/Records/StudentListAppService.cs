using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>
/// Handles student list-level operations: status management and status-history queries.
/// </summary>
/// <remarks>
/// Follows ABP DDD conventions:
/// <list type="bullet">
///   <item>All domain invariants enforced inside the <see cref="Student"/> aggregate.</item>
///   <item>The application service is responsible for loading, orchestrating, persisting and mapping.</item>
///   <item>Status-history entries are owned by the aggregate and are persisted via EF Core cascade.</item>
/// </list>
/// </remarks>
[RemoteService(false)]
public class StudentListAppService(
	IRepository<Student, Guid> studentRepository
) : ApplicationService, IStudentListAppService
{
	public async Task<PagedResultDto<StudentDto>> GetStudentsByDivisionAsync(Guid divisionId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null)
	{
		var queryable = await studentRepository.GetQueryableAsync();

		var studentsQuery = queryable
			.Where(s => s.DivisionId == divisionId && s.Status == StudentLifeCycleStatusEnum.Enrolled);

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			studentsQuery = studentsQuery.Where(s => s.FirstName.Contains(searchTerm) || s.LastName.Contains(searchTerm));
		}

		var totalCount = await AsyncExecuter.CountAsync(studentsQuery);

		var students = await AsyncExecuter.ToListAsync(
			studentsQuery
				.OrderByDescending(s => s.CreationTime)
				.PageBy(skipCount, maxResultCount)
		);

		return new PagedResultDto<StudentDto>(
			totalCount,
			ObjectMapper.Map<List<Student>, List<StudentDto>>(students)
		);
	}

	/// <inheritdoc/>
	public async Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);

		try
		{
			student.ChangeOperationalStatus(input.Status, input.Remarks);
		}
		catch (InvalidOperationException ex)
		{
			// Translate domain exceptions to user-friendly ABP exceptions.
			throw new UserFriendlyException(ex.Message);
		}

		await studentRepository.UpdateAsync(student);
	}

	/// <inheritdoc/>
	public async Task<List<StudentStatusHistoryDto>> GetStatusHistoryAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);

		return student.StatusHistories
			.OrderByDescending(h => h.CreationTime)
			.Select(MapToDto)
			.ToList();
	}

	private static StudentStatusHistoryDto MapToDto(StudentStatusHistory h) => new()
	{
		Id               = h.Id,
		StudentId        = h.StudentId,
		FromStatus       = h.FromStatus,
		ToStatus         = h.ToStatus,
		OperationalStatus = h.ToStatus switch
		{
			StudentLifeCycleStatusEnum.Enrolled => StudentStatus.Active,
			StudentLifeCycleStatusEnum.InActive => StudentStatus.Inactive,
			_                                   => StudentStatus.Left,
		},
		Remarks          = h.Remarks,
		CreationTime     = h.CreationTime,
		CreatorId        = h.CreatorId,
	};
}
