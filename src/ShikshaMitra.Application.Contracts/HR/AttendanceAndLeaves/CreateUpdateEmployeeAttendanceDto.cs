using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

public class CreateUpdateEmployeeAttendanceDto 
{
	[Required] public Guid EmployeeId { get; set; }

	[Required] public DateOnly AttendanceDate { get; set; }

	public TimeOnly? TimeIn { get; set; }

	public TimeOnly? TimeOut { get; set; }

	public EmployeeAttendanceStatus Status { get; set; }
}