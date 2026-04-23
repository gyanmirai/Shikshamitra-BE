using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class TakeAttendanceAppService(
	IRepository<Employee, Guid> employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IUserScopeService userScopeService,
	IRepository<ShiftManagement, Guid> shiftRepository)
	: ApplicationService, ITakeAttendanceAppService
{
	private const int AttendanceRemarksMaxLength = 512;

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_TakeAttendance.View)]
	public async Task<TakeAttendanceDayResponse> GetDayAsync(GetTakeAttendanceDayInput input)
	{
		var date = ParseDate(input.AttendanceDate);
		var dateOnly = DateOnly.FromDateTime(date);
		if (date.Date > Clock.Now.Date)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceFutureDateNotAllowed);
		}

		var scope = await EnsureCanOperateAttendanceAsync();
		var locked = await staffAttendanceDayLockRepository.FindAsync(dateOnly);

		var empQuery = await employeeRepository.GetQueryableAsync();
		empQuery = empQuery.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active);

		if (CurrentTenant.Id.HasValue)
		{
			empQuery = empQuery.Where(e => e.TenantId == CurrentTenant.Id);
		}

		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var d = input.Department.Trim();
			empQuery = empQuery.Where(e => e.Department != null && e.Department.Contains(d));
		}

		if (input.IsTeaching.HasValue)
		{
			empQuery = input.IsTeaching.Value
				? empQuery.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: empQuery.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			empQuery = empQuery.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var s = input.Search.Trim().ToLowerInvariant();
			empQuery = empQuery.Where(e =>
				((e.FirstName ?? "") + " " + (e.MiddleName ?? "") + " " + (e.LastName ?? "")).ToLower().Contains(s) ||
				(e.EmployeeCode ?? "").ToLower().Contains(s) ||
				(e.MobileNo ?? "").ToLower().Contains(s));
		}

		empQuery = empQuery.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

		var allEmployeeIds = await AsyncExecuter.ToListAsync(empQuery.Select(e => e.Id));
		var employeeStageMap = await ResolveEmployeeStagesAsync(allEmployeeIds);
		var scopedEmployeeIds = FilterEmployeeIdsByScope(allEmployeeIds, employeeStageMap, scope);
		empQuery = empQuery.Where(e => scopedEmployeeIds.Contains(e.Id));

		var attBaseQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attBaseQuery = attBaseQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var attForSummary = await AsyncExecuter.ToListAsync(
			attBaseQuery.Where(a =>
				scopedEmployeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate == dateOnly));

		var employees = await AsyncExecuter.ToListAsync(
			empQuery.Skip(input.SkipCount).Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var ids = employees.Select(e => e.Id).ToList();
		var attRows = attForSummary.Where(a => ids.Contains(a.EmployeeId)).ToList();
		var attByEmp = attRows.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.First());
		var shiftIds = employees.Where(e => e.ShiftId.HasValue).Select(e => e.ShiftId!.Value).Distinct().ToList();
		var shifts = await LoadShiftLookupAsync(shiftIds);

		var items = new List<TakeAttendanceEmployeeRowDto>();
		foreach (var e in employees)
		{
			attByEmp.TryGetValue(e.Id, out var a);
			shifts.TryGetValue(e.ShiftId ?? Guid.Empty, out var shift);
			var status = a == null
				? TakeAttendanceStatus.NotMarked
				: MapDomainStatus(a.StatusType);
			items.Add(new TakeAttendanceEmployeeRowDto
			{
				EmployeeId = e.Id,
				FullName = FormatName(e.FirstName, e.MiddleName, e.LastName),
				EmployeeCode = e.EmployeeCode ?? string.Empty,
				Department = e.Department,
				DateIso = new DateTimeOffset(date.Date, TimeSpan.Zero).ToString("o"),
				InTime = a?.TimeIn?.ToString("o"),
				OutTime = a?.TimeOut?.ToString("o"),
				ShiftName = shift?.ShiftName,
				ShiftStartTime = shift?.StartTime.ToString("HH:mm:ss"),
				ShiftEndTime = shift?.EndTime.ToString("HH:mm:ss"),
				Remarks = a?.Remarks,
				Warnings = [],
				Errors = [],
				Status = status
			});
		}

		var summary = new TakeAttendanceSummaryDto
		{
			Present = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.Present),
			HalfDay = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			Absent = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			NotMarked = scopedEmployeeIds.Count - attForSummary.Count
		};

		return new TakeAttendanceDayResponse
		{
			TotalCount = scopedEmployeeIds.Count,
			Date = dateOnly,
			IsDayLocked = locked != null,
			LockedByUserId = locked?.LockedByUserId,
			LockedAt = locked?.LockedAt,
			Summary = summary,
			Items = items
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_TakeAttendance.Add)]
	public async Task<TakeAttendanceSaveOutput> SaveDayAsync(TakeAttendanceSaveInput input)
	{
		var date = ParseDate(input.AttendanceDate);
		var dateOnly = DateOnly.FromDateTime(date);
		if (date.Date > Clock.Now.Date)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceFutureDateNotAllowed);
		}

		var scope = await EnsureCanOperateAttendanceAsync();
		if (await staffAttendanceDayLockRepository.FindAsync(dateOnly) != null)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceDayLocked);
		}

		if (input.Entries == null || input.Entries.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var requestedEmployeeIds = input.Entries.Select(x => x.EmployeeId).Distinct().ToList();
		var employeeStageMap = await ResolveEmployeeStagesAsync(requestedEmployeeIds);
		var upserted = 0;
		foreach (var entry in input.Entries)
		{
			var status = ParseWireStatus(entry.StatusWire);
			var normalizedRemarks = TruncateForStorage(entry.Remarks, AttendanceRemarksMaxLength);
			DateTimeOffset? tIn = null;
			DateTimeOffset? tOut = null;
			if (!string.IsNullOrWhiteSpace(entry.InTime) &&
			    DateTimeOffset.TryParse(entry.InTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedIn))
			{
				tIn = parsedIn;
			}

			if (!string.IsNullOrWhiteSpace(entry.OutTime) &&
			    DateTimeOffset.TryParse(entry.OutTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOut))
			{
				tOut = parsedOut;
			}

			await employeeRepository.GetAsync(entry.EmployeeId);

			var attQuery = await employeeAttendanceRepository.GetQueryableAsync();
			if (CurrentTenant.Id.HasValue)
			{
				attQuery = attQuery.Where(a => a.TenantId == CurrentTenant.Id);
			}

			var existing = await AsyncExecuter.FirstOrDefaultAsync(
				attQuery.Where(a =>
					a.EmployeeId == entry.EmployeeId &&
					a.AttendanceDate == dateOnly));

			var resolvedStageId = existing?.EducationMediumStageId;
			if (!resolvedStageId.HasValue)
			{
				employeeStageMap.TryGetValue(entry.EmployeeId, out resolvedStageId);
			}
			resolvedStageId = ResolveScopedStageFallback(scope, resolvedStageId);

			EnsureEmployeeScopeAllowed(scope, resolvedStageId);

			if (existing != null)
			{
				existing.StatusType = status;
				existing.TimeIn = tIn;
				existing.TimeOut = tOut;
				existing.EducationMediumStageId ??= resolvedStageId;
				existing.Remarks = normalizedRemarks;
				await employeeAttendanceRepository.UpdateAsync(existing, autoSave: false);
			}
			else
			{
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

			upserted++;
		}

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		return new TakeAttendanceSaveOutput { UpsertedCount = upserted };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_TakeAttendance.Lock)]
	public async Task LockDayAsync(string attendanceDate)
	{
		var date = ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		var scope = await EnsureCanOperateAttendanceAsync();
		EnsureTenantWideDayLockAllowed(scope);
		if (await staffAttendanceDayLockRepository.FindAsync(dateOnly) != null)
		{
			return;
		}

		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var entity = new StaffAttendanceDayLock(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			dateOnly,
			userId,
			Clock.Now);
		await staffAttendanceDayLockRepository.InsertAsync(entity, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_TakeAttendance.Lock)]
	public async Task UnlockDayAsync(string attendanceDate)
	{
		var date = ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		var scope = await EnsureCanOperateAttendanceAsync();
		EnsureTenantWideDayLockAllowed(scope);
		var existing = await staffAttendanceDayLockRepository.FindAsync(dateOnly);
		if (existing != null)
		{
			await staffAttendanceDayLockRepository.DeleteAsync(existing, autoSave: true);
		}
	}

	private async Task<UserScopeContext> EnsureCanOperateAttendanceAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return scope;
		}

		if (scope.MediumStageIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}

	private static List<Guid> FilterEmployeeIdsByScope(
		List<Guid> employeeIds,
		Dictionary<Guid, Guid?> employeeStageMap,
		UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return employeeIds;
		}

		return employeeIds
			.Where(id =>
			{
				if (!employeeStageMap.TryGetValue(id, out var stageId) || !stageId.HasValue)
				{
					return scope.MediumStageIds.Count == 1;
				}

				return scope.MediumStageIds.Contains(stageId.Value);
			})
			.ToList();
	}

	private static TakeAttendanceStatus MapDomainStatus(EmployeeAttendanceStatusType s) =>
		s switch
		{
			EmployeeAttendanceStatusType.Present => TakeAttendanceStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => TakeAttendanceStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => TakeAttendanceStatus.Absent,
			_ => TakeAttendanceStatus.NotMarked
		};

	private void EnsureEmployeeScopeAllowed(UserScopeContext scope, Guid? employeeStageId)
	{
		if (scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return;
		}

		if (!employeeStageId.HasValue || !scope.MediumStageIds.Contains(employeeStageId.Value))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}
	}

	private static Guid? ResolveScopedStageFallback(UserScopeContext scope, Guid? employeeStageId)
	{
		if (employeeStageId.HasValue || scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return employeeStageId;
		}

		return scope.MediumStageIds.Count == 1 ? scope.MediumStageIds[0] : null;
	}

	private static void EnsureTenantWideDayLockAllowed(UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	private async Task<Dictionary<Guid, Guid?>> ResolveEmployeeStagesAsync(List<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, Guid?>();
		}

		var attQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attQuery = attQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var rows = await AsyncExecuter.ToListAsync(
			attQuery
				.Where(a => employeeIds.Contains(a.EmployeeId) && a.EducationMediumStageId.HasValue)
				.OrderByDescending(a => a.AttendanceDate)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId }));

		var result = new Dictionary<Guid, Guid?>();
		foreach (var row in rows)
		{
			if (!result.ContainsKey(row.EmployeeId))
			{
				result[row.EmployeeId] = row.EducationMediumStageId;
			}
		}

		foreach (var employeeId in employeeIds)
		{
			if (!result.ContainsKey(employeeId))
			{
				result[employeeId] = null;
			}
		}

		return result;
	}

	private async Task<Dictionary<Guid, ShiftManagement>> LoadShiftLookupAsync(List<Guid> shiftIds)
	{
		if (shiftIds.Count == 0)
		{
			return new Dictionary<Guid, ShiftManagement>();
		}

		var query = await shiftRepository.GetQueryableAsync();
		var shifts = await AsyncExecuter.ToListAsync(query.Where(x => shiftIds.Contains(x.Id)));
		return shifts.ToDictionary(x => x.Id, x => x);
	}

	private static EmployeeAttendanceStatusType ParseWireStatus(string wire) =>
		(wire ?? "").Trim().ToUpperInvariant() switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"HALF_DAY" or "HALFDAY" or "HALF_DAY_LEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			"ABSENT" => EmployeeAttendanceStatusType.FullDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};

	private static string FormatName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p));
		return string.Join(" ", parts);
	}

	private static string? TruncateForStorage(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.IsNullOrEmpty(value) ? value : value.Trim();
		}

		var trimmed = value.Trim();
		return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
	}

	private static DateTime ParseDate(string dateString)
	{
		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out var result))
		{
			return result;
		}

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return result;
		}

		if (DateTimeOffset.TryParse(dateString, out var dto))
		{
			return dto.DateTime;
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}
}
