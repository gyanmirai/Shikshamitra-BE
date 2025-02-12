using ShikshaMitra.Students.StudentRecords;
using System;
using System.Collections.Generic;
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
			studentId: item.StudentId,
			attendanceDate,
			item.IsPresent
		));

		await studentAttendanceRepository.InsertManyAsync(studentAttendances);

		return true;
	}

}
