using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterDtoMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterReader(
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	AttendanceRegisterLookupReader lookupReader,
	AttendanceRegisterRowReader rowReader,
	ICurrentUser currentUser)
	: ITransientDependency
{
	private const int MaxDateRangeDays = 366;

	public async Task<AttendanceRegisterResponse> GetAsync(GetAttendanceRegisterInput input)
	{
		var (filteredRows, lockLookup) = await ResolveRegisterRowsAsync(input);
		var allDayLocks = await lookupReader.BuildDayLockLookupAsync(filteredRows.Select(x => x.AttendanceDate));
		var totalCount = filteredRows.Count;
		var maxResultCount = NormalizeMaxResultCount(input.MaxResultCount);
		var skipCount = Math.Max(input.SkipCount, 0);
		var pageRows = filteredRows
			.OrderByDescending(x => x.AttendanceDate)
			.ThenBy(x => x.EmployeeName)
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToList();
		var stageNames = await lookupReader.BuildStageNameLookupAsync(pageRows.Select(x => x.EducationMediumStageId).Where(x => x.HasValue).Select(x => x!.Value));

		return new AttendanceRegisterResponse
		{
			TotalCount = totalCount,
			CurrentPage = maxResultCount == 0 ? 1 : (skipCount / maxResultCount) + 1,
			PageSize = maxResultCount,
			TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / maxResultCount),
			Summary = BuildSummary(filteredRows, lockLookup, allDayLocks, filteredRows.Select(x => x.EmployeeId).Distinct().Count()),
			Items = pageRows.Select(x => MapRow(x, lockLookup, stageNames, allDayLocks)).ToList()
		};
	}

	public async Task<GroupedAttendanceRegisterResponse> GetGroupedAsync(GetGroupedAttendanceRegisterInput input)
	{
		var register = await ReadGroupedRegisterAsync(input);
		var size = NormalizeMaxResultCount(input.MaxResultCount);
		var offset = Math.Max(input.SkipCount, 0);
		var page = register.Groups.Skip(offset).Take(size).ToList();
		var stageNames = await lookupReader.BuildStageNameLookupAsync(StageIds(page));
		return BuildGroupedPage(register, page, stageNames, offset, size);
	}

	internal async Task<Func<int, int, GroupedAttendanceRegisterResponse>> CaptureGroupedPagesAsync(
		GetGroupedAttendanceRegisterInput input, int populationLimit)
	{
		var register = await ReadGroupedRegisterAsync(input);
		if (register.Groups.Count > populationLimit)
			throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);

		var allStageNames = await lookupReader.BuildStageNameLookupAsync(StageIds(register.Groups));
		return (offset, size) =>
		{
			var page = register.Groups.Skip(offset).Take(size).ToList();
			var stageNames = StageIds(page).Distinct().Where(allStageNames.ContainsKey)
				.ToDictionary(id => id, id => allStageNames[id]);
			return BuildGroupedPage(register, page, stageNames, offset, size);
		};
	}

	private async Task<GroupedRegister> ReadGroupedRegisterAsync(GetGroupedAttendanceRegisterInput input)
	{
		var (filteredRows, lockLookup) = await ResolveRegisterRowsAsync(input);
		var allDayLocks = await lookupReader.BuildDayLockLookupAsync(filteredRows.Select(x => x.AttendanceDate));
		var groupedRows = filteredRows
			.GroupBy(x => x.EmployeeId)
			.Select(group => BuildGroupedEmployee(group.ToList(), lockLookup, allDayLocks))
			.Where(group => MatchesExceptionFilter(group, input.ExceptionFilter))
			.OrderByDescending(group => group.HasExceptions)
			.ThenByDescending(group => group.Absent)
			.ThenByDescending(group => group.MissingPunch)
			.ThenByDescending(group => group.HalfDay)
			.ThenByDescending(group => group.Locked)
			.ThenBy(group => group.EmployeeName)
			.ThenBy(group => group.EmployeeId)
			.ToList();

		groupedRows = AttendanceRegisterOrdering.Apply(groupedRows, input.Sorting);
		return new(groupedRows, lockLookup, allDayLocks, SummarizeGroups(groupedRows));
	}

	private static IEnumerable<Guid> StageIds(IEnumerable<GroupedRegisterProjection> groups) =>
		groups.Select(group => group.EducationMediumStageId).OfType<Guid>();

	private static GroupedAttendanceRegisterResponse BuildGroupedPage(GroupedRegister register,
		List<GroupedRegisterProjection> page, Dictionary<Guid, string> stageNames, int offset, int size) =>
		new()
		{
			TotalCount = register.Groups.Count,
			CurrentPage = offset / size + 1,
			PageSize = size,
			TotalPages = (int)Math.Ceiling((double)register.Groups.Count / size),
			Summary = register.Summary,
			Items = page.Select(group => MapGroupedEmployee(group, register.PayrollLocks, stageNames, register.DayLocks)).ToList()
		};

	private sealed record GroupedRegister(List<GroupedRegisterProjection> Groups,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> PayrollLocks,
		HashSet<DateOnly> DayLocks, AttendanceRegisterSummaryDto Summary);

	internal async Task<PayrollLockInfo> ResolvePayrollLockAsync(Guid employeeId, string monthYear)
	{
		return await lookupReader.ResolvePayrollLockAsync(employeeId, monthYear);
	}

	internal async Task<Dictionary<DateOnly, HolidayInfo>> BuildHolidayLookupAsync(IEnumerable<DateOnly> dates)
	{
		return await lookupReader.BuildHolidayLookupAsync(dates);
	}

	public async Task<UserScopeContext> EnsureCanUseRegisterAsync()
	{
		var uid = currentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanUseRegisterAsync));
		return scope;
	}

	private async Task<(List<RegisterProjection> Rows, Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> LockLookup)> ResolveRegisterRowsAsync(GetAttendanceRegisterInput input)
	{
		var (startDate, endDate) = ParseDateRange(input.StartDate, input.EndDate);
		var scope = await EnsureCanUseRegisterAsync();
		if (input.EducationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, input.EducationMediumStageId);
		}

		foreach (var stageId in input.EducationMediumStageIds.Distinct())
		{
			await EnsureStageInScopeAsync(scope, stageId);
		}

		var allRowsForSummary = await rowReader.BuildRowsAsync(input, startDate, endDate, scope);

		var lockLookup = await lookupReader.BuildLockLookupAsync(allRowsForSummary);
		return (ApplyPayrollStateFilter(allRowsForSummary, lockLookup, input.PayrollState), lockLookup);
	}

	private async Task EnsureStageInScopeAsync(UserScopeContext scope, Guid? stageId)
	{
		await hrScopeAccessService.EnsureCanAccessEmployeeStageAsync(
			scope,
			stageId,
			nameof(EnsureStageInScopeAsync));
	}

	private static (DateOnly Start, DateOnly End) ParseDateRange(string start, string end)
	{
		var startDate = DateOnly.FromDateTime(ParseDate(start));
		var endDate = DateOnly.FromDateTime(ParseDate(end));
		if (endDate < startDate)
		{
			throw new BusinessException(HRErrorCodes.AttendanceEndDateBeforeStartDate);
		}
		if (endDate.DayNumber - startDate.DayNumber > MaxDateRangeDays)
		{
			throw new BusinessException(HRErrorCodes.AttendanceDateRangeTooLarge);
		}
		return (startDate, endDate);
	}

	private static DateTime ParseDate(string dateString)
	{
		if (DateTime.TryParseExact(
			    dateString,
			    ["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"],
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.None,
			    out var exact))
		{
			return exact;
		}
		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}
}
