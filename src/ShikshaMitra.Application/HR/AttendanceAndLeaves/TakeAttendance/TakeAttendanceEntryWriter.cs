using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceEntryWriter(
	IRepository<Employee, Guid> employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	TakeAttendanceAccessService accessService,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ApplicationService, ITransientDependency
{
	private const int AttendanceRemarksMaxLength = 512;

	public async Task SaveEntryAsync(
		ParsedTakeAttendanceEntry parsedEntry,
		DateOnly dateOnly,
		UserScopeContext scope,
		IReadOnlyDictionary<Guid, Guid?> employeeStageMap)
	{
		var entry = parsedEntry.Entry;
		var status = parsedEntry.Status;
		var normalizedRemarks = TakeAttendanceSaveEntryParser.TruncateForStorage(entry.Remarks, AttendanceRemarksMaxLength);
		var tIn = TakeAttendanceSaveEntryParser.TryParseTime(entry.InTime);
		var tOut = TakeAttendanceSaveEntryParser.TryParseTime(entry.OutTime);

		var employee = await employeeRepository.GetAsync(entry.EmployeeId);
		EnsureEmployeeCanTakeAttendanceOnDate(employee, dateOnly);
		if (tIn.HasValue || tOut.HasValue)
		{
			var evaluation = await workedTimeEvaluator.EvaluateAsync(
				employee,
				dateOnly,
				tIn,
				tOut,
				status);
			status = evaluation.Status;
		}

		var attQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(
			attQuery.Where(a =>
				a.EmployeeId == entry.EmployeeId &&
				a.AttendanceDate == dateOnly));

		employeeStageMap.TryGetValue(entry.EmployeeId, out var assignmentStageId);
		var resolvedStageId = assignmentStageId ?? existing?.EducationMediumStageId;
		resolvedStageId = TakeAttendanceAccessService.ResolveScopedStageFallback(scope, resolvedStageId);

		await accessService.EnsureEmployeeScopeAllowedAsync(scope, resolvedStageId);

		if (existing != null)
		{
			existing.StatusType = status;
			existing.TimeIn = tIn;
			existing.TimeOut = tOut;
			existing.EducationMediumStageId = resolvedStageId;
			existing.Remarks = normalizedRemarks;
			await employeeAttendanceRepository.UpdateAsync(existing, autoSave: false);
			return;
		}

		await employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
		{
			TenantId = CurrentTenant.Id,
			EmployeeId = entry.EmployeeId,
			AttendanceDate = dateOnly,
			EducationMediumStageId = resolvedStageId,
			StatusType = status,
			TimeIn = tIn,
			TimeOut = tOut,
			Remarks = normalizedRemarks
		}, autoSave: false);
	}

	private static void EnsureEmployeeCanTakeAttendanceOnDate(
		Employee employee,
		DateOnly attendanceDate)
	{
		var effectiveStartDate = employee.DateOfJoining
			?? DateOnly.FromDateTime(employee.CreationTime.Date);

		if (effectiveStartDate <= attendanceDate && IsEmployeeAttendanceEligibleOnDate(employee, attendanceDate))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceEmployeeNotActiveOnDate)
			.WithData("employeeId", employee.Id)
			.WithData("attendanceDate", attendanceDate)
			.WithData("effectiveStartDate", effectiveStartDate);
	}

	private static bool IsEmployeeAttendanceEligibleOnDate(Employee employee, DateOnly attendanceDate)
	{
		return EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
			employee.EmployeeStatusType,
			employee.StatusEffectiveDate,
			attendanceDate);
	}
}
