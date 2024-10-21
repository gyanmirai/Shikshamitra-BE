using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendanceDto : EntityDto<Guid>
{
	public Guid EmployeeId { get; set; }
	
	public string EmployeeName { get; set; }

	public DateOnly AttendanceDate { get; set; }

	public TimeOnly? TimeIn { get; set; }

	public TimeOnly? TimeOut { get; set; }

	public EmployeeAttendanceStatus Status { get; set; }
}