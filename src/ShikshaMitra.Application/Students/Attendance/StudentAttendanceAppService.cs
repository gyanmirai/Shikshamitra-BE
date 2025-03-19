using ShikshaMitra.Students.StudentRecords;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(true)]
public class StudentAttendanceAppService(IStudentAttendanceRepository studentAttendanceRepository, IStudentRepository studentRepository, IGuidGenerator guidGenerator)
	: CrudAppService<StudentAttendance, StudentAttendanceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentAttendanceDto>(studentAttendanceRepository),
	IStudentAttendanceAppService
{
	public async Task<bool> FillAttendance(List<CreateUpdateStudentAttendanceDto> inputs, DateTimeOffset attendanceDate)
	{
		List<StudentAttendance>? studentAttendances = inputs.ConvertAll(item => new StudentAttendance(
			guidGenerator.Create(),
			tenantId: (Guid) CurrentTenant.Id!,
			studentId: item.StudentId,
			attendanceDate,
			item.IsPresent,
			item.Remarks
		));

		await studentAttendanceRepository.InsertManyAsync(studentAttendances);

		return true;
	}

	public async Task<PagedResultDto<StudentAttendanceDto>> GetFilteredAttendenceListAsync(PagedAndSortedResultRequestDto input, DateTimeOffset? attendanceDate, Guid? educationMediumStageId, Guid? standardId, Guid? divisionId)
	{
		// Create the base query from the repository
		IQueryable<StudentAttendance>? query = await Repository.GetQueryableAsync();

		// Apply filters
		// Filter by attendance date if provided
		if (attendanceDate.HasValue)
		{
			// Compare only date part
			var startDate = attendanceDate.Value.Date;
			var endDate = startDate.AddDays(1);
			query = query.Where(a => a.AttendanceDate >= startDate && a.AttendanceDate < endDate);
		}

		// For the education medium, standard, and division filters, we'll need to join with the Student table
		if (educationMediumStageId.HasValue || standardId.HasValue || divisionId.HasValue)
		{
			// Assuming we have a DbContext available
			var dbContext = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
			var studentQuery = await dbContext.GetQueryableAsync();

			if (educationMediumStageId.HasValue)
			{
				studentQuery = studentQuery.Where(s => s.EducationMediumStageId == educationMediumStageId.Value);
			}

			if (standardId.HasValue)
			{
				studentQuery = studentQuery.Where(s => s.StandardId == standardId.Value);
			}

			if (divisionId.HasValue)
			{
				studentQuery = studentQuery.Where(s => s.DivisionId == divisionId.Value);
			}

			// Get the filtered student IDs
			var filteredStudentIds = await AsyncExecuter.ToListAsync(studentQuery.Select(s => s.Id));

			// Apply the student filter to the attendance query
			query = query.Where(a => filteredStudentIds.Contains(a.StudentId));
		}

		// Get the total count AFTER applying filters (moved this down)
		var totalCount = await AsyncExecuter.CountAsync(query);

		// Apply sorting and paging
		query = ApplySorting(query, input);
		query = ApplyPaging(query, input);

		// Execute the query to get the attendance records
		var entities = await AsyncExecuter.ToListAsync(query);

		// Map the entities to DTOs
		var entityDtos = await MapToGetListOutputDtosAsync(entities);

		return new PagedResultDto<StudentAttendanceDto>(totalCount, entityDtos);
	}
}
