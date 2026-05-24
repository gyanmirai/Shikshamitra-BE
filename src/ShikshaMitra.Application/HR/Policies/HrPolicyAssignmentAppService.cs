using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicyAssignmentAppService(
	IRepository<HrPolicyAssignment, Guid> repository,
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	HrPolicyResolver policyResolver,
	HrPolicyImpactConfirmationService confirmationService,
	HrAuditWriter auditWriter)
	: ApplicationService, IHrPolicyAssignmentAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<HrPolicyAssignmentDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.TargetType))
		{
			var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
			query = query.Where(x => x.TargetType == targetType);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.TargetKey.Contains(search) || (x.TargetDisplayName != null && x.TargetDisplayName.ToLower().Contains(search)));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsActive)
			.ThenBy(x => x.TargetType)
			.ThenBy(x => x.TargetKey)
			.ThenBy(x => x.EffectiveFrom)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var dtos = rows.Select(HrPolicyMapping.ToDto).ToList();
		await EnrichTargetOrganizationsAsync(dtos);

		return new PagedResultDto<HrPolicyAssignmentDto>(total, dtos);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<HrPolicyAssignmentDto> GetAsync(Guid id)
	{
		var dto = HrPolicyMapping.ToDto(await repository.GetAsync(id));
		await EnrichTargetOrganizationsAsync([dto]);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public async Task<HrPolicyAssignmentDto> CreateAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.TargetKey);
		await ValidateReferencesAsync(input);
		await ValidateTargetAsync(targetType, targetKey);
		await EnsureNoOverlapAsync(targetType, targetKey, input.AcademicYearId, input.EffectiveFrom, input.EffectiveTo, null, input.IsActive);
		await EnsureAssignmentImpactConfirmedAsync("create", null, input);

		var entity = new HrPolicyAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			targetType,
			targetKey,
			input.TargetDisplayName,
			input.AttendancePolicyId,
			input.PayrollPolicyId,
			input.EffectiveFrom,
			input.EffectiveTo,
			input.IsActive,
			input.Priority,
			input.AssignmentReason);

		await repository.InsertAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			entity.IsActive ? "assigned" : "unassigned",
			$"Created {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			context: BuildAssignmentAuditContext(entity));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public async Task<HrPolicyAssignmentDto> UpdateAsync(Guid id, CreateUpdateHrPolicyAssignmentDto input)
	{
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.TargetKey);
		await ValidateReferencesAsync(input);
		await ValidateTargetAsync(targetType, targetKey);
		await EnsureNoOverlapAsync(targetType, targetKey, input.AcademicYearId, input.EffectiveFrom, input.EffectiveTo, id, input.IsActive);
		await EnsureAssignmentImpactConfirmedAsync("update", id, input);

		var entity = await repository.GetAsync(id);
		var before = HrPolicyMapping.ToDto(entity);
		entity.UpdateTarget(targetType, targetKey, input.TargetDisplayName);
		entity.Update(input.AttendancePolicyId, input.PayrollPolicyId, input.EffectiveFrom, input.EffectiveTo, input.IsActive, input.Priority, input.AssignmentReason);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			entity.IsActive ? "assigned" : "unassigned",
			$"Updated {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(entity));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public async Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync(UpsertEmployeePolicyOverrideDto input)
	{
		var employee = await employeeRepository.GetAsync(input.EmployeeId);
		var targetType = HrPolicyAssignmentTargetType.Employee;
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.EmployeeId.ToString("D"));
		var effectiveFrom = input.EffectiveFrom.Date;
		var effectiveTo = input.EffectiveTo?.Date;
		var payload = new CreateUpdateHrPolicyAssignmentDto
		{
			AcademicYearId = input.AcademicYearId,
			TargetType = HrPolicyMapping.MapAssignmentTargetType(targetType),
			TargetKey = targetKey,
			TargetDisplayName = employee.EmployeeName,
			AttendancePolicyId = input.AttendancePolicyId,
			PayrollPolicyId = input.PayrollPolicyId,
			EffectiveFrom = effectiveFrom,
			EffectiveTo = effectiveTo,
			IsActive = true,
			Priority = 100,
			AssignmentReason = string.IsNullOrWhiteSpace(input.AssignmentReason)
				? "Employee payroll step override"
				: input.AssignmentReason,
			ConfirmationToken = input.ConfirmationToken
		};

		await ValidateReferencesAsync(payload);
		var query = await repository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(x =>
				x.AcademicYearId == input.AcademicYearId &&
				x.TargetType == targetType &&
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= (effectiveTo ?? DateTime.MaxValue) &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom));

		if (existing == null)
		{
			return await CreateAsync(payload);
		}

		await EnsureNoOverlapAsync(targetType, targetKey, payload.AcademicYearId, payload.EffectiveFrom, payload.EffectiveTo, existing.Id, true);
		await EnsureAssignmentImpactConfirmedAsync("update", existing.Id, payload);

		var before = HrPolicyMapping.ToDto(existing);
		existing.Update(payload.AttendancePolicyId, payload.PayrollPolicyId, payload.EffectiveFrom, payload.EffectiveTo, true, payload.Priority, payload.AssignmentReason);
		existing.UpdateTarget(targetType, targetKey, employee.EmployeeName);
		await repository.UpdateAsync(existing, autoSave: true);
		var dto = HrPolicyMapping.ToDto(existing);
		await auditWriter.WriteAsync(
			existing.AcademicYearId,
			"policy_assignment",
			existing.Id,
			BuildAssignmentAuditName(existing),
			"assigned",
			$"Upserted direct employee policy override for {employee.EmployeeName}.",
			existing.AssignmentReason,
			HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(existing));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public async Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input)
	{
		var assignment = input.Assignment;
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(assignment.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, assignment.TargetKey);
		await ValidateReferencesAsync(assignment);
		await ValidateTargetAsync(targetType, targetKey);

		var preview = new HrPolicyImpactPreviewDto
		{
			PolicyType = "assignment",
			PolicyId = input.AssignmentId,
			AcademicYearId = assignment.AcademicYearId,
			Action = input.Action,
			ProposedVersion = input.AssignmentId.HasValue ? 2 : 1
		};

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		preview.AffectedEmployees = await BuildAffectedEmployeesAsync(employeeQuery, targetType, targetKey);
		preview.AffectedEmployeeCount = preview.AffectedEmployees.Count;
		preview.AffectedAssignmentCount = 1;
		preview.EffectiveAssignmentConflicts = await BuildEffectiveAssignmentConflictsAsync(
			targetType,
			targetKey,
			assignment,
			preview.AffectedEmployees);

		try
		{
			await EnsureNoOverlapAsync(targetType, targetKey, assignment.AcademicYearId, assignment.EffectiveFrom, assignment.EffectiveTo, input.AssignmentId, assignment.IsActive);
		}
		catch (BusinessException ex) when (ex.Code == "ShikshaMitra:HrPolicyAssignmentOverlap")
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "blocking",
				Code = ex.Code,
				Message = "This assignment overlaps an existing active assignment for the same target and date range."
			});
		}

		if (preview.AffectedEmployeeCount == 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "warning",
				Code = "ShikshaMitra:NoAffectedEmployees",
				Message = "No active employees currently match this assignment target."
			});
		}

		await confirmationService.AttachTokenAsync(preview);
		return preview;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		await EnsureAssignmentImpactConfirmedAsync("delete", id, new CreateUpdateHrPolicyAssignmentDto
		{
			AcademicYearId = entity.AcademicYearId,
			TargetType = HrPolicyMapping.MapAssignmentTargetType(entity.TargetType),
			TargetKey = entity.TargetKey,
			TargetDisplayName = entity.TargetDisplayName,
			AttendancePolicyId = entity.AttendancePolicyId,
			PayrollPolicyId = entity.PayrollPolicyId,
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveTo = entity.EffectiveTo,
			IsActive = false,
			Priority = entity.Priority,
			AssignmentReason = entity.AssignmentReason
		});
		var before = HrPolicyMapping.ToDto(entity);
		entity.Update(entity.AttendancePolicyId, entity.PayrollPolicyId, entity.EffectiveFrom, entity.EffectiveTo, false, entity.Priority, entity.AssignmentReason);
		await repository.UpdateAsync(entity, autoSave: true);
		await auditWriter.WriteAsync(
			entity.AcademicYearId,
			"policy_assignment",
			entity.Id,
			BuildAssignmentAuditName(entity),
			"unassigned",
			$"Deactivated {HrPolicyMapping.MapAssignmentTargetType(entity.TargetType)} policy assignment for {entity.TargetDisplayName ?? entity.TargetKey}.",
			entity.AssignmentReason,
			HrAuditWriter.Diff(before, HrPolicyMapping.ToDto(entity), "lastModificationTime", "lastModifierId"),
			BuildAssignmentAuditContext(entity));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<EffectiveEmployeePoliciesDto> GetEffectivePoliciesAsync(Guid employeeId, DateTime date)
	{
		var resolved = await policyResolver.ResolveAsync(employeeId, date);
		return ToEffectivePoliciesDto(resolved);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<EffectiveEmployeePoliciesDto> GetPreviewAsync(PolicyAssignmentPreviewRequestDto input)
	{
		var resolved = await policyResolver.ResolvePreviewAsync(
			input.EmployeeId,
			input.GroupId,
			input.Date,
			input.AcademicYearId);

		return ToEffectivePoliciesDto(resolved);
	}

	private async Task EnsureAssignmentImpactConfirmedAsync(string action, Guid? assignmentId, CreateUpdateHrPolicyAssignmentDto input)
	{
		var preview = await PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = action,
			AssignmentId = assignmentId,
			Assignment = input
		});
		await confirmationService.EnsureConfirmedAsync(preview, input.ConfirmationToken);
	}

	private async Task ValidateReferencesAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var attendancePolicy = await attendancePolicyRepository.GetAsync(input.AttendancePolicyId);
		var payrollPolicy = await payrollPolicyRepository.GetAsync(input.PayrollPolicyId);

		if (attendancePolicy.AcademicYearId != input.AcademicYearId || payrollPolicy.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
		}

		HrPolicyMapping.EnsureActive(attendancePolicy.Status);
		HrPolicyMapping.EnsureActive(payrollPolicy.Status);
	}

	private async Task ValidateTargetAsync(HrPolicyAssignmentTargetType targetType, string targetKey)
	{
		switch (targetType)
		{
			case HrPolicyAssignmentTargetType.Employee:
				if (!Guid.TryParse(targetKey, out var employeeId))
				{
					throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetInvalid");
				}

				if (!await employeeRepository.AnyAsync(x => x.Id == employeeId))
				{
					throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetInvalid");
				}
				break;

			case HrPolicyAssignmentTargetType.Group:
				if (!Guid.TryParse(targetKey, out var groupId))
				{
					throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetInvalid");
				}

				if (!await groupRepository.AnyAsync(x => x.Id == groupId))
				{
					throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetInvalid");
				}
				break;
		}
	}

	private async Task EnsureNoOverlapAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		Guid academicYearId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		Guid? excludeId,
		bool isActive)
	{
		if (!isActive)
		{
			return;
		}

		var start = effectiveFrom.Date;
		var end = effectiveTo?.Date;
		var effectiveEnd = end ?? DateTime.MaxValue.Date;
		var query = await repository.GetQueryableAsync();
		var overlap = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.AcademicYearId == academicYearId &&
			x.IsActive &&
			(!excludeId.HasValue || x.Id != excludeId.Value) &&
			x.EffectiveFrom <= effectiveEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo >= start))
			.OrderByDescending(x => x.Priority)
			.ThenBy(x => x.EffectiveFrom));

		if (overlap != null)
		{
			var payroll = await payrollPolicyRepository.FindAsync(overlap.PayrollPolicyId);
			var attendance = await attendancePolicyRepository.FindAsync(overlap.AttendancePolicyId);
			var conflict = new HrPolicyAssignmentConflictDto
			{
				AssignmentId = overlap.Id,
				TargetType = HrPolicyMapping.MapAssignmentTargetType(overlap.TargetType),
				TargetKey = overlap.TargetKey,
				TargetDisplayName = overlap.TargetDisplayName,
				PayrollPolicyId = overlap.PayrollPolicyId,
				PayrollPolicyName = payroll?.Name ?? "Unknown payroll policy",
				AttendancePolicyId = overlap.AttendancePolicyId,
				AttendancePolicyName = attendance?.Name ?? "Unknown attendance policy",
				EffectiveFrom = overlap.EffectiveFrom,
				EffectiveTo = overlap.EffectiveTo,
				Priority = overlap.Priority,
				SuggestedFixes =
				[
					"Adjust the effective dates so the ranges do not overlap.",
					"Deactivate the existing assignment before creating this one.",
					"Edit the existing assignment instead of creating a duplicate range."
				]
			};
			throw new BusinessException("ShikshaMitra:HrPolicyAssignmentOverlap")
				.WithData("conflict", JsonSerializer.Serialize(conflict))
				.WithData("conflictingAssignmentId", overlap.Id)
				.WithData("targetType", conflict.TargetType)
				.WithData("targetKey", conflict.TargetKey);
		}
	}

	private async Task<System.Collections.Generic.List<HrPolicyAffectedEmployeeDto>> BuildAffectedEmployeesAsync(
		IQueryable<Employee> employeeQuery,
		HrPolicyAssignmentTargetType targetType,
		string targetKey)
	{
		if (targetType == HrPolicyAssignmentTargetType.Group)
		{
			if (!Guid.TryParse(targetKey, out var groupId))
			{
				return [];
			}

			var group = await groupRepository.GetWithDetailsAsync(groupId);
			if (group == null)
			{
				return [];
			}

			var employeesForGroup = await AsyncExecuter.ToListAsync(employeeQuery);
			var employeeIds = HrPolicyGroupMembership.ResolveEmployeeIds(group, employeesForGroup);
			var affected = employeesForGroup
				.Where(x => employeeIds.Contains(x.Id))
				.Take(25)
				.ToList();

			return affected.Select(MapAffectedEmployee).ToList();
		}

		var query = targetType switch
		{
			HrPolicyAssignmentTargetType.Employee when Guid.TryParse(targetKey, out var employeeId) => employeeQuery.Where(x => x.Id == employeeId),
			_ => employeeQuery.Where(_ => false)
		};

		var employees = await AsyncExecuter.ToListAsync(query.Take(25));
		return employees.Select(MapAffectedEmployee).ToList();
	}

	private async Task<List<HrPolicyEffectiveAssignmentConflictDto>> BuildEffectiveAssignmentConflictsAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAffectedEmployeeDto> affectedEmployees)
	{
		if (targetType != HrPolicyAssignmentTargetType.Group || affectedEmployees.Count == 0 || !Guid.TryParse(targetKey, out var proposedGroupId))
		{
			return [];
		}

		var date = proposed.EffectiveFrom.Date;
		var groups = await groupRepository.GetListWithDetailsAsync(proposed.AcademicYearId);
		var proposedGroup = groups.FirstOrDefault(x => x.Id == proposedGroupId);
		if (proposedGroup == null)
		{
			return [];
		}

		var affectedEmployeeIds = affectedEmployees.Select(e => e.EmployeeId).ToList();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => affectedEmployeeIds.Contains(x.Id)));
		var assignmentQuery = await repository.GetQueryableAsync();
		var activeGroupAssignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(x =>
			x.AcademicYearId == proposed.AcademicYearId &&
			x.TargetType == HrPolicyAssignmentTargetType.Group &&
			x.IsActive &&
			x.EffectiveFrom <= date &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date)));

		var payrollIds = activeGroupAssignments.Select(x => x.PayrollPolicyId).Append(proposed.PayrollPolicyId).Distinct().ToList();
		var attendanceIds = activeGroupAssignments.Select(x => x.AttendancePolicyId).Append(proposed.AttendancePolicyId).Distinct().ToList();
		var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
		var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
		var payrollNames = (await AsyncExecuter.ToListAsync(payrollQuery.Where(x => payrollIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
		var attendanceNames = (await AsyncExecuter.ToListAsync(attendanceQuery.Where(x => attendanceIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);

		var groupMap = groups.ToDictionary(x => x.Id);
		var conflicts = new List<HrPolicyEffectiveAssignmentConflictDto>();
		foreach (var employee in employees)
		{
			var candidates = new List<HrPolicyEffectiveAssignmentCandidateDto>();
			if (HrPolicyGroupMembership.MatchesEmployee(proposedGroup, employee))
			{
				candidates.Add(MapCandidate(
					null,
					targetKey,
					proposed.TargetDisplayName,
					proposed.PayrollPolicyId,
					proposed.AttendancePolicyId,
					proposed.EffectiveFrom,
					proposed.EffectiveTo,
					proposed.Priority,
					payrollNames,
					attendanceNames));
			}

			foreach (var assignment in activeGroupAssignments)
			{
				if (!Guid.TryParse(assignment.TargetKey, out var groupId) ||
					!groupMap.TryGetValue(groupId, out var group) ||
					!HrPolicyGroupMembership.MatchesEmployee(group, employee))
				{
					continue;
				}

				candidates.Add(MapCandidate(
					assignment.Id,
					assignment.TargetKey,
					assignment.TargetDisplayName,
					assignment.PayrollPolicyId,
					assignment.AttendancePolicyId,
					assignment.EffectiveFrom,
					assignment.EffectiveTo,
					assignment.Priority,
					payrollNames,
					attendanceNames));
			}

			if (candidates.Count <= 1)
			{
				continue;
			}

			var winner = candidates
				.OrderByDescending(x => x.Priority)
				.ThenByDescending(x => x.EffectiveFrom)
				.First();
			foreach (var candidate in candidates)
			{
				candidate.Status = ReferenceEquals(candidate, winner) ? "matched" : "skipped";
				candidate.SkipReason = ReferenceEquals(candidate, winner) ? "winner" : "lower_priority";
			}

			conflicts.Add(new HrPolicyEffectiveAssignmentConflictDto
			{
				EmployeeId = employee.Id,
				EmployeeCode = employee.EmployeeCode,
				EmployeeName = employee.EmployeeName,
				Date = date,
				WinningAssignment = winner,
				CandidateAssignments = candidates
			});
		}

		return conflicts;
	}

	private static HrPolicyEffectiveAssignmentCandidateDto MapCandidate(
		Guid? assignmentId,
		string targetKey,
		string? targetDisplayName,
		Guid payrollPolicyId,
		Guid attendancePolicyId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		int priority,
		IReadOnlyDictionary<Guid, string> payrollNames,
		IReadOnlyDictionary<Guid, string> attendanceNames) => new()
	{
		AssignmentId = assignmentId,
		TargetType = "group",
		TargetKey = targetKey,
		TargetDisplayName = targetDisplayName,
		PayrollPolicyId = payrollPolicyId,
		PayrollPolicyName = payrollNames.GetValueOrDefault(payrollPolicyId) ?? "Unknown payroll policy",
		AttendancePolicyId = attendancePolicyId,
		AttendancePolicyName = attendanceNames.GetValueOrDefault(attendancePolicyId) ?? "Unknown attendance policy",
		EffectiveFrom = effectiveFrom.Date,
		EffectiveTo = effectiveTo?.Date,
		Priority = priority
	};

	private static HrPolicyAffectedEmployeeDto MapAffectedEmployee(Employee x) => new()
		{
			EmployeeId = x.Id,
			EmployeeCode = x.EmployeeCode,
			EmployeeName = x.EmployeeName,
			Department = x.Department,
			RoleId = x.RoleId,
			ShiftId = x.ShiftId,
			EmployeeType = x.EmployeeGroup?.ToString()
		};

	private static EffectiveEmployeePoliciesDto ToEffectivePoliciesDto(HrResolvedPolicies resolved)
	{
		return new EffectiveEmployeePoliciesDto
		{
			EmployeeId = resolved.EmployeeId,
			Date = resolved.Date,
			Assignment = resolved.Assignment == null ? null : HrPolicyMapping.ToDto(resolved.Assignment),
			PolicySource = resolved.Assignment == null ? "default" : HrPolicyMapping.MapAssignmentTargetType(resolved.Assignment.TargetType),
			MatchedAssignmentId = resolved.Assignment?.Id,
			MatchedTargetType = resolved.Assignment == null ? null : HrPolicyMapping.MapAssignmentTargetType(resolved.Assignment.TargetType),
			MatchedTargetKey = resolved.Assignment?.TargetKey,
			MatchedTargetDisplayName = resolved.Assignment?.TargetDisplayName,
			MatchedPriority = resolved.Assignment?.Priority,
			AttendancePolicy = HrPolicyMapping.ToDto(resolved.AttendancePolicy),
			PayrollPolicy = HrPolicyMapping.ToDto(resolved.PayrollPolicy),
			AttendanceWorkCalendarPolicy = HrPolicyMapping.ToDto(resolved.AttendanceWorkCalendarPolicy),
			PayrollWorkCalendarPolicy = HrPolicyMapping.ToDto(resolved.PayrollWorkCalendarPolicy),
			ResolutionSteps = resolved.ResolutionSteps.Select(x => new EffectivePolicyResolutionStepDto
			{
				Source = x.Source,
				TargetType = x.TargetType.HasValue ? HrPolicyMapping.MapAssignmentTargetType(x.TargetType.Value) : "default",
				TargetKey = x.TargetKey,
				TargetDisplayName = x.TargetDisplayName,
				AssignmentId = x.AssignmentId,
				Priority = x.Priority,
				Status = x.Status,
				SkipReason = x.SkipReason,
				PayrollPolicyId = x.PayrollPolicyId,
				AttendancePolicyId = x.AttendancePolicyId
			}).ToList()
		};
	}

	private static string BuildAssignmentAuditName(HrPolicyAssignment assignment) =>
		$"{HrPolicyMapping.MapAssignmentTargetType(assignment.TargetType)}: {assignment.TargetDisplayName ?? assignment.TargetKey}";

	private static object BuildAssignmentAuditContext(HrPolicyAssignment assignment) => new
	{
		targetType = HrPolicyMapping.MapAssignmentTargetType(assignment.TargetType),
		assignment.TargetKey,
		assignment.TargetDisplayName,
		assignment.PayrollPolicyId,
		assignment.AttendancePolicyId,
		assignment.EffectiveFrom,
		assignment.EffectiveTo,
		assignment.Priority,
		assignment.IsActive
	};

	private async Task EnrichTargetOrganizationsAsync(IReadOnlyCollection<HrPolicyAssignmentDto> assignments)
	{
		var employeeIds = assignments
			.Where(x => string.Equals(x.TargetType, "employee", StringComparison.OrdinalIgnoreCase))
			.Select(x => Guid.TryParse(x.TargetKey, out var employeeId) ? employeeId : (Guid?) null)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		if (employeeIds.Count == 0)
		{
			return;
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		var organizationsByEmployeeId = employees.ToDictionary(
			x => x.Id,
			x => x.OrganizationAssignments
				.Where(assignment => assignment.EducationMediumStage != null)
				.Select(assignment => assignment.EducationMediumStage!.Name)
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct()
				.OrderBy(name => name)
				.ToList());

		foreach (var assignment in assignments)
		{
			if (!string.Equals(assignment.TargetType, "employee", StringComparison.OrdinalIgnoreCase) ||
			    !Guid.TryParse(assignment.TargetKey, out var employeeId) ||
			    !organizationsByEmployeeId.TryGetValue(employeeId, out var organizationNames))
			{
				continue;
			}

			assignment.TargetOrganizationNames = organizationNames;
		}
	}
}

[RemoteService(false)]
[Authorize]
public class PolicyHolidayImpactAppService(
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	IRepository<Holiday, Guid> centralHolidayRepository)
	: ApplicationService, IPolicyHolidayImpactAppService
{
	public async Task<PolicyHolidayImpactPreviewDto> GetPreviewAsync(
		Guid academicYearId,
		Guid? workCalendarPolicyId,
		Guid? payrollPolicyId,
		Guid? attendancePolicyId,
		DateTime? fromDate,
		DateTime? toDate,
		string? month)
	{
		var attendancePolicy = attendancePolicyId.HasValue
			? await attendancePolicyRepository.GetAsync(attendancePolicyId.Value)
			: await GetDefaultAttendancePolicyAsync(academicYearId);
		var payrollPolicy = payrollPolicyId.HasValue
			? await payrollPolicyRepository.GetAsync(payrollPolicyId.Value)
			: await GetDefaultPayrollPolicyAsync(academicYearId);
		var workCalendarPolicy = workCalendarPolicyId.HasValue
			? await workCalendarPolicyRepository.GetAsync(workCalendarPolicyId.Value)
			: await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId);
		var (rangeStart, rangeEnd) = ResolveHolidayRange(fromDate, toDate, month);
		var workCalendarHolidayQuery = await workCalendarHolidayRepository.GetQueryableAsync();
		var finalizedHolidays = await AsyncExecuter.ToListAsync(workCalendarHolidayQuery
			.Where(x =>
				x.AcademicYearId == academicYearId &&
				x.WorkCalendarPolicyId == workCalendarPolicy.Id));
		if (workCalendarPolicy.HolidaySyncMode == WorkCalendarHolidaySyncMode.LinkedWithOverrides)
		{
			var existingCalendarHolidayIds = finalizedHolidays
				.Where(x => x.CalendarHolidayId.HasValue)
				.Select(x => x.CalendarHolidayId!.Value)
				.ToHashSet();
			var centralHolidayQuery = await centralHolidayRepository.GetQueryableAsync();
			var centralHolidays = await AsyncExecuter.ToListAsync(centralHolidayQuery
				.Where(x => x.AcademicYearId == academicYearId && !existingCalendarHolidayIds.Contains(x.Id)));
			finalizedHolidays.AddRange(centralHolidays.Select(x => new WorkCalendarHoliday(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				workCalendarPolicy.Id,
				x.StartDate.Date,
				x.EndDate.Date,
				x.HolidayName,
				WorkCalendarHolidaySource.CentralCalendar,
				x.Id,
				isIncluded: true,
				x.Description)));
		}
		var holidays = finalizedHolidays
			.Where(x => x.IsIncluded)
			.Where(x =>
				(!rangeStart.HasValue || x.ToDate.Date >= rangeStart.Value) &&
				(!rangeEnd.HasValue || x.FromDate.Date <= rangeEnd.Value))
			.OrderBy(x => x.FromDate)
			.ToList();

		var payrollOverrides = payrollPolicy.GetHolidayOverrides()
			.GroupBy(x => (x.Date.Date, x.CalendarHolidayId))
			.ToDictionary(x => x.Key, x => x.First());

		var attendanceImpact = MapAttendanceImpact(attendancePolicy.HolidayWorkBehavior);
		var holidayWorkBehavior = MapHolidayWorkBehavior(attendancePolicy.HolidayWorkBehavior);
		var isPunchRequired = attendancePolicy.HolidayWorkBehavior == AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout;
		var isAttendanceWorkingDay = isPunchRequired;
		var items = holidays.Select(holiday =>
		{
				var date = holiday.FromDate.Date;
				payrollOverrides.TryGetValue((date, holiday.CalendarHolidayId), out var payrollOverride);
				var isPaid = payrollOverride?.IncludedInPayroll ?? true;
				var payrollImpact = isPaid ? "paid" : "unpaid";
				var payrollOverrideSource = payrollOverride is null ? "policy_default" : "policy_override";
				return new PolicyHolidayImpactItemDto
				{
					Date = date,
					FromDate = holiday.FromDate,
					ToDate = holiday.ToDate,
					DayCount = (holiday.ToDate.Date - holiday.FromDate.Date).Days + 1,
					CalendarHolidayId = holiday.CalendarHolidayId ?? holiday.Id,
					HolidayName = holiday.HolidayName,
					WorkCalendarPolicyId = workCalendarPolicy.Id,
					WorkCalendarPolicyName = workCalendarPolicy.Name,
					CalendarSource = HrPolicyMapping.MapWorkCalendarHolidaySource(holiday.Source),
					AttendanceImpact = attendanceImpact,
					IsAttendanceWorkingDay = isAttendanceWorkingDay,
					IsPunchRequired = isPunchRequired,
					HolidayWorkBehavior = holidayWorkBehavior,
					PayrollImpact = payrollImpact,
					IsPaidInPayroll = isPaid,
					PayableUnit = isPaid ? 1m : 0m,
					PayrollBasisDayDelta = isPaid ? 0m : -1m,
					PayrollOverrideSource = payrollOverrideSource,
					AttendanceBehaviorText = BuildAttendanceBehaviorText(attendanceImpact, isPunchRequired, holidayWorkBehavior),
					PayrollBasisImpactText = isPaid ? "Included as one payable payroll unit." : "Excluded from payroll basis for this policy.",
					HasDifference = isAttendanceWorkingDay || attendanceImpact != payrollImpact
				};
		}).ToList();

		return new PolicyHolidayImpactPreviewDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = workCalendarPolicy.Id,
			PayrollPolicyId = payrollPolicy.Id,
			AttendancePolicyId = attendancePolicy.Id,
			Items = items
		};
	}

	private static (DateTime? Start, DateTime? End) ResolveHolidayRange(DateTime? fromDate, DateTime? toDate, string? month)
	{
		if (!string.IsNullOrWhiteSpace(month) &&
			DateTime.TryParse($"{month.Trim()}-01", out var monthDate))
		{
			var start = new DateTime(monthDate.Year, monthDate.Month, 1);
			return (start, start.AddMonths(1).AddDays(-1));
		}

		return (fromDate?.Date, toDate?.Date);
	}

	private static string MapAttendanceImpact(AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.Paid => "non_working_paid",
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout => "working_required_punch",
		_ => "non_working_comp_off"
	};

	private static string MapHolidayWorkBehavior(AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout => "overtime_payout",
		_ => "comp_off"
	};

	private static string BuildAttendanceBehaviorText(string attendanceImpact, bool isPunchRequired, string holidayWorkBehavior) =>
		$"{attendanceImpact}; punch {(isPunchRequired ? "required" : "not required")}; behavior {holidayWorkBehavior}";

	private async Task<AttendancePolicy> GetDefaultAttendancePolicyAsync(Guid academicYearId)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
			?? throw new BusinessException("ShikshaMitra:DefaultAttendancePolicyRequired");
	}

	private async Task<PayrollPolicy> GetDefaultPayrollPolicyAsync(Guid academicYearId)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
			?? throw new BusinessException("ShikshaMitra:DefaultPayrollPolicyRequired");
	}
}
