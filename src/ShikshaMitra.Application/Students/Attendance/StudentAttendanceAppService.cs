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
public class StudentAttendanceAppService(
	IStudentAttendanceRepository studentAttendanceRepository, 
	IStudentRepository studentRepository, 
	IGuidGenerator guidGenerator)
	: CrudAppService<StudentAttendance, StudentAttendanceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentAttendanceDto>(studentAttendanceRepository),
	IStudentAttendanceAppService
{
	public async Task<bool> FillAttendance(List<CreateUpdateStudentAttendanceDto> inputs, DateTimeOffset attendanceDate)
	{
		// Normalize the attendance date to remove time component
		var normalizedDate = attendanceDate.Date;

		// Get all student IDs from inputs
		var studentIds = inputs.Select(i => i.StudentId).ToList();

		// Find existing attendance records for these students on the given date
		var query = await studentAttendanceRepository.GetQueryableAsync();
		var existingRecords = await AsyncExecuter.ToListAsync(
			query.Where(a =>
				studentIds.Contains(a.StudentId) &&
				a.AttendanceDate.Date == normalizedDate)
		);

		// Create dictionary for quick lookup
		var existingRecordsDictionary = existingRecords.ToDictionary(r => r.StudentId);

		// Prepare lists for updates and inserts
		var recordsToUpdate = new List<StudentAttendance>();
		var recordsToInsert = new List<StudentAttendance>();

		// Process each input
		foreach (var input in inputs)
		{
			if (existingRecordsDictionary.TryGetValue(input.StudentId, out var existingRecord))
			{
				// Update existing record
				existingRecord.IsPresent = input.IsPresent;
				existingRecord.Remarks = input.Remarks;
				recordsToUpdate.Add(existingRecord);
			}
			else
			{
				// Create new record
				var newRecord = new StudentAttendance(
					guidGenerator.Create(),
					tenantId: (Guid) CurrentTenant.Id!,
					studentId: input.StudentId,
					normalizedDate,
					input.IsPresent,
					input.Remarks
				);
				recordsToInsert.Add(newRecord);
			}
		}

		// Perform updates and inserts
		if (recordsToUpdate.Any())
		{
			await studentAttendanceRepository.UpdateManyAsync(recordsToUpdate);
		}

		if (recordsToInsert.Any())
		{
			await studentAttendanceRepository.InsertManyAsync(recordsToInsert);
		}

		return true;
	}

	public async Task<PagedResultDto<StudentAttendanceDto>> GetFilteredAttendenceListAsync(PagedAndSortedResultRequestDto input, DateTimeOffset? attendanceDate, Guid? educationMediumStageId, Guid? standardId, Guid? divisionId)
	{
		// Start with students instead of attendance records
		var studentRepository = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
		IQueryable<Student>? studentQuery = await studentRepository.GetQueryableAsync();

		// Apply student-specific filters
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

		// Get total count of filtered students
		var totalCount = await AsyncExecuter.CountAsync(studentQuery);

		// Get the filtered student list with eager loading of needed properties
		var students = await AsyncExecuter.ToListAsync(studentQuery);
		var studentIds = students.Select(s => s.Id).ToList();

		// Map students to StudentDto objects for later use
		var studentDtos = ObjectMapper.Map<List<Student>, List<StudentDto>>(students);
		var studentDtoDict = studentDtos.ToDictionary(s => s.Id);

		// Default attendance date to today if not specified
		var effectiveDate = attendanceDate ?? DateTimeOffset.UtcNow.Date;

		// Get attendance data for the specific date if students exist
		var attendanceRecords = new Dictionary<Guid, StudentAttendance>();
		if (studentIds.Any())
		{
			var attendanceQuery = await Repository.GetQueryableAsync();
			var startDate = effectiveDate.Date;
			var endDate = startDate.AddDays(1);

			attendanceQuery = attendanceQuery.Where(a =>
				studentIds.Contains(a.StudentId) &&
				a.AttendanceDate >= startDate &&
				a.AttendanceDate < endDate);

			var existingAttendance = await AsyncExecuter.ToListAsync(attendanceQuery);
			attendanceRecords = existingAttendance.ToDictionary(a => a.StudentId);
		}

		// Get the first day of the month
		var firstDayOfMonth = new DateTimeOffset(effectiveDate.Year, effectiveDate.Month, 1, 0, 0, 0, effectiveDate.Offset);

		// Get all attendance records for this month up to the effective date
		var monthlyAttendanceQuery = await Repository.GetQueryableAsync();
		monthlyAttendanceQuery = monthlyAttendanceQuery.Where(a =>
			studentIds.Contains(a.StudentId) &&
			a.AttendanceDate >= firstDayOfMonth &&
			a.AttendanceDate <= effectiveDate);

		var monthlyAttendance = await AsyncExecuter.ToListAsync(monthlyAttendanceQuery);

		// Find all distinct dates when attendance was recorded
		var attendanceDates = monthlyAttendance
			.Select(a => a.AttendanceDate.Date)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		// The total number of days attendance has been taken is the count of distinct dates
		int totalDaysWithAttendance = attendanceDates.Count;

		// If no attendance has been recorded yet, use the day of month of the effective date
		if (totalDaysWithAttendance == 0)
		{
			totalDaysWithAttendance = effectiveDate.Day;
		}

		// Calculate present days per student
		var presentDaysByStudent = monthlyAttendance
			.Where(a => a.IsPresent)
			.GroupBy(a => a.StudentId)
			.ToDictionary(g => g.Key, g => g.Count());

		var resultAttendances = new List<StudentAttendanceDto>();
		foreach (var student in students)
		{
			StudentAttendanceDto attendanceDto;

			if (attendanceRecords.TryGetValue(student.Id, out var attendance))
			{
				// Use existing attendance record
				attendanceDto = ObjectMapper.Map<StudentAttendance, StudentAttendanceDto>(attendance);
			}
			else
			{
				// Create placeholder attendance record with isPresent = true (default to present)
				attendanceDto = new StudentAttendanceDto
				{
					Id = Guid.Empty,
					StudentId = student.Id,
					AttendanceDate = effectiveDate,
					IsPresent = true, // Default to present when attendance hasn't been filled
					Remarks = null
				};
			}

			// Add attendance statistics
			attendanceDto.TotalDays = totalDaysWithAttendance;
			attendanceDto.PresentDays = presentDaysByStudent.GetValueOrDefault(student.Id, 0);

			// Add the full student information
			attendanceDto.Student = studentDtoDict[student.Id];

			resultAttendances.Add(attendanceDto);
		}

		// Apply paging after preparing all data
		var pagedResults = resultAttendances
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		return new PagedResultDto<StudentAttendanceDto>(totalCount, pagedResults);
	}
}
