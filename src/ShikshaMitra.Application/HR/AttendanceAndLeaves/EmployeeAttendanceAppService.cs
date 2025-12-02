using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendanceAppService : CrudAppService<EmployeeAttendance, EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeAttendanceDto>, IEmployeeAttendanceAppService
{
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly ILogger<EmployeeAttendanceAppService> _logger;

	public EmployeeAttendanceAppService(
		IEmployeeAttendanceRepository employeeAttendanceRepository,
		IRepository<Employee, Guid> employeeRepository,
		ILogger<EmployeeAttendanceAppService> logger)
		: base(employeeAttendanceRepository)
	{
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_employeeRepository = employeeRepository;
		_logger = logger;
	}

	public override async Task<EmployeeAttendanceDto> CreateAsync(CreateUpdateEmployeeAttendanceDto input)
	{
		await ValidateAndNormalizeEducationMediumStageIdAsync(input);
		return await base.CreateAsync(input);
	}

	public override async Task<EmployeeAttendanceDto> UpdateAsync(Guid id, CreateUpdateEmployeeAttendanceDto input)
	{
		await ValidateAndNormalizeEducationMediumStageIdAsync(input);
		return await base.UpdateAsync(id, input);
	}

	/// <summary>
	/// Validates and normalizes the EducationMediumStageId to ensure consistency with the Employee.
	/// If EducationMediumStageId is not provided, it will be auto-populated from the Employee.
	/// If provided, it must match the Employee's EducationMediumStageId.
	/// </summary>
	private async Task ValidateAndNormalizeEducationMediumStageIdAsync(CreateUpdateEmployeeAttendanceDto input)
	{
		// Get the employee to validate against
		var employee = await _employeeRepository.FindAsync(input.EmployeeId);
		
		if (employee == null)
		{
			var correlationId = Guid.NewGuid();
			_logger.LogWarning(
				"Employee not found for attendance record. EmployeeId: {EmployeeId}, CorrelationId: {CorrelationId}",
				input.EmployeeId, correlationId);
			
			throw new UserFriendlyException(
				$"Employee with ID '{input.EmployeeId}' does not exist. Please verify the employee ID. (Ref: {correlationId})",
				code: "EmployeeAttendance:EmployeeNotFound"
			);
		}

		// Auto-populate EducationMediumStageId if not provided
		if (!input.EducationMediumStageId.HasValue)
		{
			input.EducationMediumStageId = employee.EducationMediumStageId;
			_logger.LogDebug(
				"Auto-populated EducationMediumStageId for attendance. EmployeeId: {EmployeeId}, EducationMediumStageId: {EducationMediumStageId}",
				input.EmployeeId, input.EducationMediumStageId);
			return;
		}

		// Validate that the provided EducationMediumStageId matches the Employee's value
		if (input.EducationMediumStageId.Value != employee.EducationMediumStageId)
		{
			var correlationId = Guid.NewGuid();
			_logger.LogWarning(
				"EducationMediumStageId mismatch detected. EmployeeId: {EmployeeId}, " +
				"Expected: {ExpectedStageId}, Provided: {ProvidedStageId}, CorrelationId: {CorrelationId}",
				input.EmployeeId, employee.EducationMediumStageId, input.EducationMediumStageId, correlationId);
			
			throw new BusinessException(
				code: "EmployeeAttendance:EducationMediumStageMismatch",
				message: $"The EducationMediumStageId provided ({input.EducationMediumStageId}) does not match " +
				         $"the employee's EducationMediumStageId ({employee.EducationMediumStageId}). " +
				         $"Attendance records must use the same EducationMediumStageId as the employee for proper scope filtering. " +
				         $"(Ref: {correlationId})"
			);
		}
	}
	public async Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDate(DateTimeOffset date)
	{
		List<EmployeeAttendance> employees =
			await _employeeAttendanceRepository.GetListAsync(e => e.CreationTime.Date == date.Date,
				includeDetails: true);

		return new PagedResultDto<EmployeeAttendanceDto>
		{
			TotalCount = employees.Count,
			Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(employees)
		};
	}

	public async Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummary(List<Guid> employeeIds,
		DateTimeOffset month)
	{
		// Calculate start and end of the month as DateTimeOffset for filtering based on TimeIn
		var startOfMonth = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, month.Offset);
		var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

		// Retrieve attendance records within the given time range
		List<EmployeeAttendance> employeeAttendances = await _employeeAttendanceRepository.GetListAsync(
			e => employeeIds.Contains(e.EmployeeId) && e.TimeIn >= startOfMonth && e.TimeIn <= endOfMonth,
			includeDetails: true);

		var summary = new List<EmployeeAttendanceSummaryDto>();

		foreach (var employeeId in employeeIds)
		{
			var employeeAttendance = employeeAttendances.Where(e => e.EmployeeId == employeeId).ToList();
			var totalWorkingDays = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.Present);
			var totalFullDayLeaves =
				employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.FullDayLeave);
			var totalHalfDayLeaves = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);

			var employee = employeeAttendance.Find(e => e.EmployeeId == employeeId)?.Employee;
			summary.Add(new EmployeeAttendanceSummaryDto
			{
				EmployeeId = employeeId,
				EmployeeName = employee?.EmployeeName ?? string.Empty,
				TotalWorkingDays = totalWorkingDays + totalFullDayLeaves + totalHalfDayLeaves,
				PresentDays = totalWorkingDays,
				LeaveDays = totalFullDayLeaves,
				HalfDayLeaves = totalHalfDayLeaves,
				EmployeeAttendances = new PagedResultDto<EmployeeAttendanceDto>
				{
					TotalCount = employeeAttendance.Count,
					Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(
						employeeAttendance)
				}
			});
		}

		return summary;
	}
}