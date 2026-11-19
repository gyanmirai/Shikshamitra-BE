using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.WorkShifts;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.WorkShifts;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/work-shifts")]
public class EmployeeWorkShiftController(IEmployeeWorkShiftAppService service) : AbpController
{
	[HttpGet] public Task<PagedResultDto<EmployeeWorkShiftDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input) => service.GetListAsync(input);
	[HttpGet("lookup")] public Task<WorkShiftOptionsDto> LookupAsync() => service.LookupAsync();
	[HttpPost] public Task<EmployeeWorkShiftDto> CreateAsync(SaveWorkShiftInput input) => service.CreateAsync(input);
	[HttpPut("{id:guid}")] public Task<EmployeeWorkShiftDto> UpdateAsync(Guid id, SaveWorkShiftInput input) => service.UpdateAsync(id, input);
	[HttpDelete("{id:guid}")] public Task DeleteAsync(Guid id, [FromQuery] string concurrencyStamp) => service.DeleteAsync(id, concurrencyStamp);
}
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/employees/{employeeId:guid}/shift-assignments")]
public class EmployeeShiftAssignmentController(IEmployeeShiftAssignmentAppService service) : AbpController
{
	[HttpGet] public Task<EmployeeWorkScheduleDto> GetAsync(Guid employeeId) => service.GetAsync(employeeId);
	[HttpPost] public Task<EmployeeWorkScheduleDto> ScheduleAsync(Guid employeeId, ScheduleEmployeeShiftInput input) => service.ScheduleAsync(employeeId, input);
	[HttpDelete("{assignmentId:guid}")] public Task<EmployeeWorkScheduleDto> CancelAsync(Guid employeeId, Guid assignmentId, [FromQuery] string concurrencyStamp) => service.CancelAsync(employeeId, assignmentId, concurrencyStamp);
}