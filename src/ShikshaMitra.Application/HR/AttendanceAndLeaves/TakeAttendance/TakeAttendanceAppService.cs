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
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class TakeAttendanceAppService(
	IRepository<Employee, Guid> employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IUserScopeService userScopeService)
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

		await EnsureMediumStageAllowedAsync(input.EducationMediumStageId);
		var locked = await staffAttendanceDayLockRepository.FindAsync(input.EducationMediumStageId, dateOnly);

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

		var attBaseQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attBaseQuery = attBaseQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var attForSummary = await AsyncExecuter.ToListAsync(
			attBaseQuery.Where(a =>
				allEmployeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate == dateOnly &&
				a.EducationMediumStageId == input.EducationMediumStageId));

		var employees = await AsyncExecuter.ToListAsync(
			empQuery.Skip(input.SkipCount).Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var ids = employees.Select(e => e.Id).ToList();
		var attRows = attForSummary.Where(a => ids.Contains(a.EmployeeId)).ToList();
		var attByEmp = attRows.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.First());

		var items = new List<TakeAttendanceEmployeeRowDto>();
		foreach (var e in employees)
		{
			attByEmp.TryGetValue(e.Id, out var a);
			var statusWire = a == null
				? "NOT_MARKED"
				: MapDomainStatusToWire(a.StatusType);
			items.Add(new TakeAttendanceEmployeeRowDto
			{
				EmployeeId = e.Id,
				FullName = FormatName(e.FirstName, e.MiddleName, e.LastName),
				EmployeeCode = e.EmployeeCode ?? string.Empty,
				Department = e.Department,
				DateIso = new DateTimeOffset(date.Date, TimeSpan.Zero).ToString("o"),
				InTime = a?.TimeIn?.ToString("o"),
				OutTime = a?.TimeOut?.ToString("o"),
				Remarks = a?.Remarks,
				Warnings = [],
				Errors = [],
				StatusWire = statusWire
			});
		}

		var summary = new TakeAttendanceSummaryDto
		{
			Present = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.Present),
			HalfDay = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			Absent = attForSummary.Count(x => x.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			NotMarked = allEmployeeIds.Count - attForSummary.Count
		};

		return new TakeAttendanceDayResponse
		{
			TotalCount = allEmployeeIds.Count,
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

		await EnsureMediumStageAllowedAsync(input.EducationMediumStageId);
		if (await staffAttendanceDayLockRepository.FindAsync(input.EducationMediumStageId, dateOnly) != null)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceDayLocked);
		}

		if (input.Entries == null || input.Entries.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

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

			if (existing != null)
			{
				if (existing.EducationMediumStageId.HasValue &&
				    existing.EducationMediumStageId.Value != input.EducationMediumStageId)
				{
					throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
				}

				existing.StatusType = status;
				existing.TimeIn = tIn;
				existing.TimeOut = tOut;
				existing.EducationMediumStageId ??= input.EducationMediumStageId;
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
					EducationMediumStageId = input.EducationMediumStageId,
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
	public async Task LockDayAsync(Guid educationMediumStageId, string attendanceDate)
	{
		var date = ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		await EnsureMediumStageAllowedAsync(educationMediumStageId);
		if (await staffAttendanceDayLockRepository.FindAsync(educationMediumStageId, dateOnly) != null)
		{
			return;
		}

		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var entity = new StaffAttendanceDayLock(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			educationMediumStageId,
			dateOnly,
			userId,
			Clock.Now);
		await staffAttendanceDayLockRepository.InsertAsync(entity, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_TakeAttendance.Lock)]
	public async Task UnlockDayAsync(Guid educationMediumStageId, string attendanceDate)
	{
		var date = ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		await EnsureMediumStageAllowedAsync(educationMediumStageId);
		var existing = await staffAttendanceDayLockRepository.FindAsync(educationMediumStageId, dateOnly);
		if (existing != null)
		{
			await staffAttendanceDayLockRepository.DeleteAsync(existing, autoSave: true);
		}
	}

	private async Task EnsureMediumStageAllowedAsync(Guid educationMediumStageId)
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (scope.IsAdmin || scope.HasWildcardMediumStage)
		{
			return;
		}

		if (scope.MediumStageIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		if (scope.MediumStageIds.Contains(educationMediumStageId))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	private static string MapDomainStatusToWire(EmployeeAttendanceStatusType s) =>
		s switch
		{
			EmployeeAttendanceStatusType.Present => "PRESENT",
			EmployeeAttendanceStatusType.HalfDayLeave => "HALF_DAY",
			EmployeeAttendanceStatusType.FullDayLeave => "ABSENT",
			_ => "NOT_MARKED"
		};

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
