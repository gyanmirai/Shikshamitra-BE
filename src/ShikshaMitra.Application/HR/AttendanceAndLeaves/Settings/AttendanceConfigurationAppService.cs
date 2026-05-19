using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class AttendanceConfigurationAppService(
	IAttendanceConfigurationRepository attendanceConfigurationRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IRepository<AttendancePolicyEvaluationRun, Guid> evaluationRunRepository,
	IRepository<AttendancePolicyEvaluationResult, Guid> evaluationResultRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository)
	: ApplicationService, IAttendanceConfigurationAppService
{
	private const double DefaultExpectedWorkHours = 8d;
	private static readonly TimeOnly DefaultShiftStartTime = new(9, 0);
	private sealed record EmployeeEvaluationProfile(
		Guid Id,
		string? Department,
		EmployeeGroupType? EmployeeGroup,
		Guid RoleId,
		Guid? ShiftId);

	public async Task<AttendanceConfigurationResponse> GetAsync(Guid academicYearId)
	{
		var existing = await attendanceConfigurationRepository.FindByAcademicYearIdAsync(academicYearId);

		return existing == null
			? new AttendanceConfigurationResponse
			{
				Success = true,
				Message = "Attendance configuration not configured"
			}
			: MapToResponse(existing, "Attendance configuration fetched successfully");
	}

	public async Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(Guid academicYearId, AttendanceConfigurationRequest input)
	{
		ValidateRequest(input);

		var penaltyType = ParsePenaltyType(input.LateEarlyRules.PenaltyType);
		var overtimeRate = ParseOvertimeRate(input.Overtime.Rate);
		var existing = await attendanceConfigurationRepository.FindByAcademicYearIdAsync(academicYearId);
		AttendanceConfiguration saved;

		if (existing == null)
		{
			var fullDayThreshold = ResolveFullDayThreshold(input);
			var entity = new AttendanceConfiguration(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				fullDayThreshold,
				input.LateEarlyRules.GraceTime,
				input.LateEarlyRules.MaxLateAllowedPerMonth,
				penaltyType,
				input.Overtime.Enabled,
				overtimeRate,
				input.Overtime.MinOvertimeMinutes);

			ApplyFullConfiguration(entity, input, overtimeRate, penaltyType);
			entity.ReplaceOverrides(BuildOverrides(entity.Id, input.Overrides));
			await attendanceConfigurationRepository.InsertAsync(entity, autoSave: true);
			saved = entity;
		}
		else
		{
			ApplyFullConfiguration(existing, input, overtimeRate, penaltyType);
			existing.ReplaceOverrides(BuildOverrides(existing.Id, input.Overrides));
			await attendanceConfigurationRepository.UpdateAsync(existing, autoSave: true);
			saved = existing;
		}

		return MapToResponse(saved, "Attendance configuration saved successfully");
	}

	public async Task<AttendanceConfigurationPreviewResponse> PreviewAsync(Guid academicYearId, AttendanceConfigurationPreviewRequest input)
	{
		if (input.ToDate.Date < input.FromDate.Date)
		{
			throw new BusinessException("ShikshaMitra:AttendancePreviewDateRangeInvalid");
		}

		ValidateRequest(input.Configuration);

		var warnings = new List<string>
		{
			"Preview only. No attendance or payroll records will be changed.",
			"Payroll currently uses persisted attendance statuses; this preview is advisory until attendance is reprocessed."
		};

		if (input.Configuration.Status == "active")
		{
			warnings.Add("Activating the policy affects future attendance evaluation from the effective date.");
		}

		if (input.Configuration.Overrides.Count > 0)
		{
			warnings.Add("Overrides use priority order: shift, role, department, then global policy.");
		}

		var fromDate = DateOnly.FromDateTime(input.FromDate.Date);
		var toDate = DateOnly.FromDateTime(input.ToDate.Date);
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		employeeQuery = employeeQuery.Where(x => x.EmployeeStatusType == EmployeeStatusType.Active);
		employeeQuery = ApplyEmployeeFilters(employeeQuery, input, warnings);

		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Select(x => new EmployeeEvaluationProfile(
			x.Id,
			x.Department,
			x.EmployeeGroup,
			x.RoleId,
			x.ShiftId)));
		var employeeIds = employees.Select(x => x.Id).Distinct().ToList();

		if (employeeIds.Count == 0)
		{
			return new AttendanceConfigurationPreviewResponse
			{
				AffectedEmployeeCount = 0,
				EstimatedLateCount = 0,
				EstimatedAbsenceCount = 0,
				EstimatedOvertimeMinutes = 0,
				EstimatedPayableDayDelta = 0,
				Warnings = warnings
			};
		}

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var attendanceRecords = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(x =>
					employeeIds.Contains(x.EmployeeId) &&
					x.AttendanceDate >= fromDate &&
					x.AttendanceDate <= toDate));

		warnings.Add("Shift start and duration are not resolved in v1 preview; using 09:00 start and 8 expected hours where punch data is present.");
		if (input.FromDate.Year != input.ToDate.Year || input.FromDate.Month != input.ToDate.Month)
		{
			warnings.Add("Selected date range spans multiple payroll months; payable-day impact is approximate.");
		}

		var lateCountsByEmployeeMonth = new Dictionary<(Guid EmployeeId, int Year, int Month), int>();
		var estimatedLateCount = 0;
		var estimatedAbsenceCount = 0;
		var estimatedOvertimeMinutes = 0;
		var baselinePayableDays = 0m;
		var proposedPayableDays = 0m;

		foreach (var attendance in attendanceRecords
			.GroupBy(x => new { x.EmployeeId, x.AttendanceDate })
			.Select(x => x.OrderByDescending(row => row.LastModificationTime ?? row.CreationTime).First()))
		{
			var employee = employees.FirstOrDefault(x => x.Id == attendance.EmployeeId);
			var effectiveConfiguration = employee == null
				? input.Configuration
				: ResolveEffectiveConfiguration(input.Configuration, employee);
			var baselineUnits = GetBaselinePayableUnits(attendance.StatusType);
			var proposedUnits = GetProposedPayableUnits(attendance, effectiveConfiguration);
			var latePenaltyUnits = 0m;

			if (IsLate(attendance, effectiveConfiguration.LateEarlyRules.GraceTime))
			{
				estimatedLateCount++;
				var monthKey = (attendance.EmployeeId, attendance.AttendanceDate.Year, attendance.AttendanceDate.Month);
				lateCountsByEmployeeMonth.TryGetValue(monthKey, out var lateCount);
				lateCount++;
				lateCountsByEmployeeMonth[monthKey] = lateCount;
				latePenaltyUnits = GetLatePenaltyUnits(effectiveConfiguration, lateCount);
			}

			if (proposedUnits <= 0m)
			{
				estimatedAbsenceCount++;
			}

			estimatedOvertimeMinutes += GetOvertimeMinutes(attendance, effectiveConfiguration);
			baselinePayableDays += baselineUnits;
			proposedPayableDays += Math.Max(0m, proposedUnits - latePenaltyUnits);
		}

		var payrollCompatibleBaseline = PayrollProrationCalculator.CalculatePayableDays(
			attendanceRecords.Count(x => x.StatusType == EmployeeAttendanceStatusType.Present),
			attendanceRecords.Count(x => x.StatusType == EmployeeAttendanceStatusType.HalfDayLeave));
		if (Math.Abs(payrollCompatibleBaseline - baselinePayableDays) > 0.01m)
		{
			warnings.Add("Multiple attendance rows exist for at least one employee/date; preview uses the latest row per day.");
		}

		return new AttendanceConfigurationPreviewResponse
		{
			AffectedEmployeeCount = employeeIds.Count,
			EstimatedLateCount = estimatedLateCount,
			EstimatedAbsenceCount = estimatedAbsenceCount,
			EstimatedOvertimeMinutes = estimatedOvertimeMinutes,
			EstimatedPayableDayDelta = Math.Round((double)(proposedPayableDays - baselinePayableDays), 2),
			Warnings = warnings
		};
	}

	public async Task<AttendancePolicyEvaluationRunResponse> PreviewEvaluationAsync(AttendancePolicyEvaluationRequest input) =>
		await BuildEvaluationAsync(input, persist: false);

	public async Task<AttendancePolicyEvaluationRunResponse> CreateEvaluationAsync(AttendancePolicyEvaluationRequest input) =>
		await BuildEvaluationAsync(input, persist: true);

	public async Task<List<AttendancePolicyEvaluationRunResponse>> GetEvaluationsAsync(Guid academicYearId, int month, int year)
	{
		var query = await evaluationRunRepository.GetQueryableAsync();
		query = ApplyTenantFilter(query)
			.Where(x => x.AcademicYearId == academicYearId && x.Month == month && x.Year == year)
			.OrderByDescending(x => x.CreationTime);

		var runs = await AsyncExecuter.ToListAsync(query);
		var responses = new List<AttendancePolicyEvaluationRunResponse>();
		foreach (var run in runs)
		{
			responses.Add(await MapEvaluationRunAsync(run, includeResults: false));
		}

		return responses;
	}

	public async Task<AttendancePolicyEvaluationRunResponse> GetEvaluationAsync(Guid runId)
	{
		var run = await evaluationRunRepository.GetAsync(runId);
		EnsureTenant(run.TenantId);
		return await MapEvaluationRunAsync(run, includeResults: false);
	}

	public async Task<PagedResultDto<AttendancePolicyEvaluationResultResponse>> GetEvaluationResultsAsync(
		Guid runId,
		AttendancePolicyEvaluationResultsRequest input)
	{
		var run = await evaluationRunRepository.GetAsync(runId);
		EnsureTenant(run.TenantId);

		var maxResultCount = Math.Clamp(input.MaxResultCount <= 0 ? 25 : input.MaxResultCount, 1, 100);
		var skipCount = Math.Max(0, input.SkipCount);
		var query = await BuildEvaluationResultQueryAsync(runId, input);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.AttendanceDate)
				.ThenBy(x => x.EmployeeId)
				.Skip(skipCount)
				.Take(maxResultCount));
		var employeeIds = rows.Select(x => x.EmployeeId).Distinct().ToList();
		var employeeLookup = new Dictionary<Guid, (string? Code, string? Name)>();
		if (employeeIds.Count > 0)
		{
			var employeeQuery = ApplyTenantFilter(await employeeRepository.GetQueryableAsync())
				.Where(x => employeeIds.Contains(x.Id));
			employeeLookup = (await AsyncExecuter.ToListAsync(employeeQuery))
				.ToDictionary(x => x.Id, x => (x.EmployeeCode, FormatEmployeeName(x)));
		}

		return new PagedResultDto<AttendancePolicyEvaluationResultResponse>(
			totalCount,
			rows.Select(row => MapEvaluationResult(row, employeeLookup.GetValueOrDefault(row.EmployeeId))).ToList());
	}

	public async Task<AttendancePolicyEvaluationRunResponse> ApproveEvaluationAsync(Guid runId)
	{
		var run = await evaluationRunRepository.GetAsync(runId);
		EnsureTenant(run.TenantId);
		run.Approve(CurrentUser.Id, Clock.Now);
		await evaluationRunRepository.UpdateAsync(run, autoSave: true);
		return await MapEvaluationRunAsync(run, includeResults: false);
	}

	public async Task<AttendancePolicyEvaluationRunResponse> PublishEvaluationAsync(Guid runId)
	{
		var run = await evaluationRunRepository.GetAsync(runId);
		EnsureTenant(run.TenantId);

		var allRunsQuery = await evaluationRunRepository.GetQueryableAsync();
		var publishedRuns = await AsyncExecuter.ToListAsync(
			ApplyTenantFilter(allRunsQuery)
				.Where(x =>
					x.Id != run.Id &&
					x.AcademicYearId == run.AcademicYearId &&
					x.Year == run.Year &&
					x.Month == run.Month &&
					x.Status == AttendancePolicyEvaluationRunStatus.Published));
		foreach (var prior in publishedRuns)
		{
			prior.Supersede();
			await evaluationRunRepository.UpdateAsync(prior, autoSave: false);
		}

		if (publishedRuns.Count > 0)
		{
			await UnitOfWorkManager.Current!.SaveChangesAsync();
		}

		run.Publish(CurrentUser.Id, Clock.Now);
		await evaluationRunRepository.UpdateAsync(run, autoSave: false);
		await InvalidateUnlockedPayrollSnapshotsAsync(run.Year, run.Month);
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return await MapEvaluationRunAsync(run, includeResults: false);
	}

	public async Task<AttendancePolicyEvaluationRunResponse> CancelEvaluationAsync(Guid runId)
	{
		var run = await evaluationRunRepository.GetAsync(runId);
		EnsureTenant(run.TenantId);
		run.Cancel();
		await evaluationRunRepository.UpdateAsync(run, autoSave: true);
		return await MapEvaluationRunAsync(run, includeResults: false);
	}

	private async Task<AttendancePolicyEvaluationRunResponse> BuildEvaluationAsync(AttendancePolicyEvaluationRequest input, bool persist)
	{
		if (persist && HasEvaluationFilters(input))
		{
			throw new BusinessException("ShikshaMitra:ScopedAttendanceEvaluationCannotBePublished");
		}

		var configuration = await attendanceConfigurationRepository.FindByAcademicYearIdAsync(input.AcademicYearId)
			?? throw new BusinessException("ShikshaMitra:AttendanceConfigurationRequired");
		var configurationRequest = MapConfigurationToRequest(configuration);
		var fromDate = DateOnly.FromDateTime(new DateTime(input.Year, input.Month, 1));
		var toDate = DateOnly.FromDateTime(new DateTime(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month)));
		var configHash = AttendanceConfigurationHasher.Compute(configuration);
		var warnings = new List<string>
		{
			"Evaluation preview only until approved and published.",
			"Raw attendance records will not be changed."
		};
		if (HasEvaluationFilters(input))
		{
			warnings.Add("Filtered preview cannot be published; create a full monthly evaluation to publish.");
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		employeeQuery = ApplyTenantFilter(employeeQuery)
			.Where(x => x.EmployeeStatusType == EmployeeStatusType.Active);
		employeeQuery = ApplyEmployeeFilters(employeeQuery, new AttendanceConfigurationPreviewRequest
		{
			FromDate = fromDate.ToDateTime(TimeOnly.MinValue),
			ToDate = toDate.ToDateTime(TimeOnly.MinValue),
			DepartmentId = input.DepartmentId,
			RoleId = input.RoleId,
			EmployeeType = input.EmployeeType,
			ShiftId = input.ShiftId,
			Configuration = configurationRequest
		}, warnings);

		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Select(x => new EmployeeEvaluationProfile(
			x.Id,
			x.Department,
			x.EmployeeGroup,
			x.RoleId,
			x.ShiftId)));
		var employeeIds = employees.Select(x => x.Id).Distinct().ToList();
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var attendanceRows = await AsyncExecuter.ToListAsync(
			ApplyTenantFilter(attendanceQuery)
				.Where(x => employeeIds.Contains(x.EmployeeId) && x.AttendanceDate >= fromDate && x.AttendanceDate <= toDate));
		var latestRowsByEmployeeDate = attendanceRows
			.GroupBy(x => new { x.EmployeeId, x.AttendanceDate })
			.ToDictionary(
				x => (x.Key.EmployeeId, x.Key.AttendanceDate),
				x => x.OrderByDescending(row => row.LastModificationTime ?? row.CreationTime).First());
		var workingDates = GetPolicyWorkingDates(fromDate, toDate, configurationRequest).ToList();

		var run = new AttendancePolicyEvaluationRun(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			configuration.Id,
			input.Year,
			input.Month,
			fromDate,
			toDate,
			configHash);

		var lateCountsByEmployeeMonth = new Dictionary<(Guid EmployeeId, int Year, int Month), int>();
		var resultResponses = new List<AttendancePolicyEvaluationResultResponse>();
		var results = new List<AttendancePolicyEvaluationResult>();
		var lateCount = 0;
		var absenceCount = 0;
		var overtimeMinutes = 0;
		var baselinePayable = 0m;
		var evaluatedPayable = 0m;
		var hasBlockingWarnings = false;

		foreach (var employee in employees.OrderBy(x => x.Id))
		{
			var effectiveConfiguration = ResolveEffectiveConfiguration(configurationRequest, employee);
			foreach (var attendanceDate in workingDates)
			{
				latestRowsByEmployeeDate.TryGetValue((employee.Id, attendanceDate), out var attendance);
				var rowWarnings = new List<string>();
				var baselineStatus = attendance?.StatusType ?? EmployeeAttendanceStatusType.FullDayLeave;
				var baselineUnits = attendance == null ? 0m : GetBaselinePayableUnits(attendance.StatusType);
				var proposedUnits = attendance == null
					? GetMissingAttendancePayableUnits(effectiveConfiguration, rowWarnings, ref hasBlockingWarnings)
					: GetProposedPayableUnits(attendance, effectiveConfiguration);
				var isLate = attendance != null && IsLate(attendance, effectiveConfiguration.LateEarlyRules.GraceTime);
				var isEarlyExit = false;
				var latePenaltyUnits = 0m;

				if (attendance == null)
				{
					rowWarnings.Add("No raw attendance record exists for this employee/date.");
				}
				else if (!attendance.TimeIn.HasValue || !attendance.TimeOut.HasValue)
				{
					if (ParseMissingPunchBehavior(effectiveConfiguration.PunchRules.MissingPunchBehavior) == AttendanceMissingPunchBehavior.RequiresReview)
					{
						rowWarnings.Add("Missing punch requires review before publish.");
						hasBlockingWarnings = true;
					}
				}

				if (isLate)
				{
					lateCount++;
					var monthKey = (employee.Id, attendanceDate.Year, attendanceDate.Month);
					lateCountsByEmployeeMonth.TryGetValue(monthKey, out var currentLateCount);
					currentLateCount++;
					lateCountsByEmployeeMonth[monthKey] = currentLateCount;
					latePenaltyUnits = GetLatePenaltyUnits(effectiveConfiguration, currentLateCount);
				}

				var evaluatedUnits = Math.Max(0m, proposedUnits - latePenaltyUnits);
				var evaluatedStatus = ToStatusType(evaluatedUnits);
				if (evaluatedUnits <= 0m)
				{
					absenceCount++;
				}

				var rowOvertime = attendance == null ? 0 : GetOvertimeMinutes(attendance, effectiveConfiguration);
				overtimeMinutes += rowOvertime;
				baselinePayable += baselineUnits;
				evaluatedPayable += evaluatedUnits;
				var warningsJson = JsonSerializer.Serialize(rowWarnings);

				var result = new AttendancePolicyEvaluationResult(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					run.Id,
					employee.Id,
					attendanceDate,
					attendance?.Id,
					baselineStatus,
					baselineUnits,
					evaluatedStatus,
					evaluatedUnits,
					isLate,
					isEarlyExit,
					rowOvertime,
					warningsJson,
					false);
				results.Add(result);
				resultResponses.Add(MapEvaluationResult(result));
			}
		}

		if (hasBlockingWarnings)
		{
			warnings.Add("One or more rows require review before this evaluation can be approved.");
		}

		run.SetEvaluationSummary(
			employeeIds.Count,
			lateCount,
			absenceCount,
			overtimeMinutes,
			Math.Round(evaluatedPayable - baselinePayable, 2),
			JsonSerializer.Serialize(warnings),
			hasBlockingWarnings);

		if (persist)
		{
			await evaluationRunRepository.InsertAsync(run, autoSave: false);
			foreach (var result in results)
			{
				await evaluationResultRepository.InsertAsync(result, autoSave: false);
			}

			await UnitOfWorkManager.Current!.SaveChangesAsync();
		}

		var response = MapEvaluationRun(run, warnings, resultResponses, resultResponses.Count);
		return response;
	}

	private void ApplyFullConfiguration(
		AttendanceConfiguration entity,
		AttendanceConfigurationRequest input,
		AttendanceOvertimeRate? overtimeRate,
		AttendancePenaltyType penaltyType)
	{
		var fullDayThreshold = ResolveFullDayThreshold(input);

		entity.Update(
			input.PolicyName,
			ParseStatus(input.Status),
			input.EffectiveFrom,
			input.EffectiveTo,
			ParseWeeklyOffPattern(input.WorkdayRules.WeeklyOffPattern),
			string.Join(",", input.WorkdayRules.CustomWeeklyOffs.Distinct().OrderBy(x => x)),
			fullDayThreshold,
			input.PunchRules.HalfDayThresholdPercentage,
			input.PunchRules.AbsentThresholdPercentage,
			ParseMissingPunchBehavior(input.PunchRules.MissingPunchBehavior),
			input.ManualCorrectionRules.Allowed,
			input.LateEarlyRules.GraceTime,
			input.LateEarlyRules.MaxLateAllowedPerMonth,
			penaltyType,
			input.LateEarlyRules.PenaltyCountThreshold,
			input.LateEarlyRules.EarlyExitEnabled,
			input.LateEarlyRules.EarlyExitGraceTime,
			input.Overtime.Enabled,
			overtimeRate,
			input.Overtime.MinOvertimeMinutes,
			ParseHolidayWorkBehavior(input.HolidayRules.WorkOnHolidayBehavior),
			input.HolidayRules.SandwichRule,
			input.PayrollPreviewEnabled,
			ParsePayrollIntegrationMode(input.PayrollIntegrationMode),
			input.RequirePublishedAttendanceForPayrollLock);
	}

	private IEnumerable<AttendancePolicyOverride> BuildOverrides(Guid configurationId, IEnumerable<AttendancePolicyOverrideRequest> overrides)
	{
		var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var item in overrides)
		{
			var targetType = ParseOverrideTargetType(item.TargetType);
			var duplicateKey = $"{targetType}:{item.TargetId}";
			if (item.IsActive && !seenTargets.Add(duplicateKey))
			{
				throw new BusinessException("ShikshaMitra:DuplicateAttendancePolicyOverride");
			}

			yield return new AttendancePolicyOverride(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				configurationId,
				targetType,
				item.TargetId,
				item.TargetName,
				item.IsActive,
				item.HoursBasedPayPercentage,
				item.GraceTime,
				item.MaxLateAllowedPerMonth,
				item.PenaltyType == null ? null : ParsePenaltyType(item.PenaltyType),
				item.OvertimeEnabled,
				ParseOvertimeRate(item.OvertimeRate),
				item.MinOvertimeMinutes);
		}
	}

	private async Task InvalidateUnlockedPayrollSnapshotsAsync(int year, int month)
	{
		var monthYear = $"{year:0000}-{month:00}";
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		var snapshots = await AsyncExecuter.ToListAsync(
			ApplyTenantFilter(query).Where(x => x.MonthYear == monthYear && !x.IsLocked));

		foreach (var snapshot in snapshots)
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}
	}

	private async Task<IQueryable<AttendancePolicyEvaluationResult>> BuildEvaluationResultQueryAsync(
		Guid runId,
		AttendancePolicyEvaluationResultsRequest input)
	{
		var resultQuery = ApplyTenantFilter(await evaluationResultRepository.GetQueryableAsync())
			.Where(x => x.AttendancePolicyEvaluationRunId == runId);

		if (input.FromDate.HasValue)
		{
			var fromDate = DateOnly.FromDateTime(input.FromDate.Value.Date);
			resultQuery = resultQuery.Where(x => x.AttendanceDate >= fromDate);
		}

		if (input.ToDate.HasValue)
		{
			var toDate = DateOnly.FromDateTime(input.ToDate.Value.Date);
			resultQuery = resultQuery.Where(x => x.AttendanceDate <= toDate);
		}

		if (!string.IsNullOrWhiteSpace(input.EvaluatedStatus) &&
			Enum.TryParse<EmployeeAttendanceStatusType>(input.EvaluatedStatus, ignoreCase: true, out var evaluatedStatus))
		{
			resultQuery = resultQuery.Where(x => x.EvaluatedStatus == evaluatedStatus);
		}

		if (input.HasWarnings.HasValue)
		{
			resultQuery = input.HasWarnings.Value
				? resultQuery.Where(x => x.WarningsJson != null && x.WarningsJson != "[]")
				: resultQuery.Where(x => x.WarningsJson == null || x.WarningsJson == "[]");
		}

		if (input.IsLate.HasValue)
		{
			resultQuery = resultQuery.Where(x => x.IsLate == input.IsLate.Value);
		}

		if (input.HasOvertime.HasValue)
		{
			resultQuery = input.HasOvertime.Value
				? resultQuery.Where(x => x.OvertimeMinutes > 0)
				: resultQuery.Where(x => x.OvertimeMinutes <= 0);
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		employeeQuery = ApplyTenantFilter(employeeQuery);
		var shouldFilterEmployees =
			!string.IsNullOrWhiteSpace(input.Search) ||
			!string.IsNullOrWhiteSpace(input.DepartmentId) ||
			!string.IsNullOrWhiteSpace(input.RoleId) ||
			!string.IsNullOrWhiteSpace(input.EmployeeType) ||
			!string.IsNullOrWhiteSpace(input.ShiftId);

		if (!shouldFilterEmployees)
		{
			return resultQuery;
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			employeeQuery = employeeQuery.Where(x =>
				(x.EmployeeCode != null && x.EmployeeCode.ToLower().Contains(search)) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(search)) ||
				(x.MiddleName != null && x.MiddleName.ToLower().Contains(search)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(search)));
		}

		if (!string.IsNullOrWhiteSpace(input.DepartmentId))
		{
			var department = input.DepartmentId.Trim().ToLower();
			employeeQuery = employeeQuery.Where(x => x.Department != null && x.Department.ToLower() == department);
		}

		if (!string.IsNullOrWhiteSpace(input.RoleId))
		{
			if (!Guid.TryParse(input.RoleId, out var roleId))
			{
				return resultQuery.Where(_ => false);
			}

			employeeQuery = employeeQuery.Where(x => x.RoleId == roleId);
		}

		if (!string.IsNullOrWhiteSpace(input.ShiftId))
		{
			if (!Guid.TryParse(input.ShiftId, out var shiftId))
			{
				return resultQuery.Where(_ => false);
			}

			employeeQuery = employeeQuery.Where(x => x.ShiftId == shiftId);
		}

		if (!string.IsNullOrWhiteSpace(input.EmployeeType) &&
			TryParseEmployeeGroup(input.EmployeeType, out var employeeGroup))
		{
			employeeQuery = employeeQuery.Where(x => x.EmployeeGroup == employeeGroup);
		}
		else if (!string.IsNullOrWhiteSpace(input.EmployeeType))
		{
			return resultQuery.Where(_ => false);
		}

		var employeeIds = employeeQuery.Select(x => x.Id);
		return resultQuery.Where(x => employeeIds.Contains(x.EmployeeId));
	}

	private async Task<AttendancePolicyEvaluationRunResponse> MapEvaluationRunAsync(
		AttendancePolicyEvaluationRun run,
		bool includeResults)
	{
		var warnings = JsonSerializer.Deserialize<List<string>>(run.WarningsJson) ?? [];
		var results = new List<AttendancePolicyEvaluationResultResponse>();
		long totalResultCount;
		var query = ApplyTenantFilter(await evaluationResultRepository.GetQueryableAsync())
			.Where(x => x.AttendancePolicyEvaluationRunId == run.Id);

		if (includeResults)
		{
			var rows = await AsyncExecuter.ToListAsync(
				query
					.OrderBy(x => x.AttendanceDate)
					.ThenBy(x => x.EmployeeId));
			results = rows.Select(MapEvaluationResult).ToList();
			totalResultCount = results.Count;
		}
		else
		{
			totalResultCount = await AsyncExecuter.CountAsync(query);
		}

		return MapEvaluationRun(run, warnings, results, totalResultCount);
	}

	private static AttendancePolicyEvaluationRunResponse MapEvaluationRun(
		AttendancePolicyEvaluationRun run,
		List<string> warnings,
		List<AttendancePolicyEvaluationResultResponse> results,
		long totalResultCount) =>
		new()
		{
			Id = run.Id,
			AcademicYearId = run.AcademicYearId,
			AttendanceConfigurationId = run.AttendanceConfigurationId,
			Year = run.Year,
			Month = run.Month,
			FromDate = run.FromDate.ToDateTime(TimeOnly.MinValue),
			ToDate = run.ToDate.ToDateTime(TimeOnly.MinValue),
			Status = MapEvaluationStatus(run.Status),
			AffectedEmployeeCount = run.AffectedEmployeeCount,
			EstimatedLateCount = run.EstimatedLateCount,
			EstimatedAbsenceCount = run.EstimatedAbsenceCount,
			EstimatedOvertimeMinutes = run.EstimatedOvertimeMinutes,
			EstimatedPayableDayDelta = (double)run.EstimatedPayableDayDelta,
			ConfigHash = run.ConfigHash,
			ApprovedAt = run.ApprovedAt,
			PublishedAt = run.PublishedAt,
			CreatedAt = run.CreationTime,
			LastUpdatedAt = run.LastModificationTime,
			TotalResultCount = totalResultCount,
			Warnings = warnings,
			Results = results
		};

	private static AttendancePolicyEvaluationResultResponse MapEvaluationResult(AttendancePolicyEvaluationResult result) =>
		MapEvaluationResult(result, default);

	private static AttendancePolicyEvaluationResultResponse MapEvaluationResult(
		AttendancePolicyEvaluationResult result,
		(string? Code, string? Name) employee) =>
		new()
		{
			Id = result.Id,
			EmployeeId = result.EmployeeId,
			EmployeeCode = employee.Code,
			EmployeeName = employee.Name,
			AttendanceDate = result.AttendanceDate.ToDateTime(TimeOnly.MinValue),
			SourceAttendanceId = result.SourceAttendanceId,
			BaselineStatus = result.BaselineStatus.ToString(),
			BaselinePayableUnits = (double)result.BaselinePayableUnits,
			EvaluatedStatus = result.EvaluatedStatus.ToString(),
			EvaluatedPayableUnits = (double)result.EvaluatedPayableUnits,
			IsLate = result.IsLate,
			IsEarlyExit = result.IsEarlyExit,
			OvertimeMinutes = result.OvertimeMinutes,
			Warnings = JsonSerializer.Deserialize<List<string>>(result.WarningsJson) ?? [],
			IsManualOverride = result.IsManualOverride
		};

	private static string FormatEmployeeName(Employee employee)
	{
		if (!string.IsNullOrWhiteSpace(employee.EmployeeName))
		{
			return employee.EmployeeName;
		}

		return string.Join(" ", new[] { employee.FirstName, employee.MiddleName, employee.LastName }
			.Where(x => !string.IsNullOrWhiteSpace(x)));
	}

	private static AttendanceConfigurationRequest MapConfigurationToRequest(AttendanceConfiguration entity) =>
		new()
		{
			PolicyName = entity.PolicyName,
			Status = MapStatus(entity.Status),
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveTo = entity.EffectiveTo,
			WorkdayRules = new AttendanceWorkdayRulesRequest
			{
				WeeklyOffPattern = MapWeeklyOffPattern(entity.WeeklyOffPattern),
				CustomWeeklyOffs = MapCustomWeeklyOffs(entity.CustomWeeklyOffs)
			},
			PunchRules = new AttendancePunchRulesRequest
			{
				FullDayThresholdPercentage = entity.HoursBasedPayPercentage,
				HalfDayThresholdPercentage = entity.HalfDayPayPercentage,
				AbsentThresholdPercentage = entity.AbsentPayPercentage,
				MissingPunchBehavior = MapMissingPunchBehavior(entity.MissingPunchBehavior)
			},
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = entity.HoursBasedPayPercentage
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = entity.GraceTime,
				MaxLateAllowedPerMonth = entity.MaxLateAllowedPerMonth,
				PenaltyType = MapPenaltyType(entity.PenaltyType),
				PenaltyCountThreshold = entity.PenaltyCountThreshold,
				EarlyExitEnabled = entity.EarlyExitEnabled,
				EarlyExitGraceTime = entity.EarlyExitGraceTime
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = entity.OvertimeEnabled,
				Rate = entity.OvertimeRate.HasValue ? MapOvertimeRate(entity.OvertimeRate.Value) : null,
				MinOvertimeMinutes = entity.MinOvertimeMinutes
			},
			HolidayRules = new AttendanceHolidayRulesRequest
			{
				WorkOnHolidayBehavior = MapHolidayWorkBehavior(entity.HolidayWorkBehavior),
				SandwichRule = entity.SandwichRuleEnabled
			},
			ManualCorrectionRules = new AttendanceManualCorrectionRulesRequest
			{
				Allowed = entity.ManualCorrectionAllowed
			},
			PayrollPreviewEnabled = entity.PayrollPreviewEnabled,
			PayrollIntegrationMode = MapPayrollIntegrationMode(entity.PayrollIntegrationMode),
			RequirePublishedAttendanceForPayrollLock = entity.RequirePublishedAttendanceForPayrollLock,
			Overrides = entity.Overrides.Select(x => new AttendancePolicyOverrideRequest
			{
				TargetType = MapOverrideTargetType(x.TargetType),
				TargetId = x.TargetId,
				TargetName = x.TargetName,
				IsActive = x.IsActive,
				HoursBasedPayPercentage = x.HoursBasedPayPercentage,
				GraceTime = x.GraceTime,
				MaxLateAllowedPerMonth = x.MaxLateAllowedPerMonth,
				PenaltyType = x.PenaltyType.HasValue ? MapPenaltyType(x.PenaltyType.Value) : null,
				OvertimeEnabled = x.OvertimeEnabled,
				OvertimeRate = x.OvertimeRate.HasValue ? MapOvertimeRate(x.OvertimeRate.Value) : null,
				MinOvertimeMinutes = x.MinOvertimeMinutes
			}).ToList()
		};

	private IQueryable<Employee> ApplyTenantFilter(IQueryable<Employee> query) =>
		CurrentTenant.Id.HasValue ? query.Where(x => x.TenantId == CurrentTenant.Id) : query.Where(x => x.TenantId == null);

	private IQueryable<EmployeeAttendance> ApplyTenantFilter(IQueryable<EmployeeAttendance> query) =>
		CurrentTenant.Id.HasValue ? query.Where(x => x.TenantId == CurrentTenant.Id) : query.Where(x => x.TenantId == null);

	private IQueryable<AttendancePolicyEvaluationRun> ApplyTenantFilter(IQueryable<AttendancePolicyEvaluationRun> query) =>
		CurrentTenant.Id.HasValue ? query.Where(x => x.TenantId == CurrentTenant.Id) : query.Where(x => x.TenantId == null);

	private IQueryable<AttendancePolicyEvaluationResult> ApplyTenantFilter(IQueryable<AttendancePolicyEvaluationResult> query) =>
		CurrentTenant.Id.HasValue ? query.Where(x => x.TenantId == CurrentTenant.Id) : query.Where(x => x.TenantId == null);

	private IQueryable<PayrollRunItemSnapshot> ApplyTenantFilter(IQueryable<PayrollRunItemSnapshot> query) =>
		CurrentTenant.Id.HasValue ? query.Where(x => x.TenantId == CurrentTenant.Id) : query.Where(x => x.TenantId == null);

	private void EnsureTenant(Guid? tenantId)
	{
		if (tenantId != CurrentTenant.Id)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEvaluationNotFound");
		}
	}

	private static EmployeeAttendanceStatusType ToStatusType(decimal payableUnits) =>
		payableUnits >= 1m
			? EmployeeAttendanceStatusType.Present
			: payableUnits >= 0.5m
				? EmployeeAttendanceStatusType.HalfDayLeave
				: EmployeeAttendanceStatusType.FullDayLeave;

	private static void ValidateRequest(AttendanceConfigurationRequest input)
	{
		ResolveFullDayThreshold(input);

		if (input.Status == "active" && !input.EffectiveFrom.HasValue)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEffectiveFromRequired");
		}

		var fullDayThreshold = ResolveFullDayThreshold(input);

		if (input.PunchRules.HalfDayThresholdPercentage >= fullDayThreshold)
		{
			throw new BusinessException("ShikshaMitra:HalfDayThresholdMustBeLessThanFullDay");
		}

		if (input.PunchRules.AbsentThresholdPercentage > input.PunchRules.HalfDayThresholdPercentage)
		{
			throw new BusinessException("ShikshaMitra:AbsentThresholdMustBeLessThanHalfDay");
		}

		if (input.Overtime.Enabled && (string.IsNullOrWhiteSpace(input.Overtime.Rate) || !input.Overtime.MinOvertimeMinutes.HasValue))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}
	}

	private static double ResolveFullDayThreshold(AttendanceConfigurationRequest input)
	{
		var canonical = input.PunchRules.FullDayThresholdPercentage;
		var legacy = input.DailyRules.HoursBasedPayPercentage;

		if (!canonical.HasValue && !legacy.HasValue)
		{
			return 75d;
		}

		if (!canonical.HasValue)
		{
			return legacy!.Value;
		}

		if (legacy.HasValue && Math.Abs(legacy.Value - canonical.Value) > 0.0001)
		{
			throw new BusinessException("ShikshaMitra:ConflictingFullDayThreshold");
		}

		return canonical.Value;
	}

	private IQueryable<Employee> ApplyEmployeeFilters(
		IQueryable<Employee> query,
		AttendanceConfigurationPreviewRequest input,
		List<string> warnings)
	{
		if (!string.IsNullOrWhiteSpace(input.DepartmentId))
		{
			var department = input.DepartmentId.Trim().ToLower();
			query = query.Where(x => x.Department != null && x.Department.ToLower() == department);
			warnings.Add("Department preview filter is matched against employee department text.");
		}

		if (!string.IsNullOrWhiteSpace(input.RoleId))
		{
			if (Guid.TryParse(input.RoleId, out var roleId))
			{
				query = query.Where(x => x.RoleId == roleId);
			}
			else
			{
				warnings.Add("Role filter was ignored because it is not a valid role id.");
			}
		}

		if (!string.IsNullOrWhiteSpace(input.ShiftId))
		{
			if (Guid.TryParse(input.ShiftId, out var shiftId))
			{
				query = query.Where(x => x.ShiftId == shiftId);
			}
			else
			{
				warnings.Add("Shift filter was ignored because it is not a valid shift id.");
			}
		}

		if (!string.IsNullOrWhiteSpace(input.EmployeeType))
		{
			if (TryParseEmployeeGroup(input.EmployeeType, out var employeeGroup))
			{
				query = query.Where(x => x.EmployeeGroup == employeeGroup);
			}
			else
			{
				warnings.Add("Employee type filter was ignored because it is not teaching or non-teaching.");
			}
		}

		return query;
	}

	private static bool TryParseEmployeeGroup(string value, out EmployeeGroupType employeeGroup)
	{
		var normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
		if (string.Equals(normalized, "teaching", StringComparison.OrdinalIgnoreCase) || normalized == "0")
		{
			employeeGroup = EmployeeGroupType.Teaching;
			return true;
		}

		if (string.Equals(normalized, "nonteaching", StringComparison.OrdinalIgnoreCase) || normalized == "1")
		{
			employeeGroup = EmployeeGroupType.NonTeaching;
			return true;
		}

		employeeGroup = default;
		return false;
	}

	private static bool HasEvaluationFilters(AttendancePolicyEvaluationRequest input) =>
		!string.IsNullOrWhiteSpace(input.DepartmentId) ||
		!string.IsNullOrWhiteSpace(input.RoleId) ||
		!string.IsNullOrWhiteSpace(input.EmployeeType) ||
		!string.IsNullOrWhiteSpace(input.ShiftId);

	private static IEnumerable<DateOnly> GetPolicyWorkingDates(
		DateOnly fromDate,
		DateOnly toDate,
		AttendanceConfigurationRequest configuration)
	{
		for (var date = fromDate; date <= toDate; date = date.AddDays(1))
		{
			if (IsPolicyWorkingDay(date, configuration))
			{
				yield return date;
			}
		}
	}

	private static bool IsPolicyWorkingDay(DateOnly date, AttendanceConfigurationRequest configuration)
	{
		var day = (int)date.DayOfWeek;
		return ParseWeeklyOffPattern(configuration.WorkdayRules.WeeklyOffPattern) switch
		{
			AttendanceWeeklyOffPattern.Sunday => day != 0,
			AttendanceWeeklyOffPattern.Custom => !configuration.WorkdayRules.CustomWeeklyOffs.Contains(day),
			_ => day is not 0 and not 6
		};
	}

	private static decimal GetMissingAttendancePayableUnits(
		AttendanceConfigurationRequest configuration,
		List<string> rowWarnings,
		ref bool hasBlockingWarnings)
	{
		return ParseMissingPunchBehavior(configuration.PunchRules.MissingPunchBehavior) switch
		{
			AttendanceMissingPunchBehavior.MarkHalfDay => 0.5m,
			AttendanceMissingPunchBehavior.RequiresReview => MarkMissingAttendanceForReview(rowWarnings, ref hasBlockingWarnings),
			_ => 0m
		};
	}

	private static decimal MarkMissingAttendanceForReview(List<string> rowWarnings, ref bool hasBlockingWarnings)
	{
		rowWarnings.Add("Missing attendance requires review before publish.");
		hasBlockingWarnings = true;
		return 0m;
	}

	private static AttendanceConfigurationRequest ResolveEffectiveConfiguration(
		AttendanceConfigurationRequest configuration,
		EmployeeEvaluationProfile employee)
	{
		var effective = CloneConfiguration(configuration);
		var match = FindOverride(configuration.Overrides, employee);
		if (match == null)
		{
			return effective;
		}

		if (match.HoursBasedPayPercentage.HasValue)
		{
			effective.PunchRules.FullDayThresholdPercentage = match.HoursBasedPayPercentage.Value;
			effective.DailyRules.HoursBasedPayPercentage = match.HoursBasedPayPercentage.Value;
		}

		if (match.GraceTime.HasValue)
		{
			effective.LateEarlyRules.GraceTime = match.GraceTime.Value;
		}

		if (match.MaxLateAllowedPerMonth.HasValue)
		{
			effective.LateEarlyRules.MaxLateAllowedPerMonth = match.MaxLateAllowedPerMonth.Value;
		}

		if (!string.IsNullOrWhiteSpace(match.PenaltyType))
		{
			effective.LateEarlyRules.PenaltyType = match.PenaltyType;
		}

		if (match.OvertimeEnabled.HasValue)
		{
			effective.Overtime.Enabled = match.OvertimeEnabled.Value;
		}

		if (!string.IsNullOrWhiteSpace(match.OvertimeRate))
		{
			effective.Overtime.Rate = match.OvertimeRate;
		}

		if (match.MinOvertimeMinutes.HasValue)
		{
			effective.Overtime.MinOvertimeMinutes = match.MinOvertimeMinutes;
		}

		return effective;
	}

	private static AttendancePolicyOverrideRequest? FindOverride(
		IReadOnlyCollection<AttendancePolicyOverrideRequest> overrides,
		EmployeeEvaluationProfile employee) =>
		FindOverride(overrides, "shift", employee.ShiftId?.ToString()) ??
		FindOverride(overrides, "role", employee.RoleId.ToString()) ??
		FindOverride(overrides, "employee_type", MapEmployeeGroupOverrideTarget(employee.EmployeeGroup)) ??
		FindOverride(overrides, "employee_type", employee.EmployeeGroup?.ToString()) ??
		FindOverride(overrides, "employee_type", employee.EmployeeGroup.HasValue ? ((int)employee.EmployeeGroup.Value).ToString() : null) ??
		FindOverride(overrides, "department", employee.Department);

	private static string? MapEmployeeGroupOverrideTarget(EmployeeGroupType? employeeGroup) =>
		employeeGroup switch
		{
			EmployeeGroupType.Teaching => "teaching",
			EmployeeGroupType.NonTeaching => "non_teaching",
			_ => null
		};

	private static AttendancePolicyOverrideRequest? FindOverride(
		IReadOnlyCollection<AttendancePolicyOverrideRequest> overrides,
		string targetType,
		string? targetId)
	{
		if (string.IsNullOrWhiteSpace(targetId))
		{
			return null;
		}

		return overrides.FirstOrDefault(x =>
			x.IsActive &&
			string.Equals(x.TargetType, targetType, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(x.TargetId, targetId, StringComparison.OrdinalIgnoreCase));
	}

	private static AttendanceConfigurationRequest CloneConfiguration(AttendanceConfigurationRequest source) =>
		new()
		{
			PolicyName = source.PolicyName,
			Status = source.Status,
			EffectiveFrom = source.EffectiveFrom,
			EffectiveTo = source.EffectiveTo,
			WorkdayRules = new AttendanceWorkdayRulesRequest
			{
				WeeklyOffPattern = source.WorkdayRules.WeeklyOffPattern,
				CustomWeeklyOffs = source.WorkdayRules.CustomWeeklyOffs.ToList()
			},
			PunchRules = new AttendancePunchRulesRequest
			{
				FullDayThresholdPercentage = source.PunchRules.FullDayThresholdPercentage,
				HalfDayThresholdPercentage = source.PunchRules.HalfDayThresholdPercentage,
				AbsentThresholdPercentage = source.PunchRules.AbsentThresholdPercentage,
				MissingPunchBehavior = source.PunchRules.MissingPunchBehavior
			},
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = source.DailyRules.HoursBasedPayPercentage
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = source.LateEarlyRules.GraceTime,
				MaxLateAllowedPerMonth = source.LateEarlyRules.MaxLateAllowedPerMonth,
				PenaltyType = source.LateEarlyRules.PenaltyType,
				PenaltyCountThreshold = source.LateEarlyRules.PenaltyCountThreshold,
				EarlyExitEnabled = source.LateEarlyRules.EarlyExitEnabled,
				EarlyExitGraceTime = source.LateEarlyRules.EarlyExitGraceTime
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = source.Overtime.Enabled,
				Rate = source.Overtime.Rate,
				MinOvertimeMinutes = source.Overtime.MinOvertimeMinutes
			},
			HolidayRules = new AttendanceHolidayRulesRequest
			{
				WorkOnHolidayBehavior = source.HolidayRules.WorkOnHolidayBehavior,
				SandwichRule = source.HolidayRules.SandwichRule
			},
			ManualCorrectionRules = new AttendanceManualCorrectionRulesRequest
			{
				Allowed = source.ManualCorrectionRules.Allowed
			},
			PayrollPreviewEnabled = source.PayrollPreviewEnabled,
			PayrollIntegrationMode = source.PayrollIntegrationMode,
			RequirePublishedAttendanceForPayrollLock = source.RequirePublishedAttendanceForPayrollLock,
			Overrides = source.Overrides
		};

	private static decimal GetBaselinePayableUnits(EmployeeAttendanceStatusType statusType) => statusType switch
	{
		EmployeeAttendanceStatusType.Present => 1m,
		EmployeeAttendanceStatusType.HalfDayLeave => 0.5m,
		_ => 0m
	};

	private static decimal GetProposedPayableUnits(EmployeeAttendance attendance, AttendanceConfigurationRequest configuration)
	{
		if (!attendance.TimeIn.HasValue || !attendance.TimeOut.HasValue)
		{
			return ParseMissingPunchBehavior(configuration.PunchRules.MissingPunchBehavior) switch
			{
				AttendanceMissingPunchBehavior.MarkAbsent => 0m,
				AttendanceMissingPunchBehavior.MarkHalfDay => 0.5m,
				_ => GetBaselinePayableUnits(attendance.StatusType)
			};
		}

		var workedHours = Math.Max(0d, (attendance.TimeOut.Value - attendance.TimeIn.Value).TotalHours);
		var workedPercent = workedHours / DefaultExpectedWorkHours * 100d;

		if (workedPercent >= ResolveFullDayThreshold(configuration))
		{
			return 1m;
		}

		if (workedPercent >= configuration.PunchRules.HalfDayThresholdPercentage)
		{
			return 0.5m;
		}

		return 0m;
	}

	private static bool IsLate(EmployeeAttendance attendance, int graceMinutes)
	{
		if (!attendance.TimeIn.HasValue)
		{
			return false;
		}

		var expectedStart = DefaultShiftStartTime.AddMinutes(Math.Max(0, graceMinutes));
		return TimeOnly.FromDateTime(attendance.TimeIn.Value.LocalDateTime) > expectedStart;
	}

	private static decimal GetLatePenaltyUnits(AttendanceConfigurationRequest configuration, int employeeLateCountInMonth)
	{
		if (configuration.LateEarlyRules.PenaltyType == "none" ||
			employeeLateCountInMonth <= configuration.LateEarlyRules.PenaltyCountThreshold)
		{
			return 0m;
		}

		return configuration.LateEarlyRules.PenaltyType switch
		{
			"deduct_full_day" => 1m,
			"deduct_half_day" => 0.5m,
			_ => 0m
		};
	}

	private static int GetOvertimeMinutes(EmployeeAttendance attendance, AttendanceConfigurationRequest configuration)
	{
		if (!configuration.Overtime.Enabled || !attendance.TimeIn.HasValue || !attendance.TimeOut.HasValue)
		{
			return 0;
		}

		var overtimeMinutes = (int)Math.Floor(Math.Max(0d, (attendance.TimeOut.Value - attendance.TimeIn.Value).TotalMinutes - DefaultExpectedWorkHours * 60d));
		var minimum = Math.Max(configuration.Overtime.MinOvertimeMinutes ?? 0, 0);
		return overtimeMinutes >= minimum ? overtimeMinutes : 0;
	}

	private static AttendanceConfigurationStatus ParseStatus(string value) => value switch
	{
		"active" => AttendanceConfigurationStatus.Active,
		_ => AttendanceConfigurationStatus.Draft
	};

	private static AttendancePenaltyType ParsePenaltyType(string value) => value switch
	{
		"deduct_half_day" => AttendancePenaltyType.DeductHalfDay,
		"deduct_full_day" => AttendancePenaltyType.DeductFullDay,
		_ => AttendancePenaltyType.None
	};

	private static AttendanceOvertimeRate? ParseOvertimeRate(string? value) => value switch
	{
		"1.5x" => AttendanceOvertimeRate.OnePointFiveX,
		"2.0x" => AttendanceOvertimeRate.TwoX,
		"flat" => AttendanceOvertimeRate.Flat,
		_ => null
	};

	private static AttendanceWeeklyOffPattern ParseWeeklyOffPattern(string value) => value switch
	{
		"sunday" => AttendanceWeeklyOffPattern.Sunday,
		"custom" => AttendanceWeeklyOffPattern.Custom,
		_ => AttendanceWeeklyOffPattern.SaturdaySunday
	};

	private static AttendanceMissingPunchBehavior ParseMissingPunchBehavior(string value) => value switch
	{
		"mark_absent" => AttendanceMissingPunchBehavior.MarkAbsent,
		"mark_half_day" => AttendanceMissingPunchBehavior.MarkHalfDay,
		_ => AttendanceMissingPunchBehavior.RequiresReview
	};

	private static AttendanceHolidayWorkBehavior ParseHolidayWorkBehavior(string value) => value switch
	{
		"paid" => AttendanceHolidayWorkBehavior.Paid,
		"ot_payout" => AttendanceHolidayWorkBehavior.OvertimePayout,
		_ => AttendanceHolidayWorkBehavior.CompOff
	};

	private static AttendancePolicyOverrideTargetType ParseOverrideTargetType(string value) => value switch
	{
		"role" => AttendancePolicyOverrideTargetType.Role,
		"employee_type" => AttendancePolicyOverrideTargetType.EmployeeType,
		"shift" => AttendancePolicyOverrideTargetType.Shift,
		_ => AttendancePolicyOverrideTargetType.Department
	};

	private static AttendancePayrollIntegrationMode ParsePayrollIntegrationMode(string value) => value switch
	{
		"approved_results" => AttendancePayrollIntegrationMode.ApprovedResults,
		_ => AttendancePayrollIntegrationMode.Advisory
	};

	private static string MapStatus(AttendanceConfigurationStatus value) => value == AttendanceConfigurationStatus.Active ? "active" : "draft";
	private static string MapPayrollIntegrationMode(AttendancePayrollIntegrationMode value) =>
		value == AttendancePayrollIntegrationMode.ApprovedResults ? "approved_results" : "advisory";

	private static string MapEvaluationStatus(AttendancePolicyEvaluationRunStatus value) => value switch
	{
		AttendancePolicyEvaluationRunStatus.Evaluated => "evaluated",
		AttendancePolicyEvaluationRunStatus.NeedsReview => "needs_review",
		AttendancePolicyEvaluationRunStatus.Approved => "approved",
		AttendancePolicyEvaluationRunStatus.Published => "published",
		AttendancePolicyEvaluationRunStatus.Superseded => "superseded",
		AttendancePolicyEvaluationRunStatus.Cancelled => "cancelled",
		_ => "draft"
	};

	private static string MapPenaltyType(AttendancePenaltyType value) => value switch
	{
		AttendancePenaltyType.DeductHalfDay => "deduct_half_day",
		AttendancePenaltyType.DeductFullDay => "deduct_full_day",
		_ => "none"
	};

	private static string? MapOvertimeRate(AttendanceOvertimeRate? value) => value switch
	{
		AttendanceOvertimeRate.OnePointFiveX => "1.5x",
		AttendanceOvertimeRate.TwoX => "2.0x",
		AttendanceOvertimeRate.Flat => "flat",
		_ => null
	};

	private static string MapWeeklyOffPattern(AttendanceWeeklyOffPattern value) => value switch
	{
		AttendanceWeeklyOffPattern.Sunday => "sunday",
		AttendanceWeeklyOffPattern.Custom => "custom",
		_ => "saturday_sunday"
	};

	private static string MapMissingPunchBehavior(AttendanceMissingPunchBehavior value) => value switch
	{
		AttendanceMissingPunchBehavior.MarkAbsent => "mark_absent",
		AttendanceMissingPunchBehavior.MarkHalfDay => "mark_half_day",
		_ => "requires_review"
	};

	private static string MapHolidayWorkBehavior(AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceHolidayWorkBehavior.OvertimePayout => "ot_payout",
		_ => "comp_off"
	};

	private static string MapOverrideTargetType(AttendancePolicyOverrideTargetType value) => value switch
	{
		AttendancePolicyOverrideTargetType.Role => "role",
		AttendancePolicyOverrideTargetType.EmployeeType => "employee_type",
		AttendancePolicyOverrideTargetType.Shift => "shift",
		_ => "department"
	};

	private static List<int> MapCustomWeeklyOffs(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? []
			: value.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => int.TryParse(x, out var day) ? day : -1)
				.Where(x => x >= 0)
				.ToList();

	private static AttendanceConfigurationResponse MapToResponse(AttendanceConfiguration entity, string message) =>
		new()
		{
			Success = true,
			Message = message,
			PolicyName = entity.PolicyName,
			Status = MapStatus(entity.Status),
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveTo = entity.EffectiveTo,
			LastUpdatedAt = entity.LastModificationTime ?? entity.CreationTime,
			WorkdayRules = new AttendanceWorkdayRulesRequest
			{
				WeeklyOffPattern = MapWeeklyOffPattern(entity.WeeklyOffPattern),
				CustomWeeklyOffs = MapCustomWeeklyOffs(entity.CustomWeeklyOffs)
			},
			PunchRules = new AttendancePunchRulesRequest
			{
				FullDayThresholdPercentage = entity.HoursBasedPayPercentage,
				HalfDayThresholdPercentage = entity.HalfDayPayPercentage,
				AbsentThresholdPercentage = entity.AbsentPayPercentage,
				MissingPunchBehavior = MapMissingPunchBehavior(entity.MissingPunchBehavior)
			},
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = entity.HoursBasedPayPercentage
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = entity.GraceTime,
				MaxLateAllowedPerMonth = entity.MaxLateAllowedPerMonth,
				PenaltyType = MapPenaltyType(entity.PenaltyType),
				PenaltyCountThreshold = entity.PenaltyCountThreshold,
				EarlyExitEnabled = entity.EarlyExitEnabled,
				EarlyExitGraceTime = entity.EarlyExitGraceTime
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = entity.OvertimeEnabled,
				Rate = MapOvertimeRate(entity.OvertimeRate),
				MinOvertimeMinutes = entity.MinOvertimeMinutes
			},
			HolidayRules = new AttendanceHolidayRulesRequest
			{
				WorkOnHolidayBehavior = MapHolidayWorkBehavior(entity.HolidayWorkBehavior),
				SandwichRule = entity.SandwichRuleEnabled
			},
			ManualCorrectionRules = new AttendanceManualCorrectionRulesRequest
			{
				Allowed = entity.ManualCorrectionAllowed
			},
			PayrollPreviewEnabled = entity.PayrollPreviewEnabled,
			PayrollIntegrationMode = MapPayrollIntegrationMode(entity.PayrollIntegrationMode),
			RequirePublishedAttendanceForPayrollLock = entity.RequirePublishedAttendanceForPayrollLock,
			Overrides = entity.Overrides
				.OrderBy(x => x.Priority)
				.Select(x => new AttendancePolicyOverrideResponse
				{
					Id = x.Id,
					TargetType = MapOverrideTargetType(x.TargetType),
					TargetId = x.TargetId,
					TargetName = x.TargetName,
					IsActive = x.IsActive,
					Priority = x.Priority,
					HoursBasedPayPercentage = x.HoursBasedPayPercentage,
					GraceTime = x.GraceTime,
					MaxLateAllowedPerMonth = x.MaxLateAllowedPerMonth,
					PenaltyType = x.PenaltyType.HasValue ? MapPenaltyType(x.PenaltyType.Value) : null,
					OvertimeEnabled = x.OvertimeEnabled,
					OvertimeRate = MapOvertimeRate(x.OvertimeRate),
					MinOvertimeMinutes = x.MinOvertimeMinutes
				})
				.ToList()
		};
}
