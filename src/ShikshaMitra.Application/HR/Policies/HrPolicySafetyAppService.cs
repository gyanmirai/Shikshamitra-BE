using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.AuditLogging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicySafetyAppService(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IGroupRepository groupRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<AuditLog, Guid> auditLogRepository,
	IRepository<HrAuditEvent, Guid> hrAuditEventRepository,
	HrPolicyImpactConfirmationService confirmationService)
	: ApplicationService, IHrPolicySafetyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public async Task<HrPolicyImpactPreviewDto> PreviewPolicyImpactAsync(HrPolicyImpactPreviewRequestDto input)
	{
		var policyType = NormalizePolicyType(input.PolicyType);
		var academicYearId = input.AcademicYearId ?? await ResolveAcademicYearIdAsync(policyType, input.PolicyId, input);
		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = policyType,
			PolicyId = input.PolicyId,
			AcademicYearId = academicYearId,
			Action = string.IsNullOrWhiteSpace(input.Action) ? "update" : input.Action.Trim().ToLowerInvariant()
		};

		await FillCurrentVersionAsync(preview);
		preview.ProposedVersion = preview.CurrentVersion.HasValue ? preview.CurrentVersion.Value + 1 : 1;
		var workCalendarRefs = await ResolveWorkCalendarReferencesAsync(policyType, input.PolicyId, academicYearId);

		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var assignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(x => x.AcademicYearId == academicYearId && x.IsActive));
		var affectedAssignments = assignments.Where(x => IsAssignmentAffected(x, policyType, input.PolicyId, workCalendarRefs)).ToList();
		preview.AffectedAssignmentCount = affectedAssignments.Count;

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Take(500));
		var affectedEmployees = await ResolveAffectedEmployeesAsync(employees, affectedAssignments);
		if (affectedEmployees.Count == 0 && preview.Action is "set-default" or "archive")
		{
			affectedEmployees = employees.Take(25).ToList();
		}
		preview.AffectedEmployeeCount = affectedEmployees.Count;
		preview.AffectedEmployees = affectedEmployees.Take(25).Select(ToAffectedEmployeeDto).ToList();

		await FillPayrollRiskAsync(preview, policyType, input.PolicyId);
		AddWorkCalendarReferenceWarning(preview, workCalendarRefs);
		AddWarnings(preview);
		await confirmationService.AttachTokenAsync(preview);
		return preview;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit)]
	public async Task<HrPolicyAuditSummaryDto> GetAuditSummaryAsync(string policyType, Guid id)
	{
		var normalized = NormalizePolicyType(policyType);
		if (normalized == "work_calendar")
		{
			var entity = await workCalendarPolicyRepository.GetAsync(id);
			return await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
		}
		if (normalized == "payroll")
		{
			var entity = await payrollPolicyRepository.GetAsync(id);
			var summary = await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
			summary.LatestAssignmentReason = await GetLatestAssignmentReasonAsync(x => x.PayrollPolicyId == id);
			return summary;
		}
		if (normalized == "attendance")
		{
			var entity = await attendancePolicyRepository.GetAsync(id);
			var summary = await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
			summary.LatestAssignmentReason = await GetLatestAssignmentReasonAsync(x => x.AttendancePolicyId == id);
			return summary;
		}

		throw new BusinessException("ShikshaMitra:InvalidPolicyType");
	}

	private async Task<Guid> ResolveAcademicYearIdAsync(string policyType, Guid? policyId, HrPolicyImpactPreviewRequestDto input)
	{
		if (policyType == "work_calendar" && input.WorkCalendarPolicy?.AcademicYearId is Guid calendarYear && calendarYear != Guid.Empty)
		{
			return calendarYear;
		}
		if (policyType == "payroll" && input.PayrollPolicy?.AcademicYearId is Guid payrollYear && payrollYear != Guid.Empty)
		{
			return payrollYear;
		}
		if (policyType == "attendance" && input.AttendancePolicy?.AcademicYearId is Guid attendanceYear && attendanceYear != Guid.Empty)
		{
			return attendanceYear;
		}
		if (!policyId.HasValue)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearRequiredForPolicyImpact");
		}

		return policyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"payroll" => (await payrollPolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			"attendance" => (await attendancePolicyRepository.GetAsync(policyId.Value)).AcademicYearId,
			_ => throw new BusinessException("ShikshaMitra:InvalidPolicyType")
		};
	}

	private async Task FillCurrentVersionAsync(HrPolicyImpactPreviewDto preview)
	{
		if (!preview.PolicyId.HasValue)
		{
			return;
		}

		preview.CurrentVersion = preview.PolicyType switch
		{
			"work_calendar" => (await workCalendarPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"payroll" => (await payrollPolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			"attendance" => (await attendancePolicyRepository.GetAsync(preview.PolicyId.Value)).Version,
			_ => null
		};
	}

	private async Task<WorkCalendarPolicyReferences> ResolveWorkCalendarReferencesAsync(string policyType, Guid? policyId, Guid academicYearId)
	{
		if (policyType != "work_calendar" || !policyId.HasValue)
		{
			return WorkCalendarPolicyReferences.Empty;
		}

		var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
		var payrollPolicies = await AsyncExecuter.ToListAsync(payrollQuery.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.WorkCalendarPolicyId == policyId.Value &&
			x.Status == HrPolicyStatus.Active));
		var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
		var attendancePolicies = await AsyncExecuter.ToListAsync(attendanceQuery.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.WorkCalendarPolicyId == policyId.Value &&
			x.Status == HrPolicyStatus.Active));

		return new WorkCalendarPolicyReferences(
			payrollPolicies.Select(x => x.Id).ToHashSet(),
			attendancePolicies.Select(x => x.Id).ToHashSet(),
			payrollPolicies.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(5).ToList(),
			attendancePolicies.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(5).ToList());
	}

	private static bool IsAssignmentAffected(HrPolicyAssignment assignment, string policyType, Guid? policyId, WorkCalendarPolicyReferences workCalendarRefs) =>
		!policyId.HasValue ||
		(policyType == "payroll" && assignment.PayrollPolicyId == policyId.Value) ||
		(policyType == "attendance" && assignment.AttendancePolicyId == policyId.Value) ||
		(policyType == "work_calendar" &&
			(workCalendarRefs.PayrollPolicyIds.Contains(assignment.PayrollPolicyId) ||
				workCalendarRefs.AttendancePolicyIds.Contains(assignment.AttendancePolicyId)));

	private async Task<List<Employee>> ResolveAffectedEmployeesAsync(IReadOnlyList<Employee> employees, IReadOnlyList<HrPolicyAssignment> assignments)
	{
		var result = new List<Employee>();
		var groupIds = assignments
			.Where(assignment => assignment.TargetType == HrPolicyAssignmentTargetType.Group && Guid.TryParse(assignment.TargetKey, out _))
			.Select(assignment => Guid.Parse(assignment.TargetKey))
			.Distinct()
			.ToList();
		var groups = new Dictionary<Guid, Group>();
		foreach (var groupId in groupIds)
		{
			var group = await groupRepository.GetWithDetailsAsync(groupId);
			if (group != null)
			{
				groups[groupId] = group;
			}
		}

		foreach (var employee in employees)
		{
			if (assignments.Any(assignment => Matches(employee, assignment, groups)))
			{
				result.Add(employee);
			}
		}

		return result;
	}

	private static bool Matches(Employee employee, HrPolicyAssignment assignment, IReadOnlyDictionary<Guid, Group> groups) => assignment.TargetType switch
	{
		HrPolicyAssignmentTargetType.Employee => employee.Id.ToString("D").Equals(assignment.TargetKey, StringComparison.OrdinalIgnoreCase),
		HrPolicyAssignmentTargetType.Group when Guid.TryParse(assignment.TargetKey, out var groupId) && groups.TryGetValue(groupId, out var group) =>
			HrPolicyGroupMembership.MatchesEmployee(group, employee),
		_ => false
	};

	private async Task FillPayrollRiskAsync(HrPolicyImpactPreviewDto preview, string policyType, Guid? policyId)
	{
		if (!policyId.HasValue)
		{
			return;
		}

		var query = await payrollSnapshotRepository.GetQueryableAsync();
		var snapshots = await AsyncExecuter.ToListAsync(query.Where(x =>
			(policyType == "payroll" && x.PayrollPolicyId == policyId.Value) ||
			(policyType == "attendance" && x.AttendancePolicyId == policyId.Value) ||
			(policyType == "work_calendar" && x.WorkCalendarPolicyId == policyId.Value)));

		preview.AffectedPayrollMonths = snapshots
			.GroupBy(x => x.MonthYear)
			.Select(x => new HrPolicyAffectedPeriodDto { Label = x.Key, Count = x.Count(), Status = "snapshot" })
			.Take(12)
			.ToList();
		preview.LockedPayrollMonths = snapshots
			.Where(x => x.IsLocked)
			.GroupBy(x => x.MonthYear)
			.Select(x => new HrPolicyAffectedPeriodDto { Label = x.Key, Count = x.Count(), Status = "locked" })
			.Take(12)
			.ToList();
		preview.HasStalePayrollRisk = preview.LockedPayrollMonths.Count > 0;
	}

	private static void AddWorkCalendarReferenceWarning(HrPolicyImpactPreviewDto preview, WorkCalendarPolicyReferences refs)
	{
		if (preview.PolicyType != "work_calendar" || refs.IsEmpty)
		{
			return;
		}

		var referencedPolicies = new List<string>();
		if (refs.PayrollPolicyNames.Count > 0)
		{
			referencedPolicies.Add($"payroll: {string.Join(", ", refs.PayrollPolicyNames)}");
		}
		if (refs.AttendancePolicyNames.Count > 0)
		{
			referencedPolicies.Add($"attendance: {string.Join(", ", refs.AttendancePolicyNames)}");
		}

		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "warning",
			Code = "ShikshaMitra:WorkCalendarPolicyReferenced",
			Message = $"This work calendar is referenced by active policies ({string.Join("; ", referencedPolicies)})."
		});
	}

	private static void AddWarnings(HrPolicyImpactPreviewDto preview)
	{
		if (preview.LockedPayrollMonths.Count > 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "blocking",
				Code = "ShikshaMitra:LockedPayrollImpact",
				Message = "This change affects locked payroll months. Review recalculation and audit requirements before saving."
			});
		}
		if (preview.AffectedEmployeeCount > 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "info",
				Code = "ShikshaMitra:AffectedEmployees",
				Message = $"{preview.AffectedEmployeeCount} employees may be affected."
			});
		}
	}

	private static HrPolicyAffectedEmployeeDto ToAffectedEmployeeDto(Employee employee) => new()
	{
		EmployeeId = employee.Id,
		EmployeeCode = employee.EmployeeCode,
		EmployeeName = employee.EmployeeName,
		Department = employee.Department,
		RoleId = employee.RoleId,
		ShiftId = employee.ShiftId,
		EmployeeType = employee.EmployeeGroup?.ToString()
	};

	private async Task<string?> GetLatestAssignmentReasonAsync(System.Linq.Expressions.Expression<Func<HrPolicyAssignment, bool>> predicate)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return (await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(predicate)
			.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)))?.AssignmentReason;
	}

	private async Task<HrPolicyAuditSummaryDto> BuildAuditSummaryAsync(
		string policyType,
		Guid id,
		string name,
		int version,
		FullAuditedAggregateRoot<Guid> entity)
	{
		var userIds = new[] { entity.CreatorId, entity.LastModifierId }
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		var users = userIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => userIds.Contains(x.Id))))
				.ToDictionary(x => x.Id, x => x.Name ?? x.UserName);
		var events = await BuildAuditEventsAsync(id, policyType, entity);
		return new HrPolicyAuditSummaryDto
		{
			PolicyType = policyType,
			PolicyId = id,
			Name = name,
			Version = version,
			CreationTime = entity.CreationTime,
			CreatorId = entity.CreatorId,
			CreatedByName = entity.CreatorId.HasValue ? users.GetValueOrDefault(entity.CreatorId.Value) : null,
			LastModificationTime = entity.LastModificationTime,
			LastModifierId = entity.LastModifierId,
			LastModifiedByName = entity.LastModifierId.HasValue ? users.GetValueOrDefault(entity.LastModifierId.Value) : null,
			LastDefaultSetSummary = "Default changes are available in the ABP audit log.",
			Events = events
		};
	}

	private async Task<List<HrPolicyAuditEventDto>> BuildAuditEventsAsync(
		Guid id,
		string policyType,
		FullAuditedAggregateRoot<Guid> entity)
	{
		var entityType = policyType switch
		{
			"work_calendar" => "work_calendar_policy",
			"payroll" => "payroll_policy",
			"attendance" => "attendance_policy",
			_ => policyType
		};
		var hrAuditQuery = await hrAuditEventRepository.GetQueryableAsync();
		var hrAuditEvents = await AsyncExecuter.ToListAsync(hrAuditQuery
			.Where(x => x.EntityType == entityType && x.EntityId == id)
			.OrderByDescending(x => x.OccurredAt)
			.Take(50));
		if (hrAuditEvents.Count > 0)
		{
			return hrAuditEvents.Select(x => new HrPolicyAuditEventDto
			{
				Timestamp = x.OccurredAt,
				ActorId = x.ActorUserId,
				ActorName = x.ActorNameSnapshot,
				Action = x.Action,
				SourceMethod = "HrAuditEvent",
				ChangedFieldsSummary = BuildHrAuditChangedFieldsSummary(x)
			}).ToList();
		}

		var query = await auditLogRepository.GetQueryableAsync();
		var logs = await AsyncExecuter.ToListAsync(query
			.Where(x => x.ExecutionTime >= entity.CreationTime.AddMinutes(-1))
			.OrderByDescending(x => x.ExecutionTime)
			.Take(50));
		var userIds = logs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
		var users = userIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => userIds.Contains(x.Id))))
				.ToDictionary(x => x.Id, x => x.Name ?? x.UserName);
		var events = logs
			.SelectMany(log => (log.Actions ?? []).Select(action => new { log, action }))
			.Where(x => IsPolicyAuditAction(x.action.MethodName, policyType))
			.Take(20)
			.Select(x => new HrPolicyAuditEventDto
			{
				Timestamp = x.log.ExecutionTime,
				ActorId = x.log.UserId,
				ActorName = x.log.UserId.HasValue ? users.GetValueOrDefault(x.log.UserId.Value) : null,
				Action = MapAuditAction(x.action.MethodName),
				SourceMethod = x.action.MethodName,
				ChangedFieldsSummary = $"Policy {id:D}"
			})
			.ToList();
		events.Add(new HrPolicyAuditEventDto
		{
			Timestamp = entity.CreationTime,
			ActorId = entity.CreatorId,
			ActorName = entity.CreatorId.HasValue ? users.GetValueOrDefault(entity.CreatorId.Value) : null,
			Action = "created",
			ChangedFieldsSummary = $"Created {policyType} policy"
		});
		return events.OrderByDescending(x => x.Timestamp).ToList();
	}

	private static string? BuildHrAuditChangedFieldsSummary(HrAuditEvent auditEvent)
	{
		if (string.IsNullOrWhiteSpace(auditEvent.ChangedFieldsJson))
		{
			return auditEvent.Summary;
		}

		try
		{
			var fields = System.Text.Json.JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(auditEvent.ChangedFieldsJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
			return fields.Count == 0
				? auditEvent.Summary
				: string.Join(", ", fields.Take(6).Select(x => $"{x.Field}: {x.OldValue} -> {x.NewValue}"));
		}
		catch
		{
			return auditEvent.Summary;
		}
	}

	private static bool IsPolicyAuditAction(string? methodName, string policyType)
	{
		if (string.IsNullOrWhiteSpace(methodName))
		{
			return false;
		}
		var method = methodName.ToLowerInvariant();
		return method.Contains("policy") && (method.Contains("update") || method.Contains("clone") || method.Contains("delete") || method.Contains("setdefault") || method.Contains("create"));
	}

	private static string MapAuditAction(string? methodName)
	{
		var method = methodName?.ToLowerInvariant() ?? string.Empty;
		if (method.Contains("clone")) return "cloned";
		if (method.Contains("delete")) return "archived";
		if (method.Contains("setdefault")) return "set_default";
		if (method.Contains("update")) return "edited";
		if (method.Contains("create")) return "created";
		return "changed";
	}

	private static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" => "work_calendar",
		"payroll" => "payroll",
		"attendance" => "attendance",
		_ => throw new BusinessException("ShikshaMitra:InvalidPolicyType")
	};

	private sealed record WorkCalendarPolicyReferences(
		HashSet<Guid> PayrollPolicyIds,
		HashSet<Guid> AttendancePolicyIds,
		List<string> PayrollPolicyNames,
		List<string> AttendancePolicyNames)
	{
		public static WorkCalendarPolicyReferences Empty { get; } = new([], [], [], []);
		public bool IsEmpty => PayrollPolicyIds.Count == 0 && AttendancePolicyIds.Count == 0;
	}
}
