using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

[RemoteService(IsEnabled = false)]
public class ClassTeacherAssignmentAppService(
	IRepository<ClassTeacher.ClassTeacher, Guid> classTeacherRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Employee, Guid> employeeRepository,
	IIdentityUserRepository userRepository,
	IGuidGenerator guidGenerator,
	ILogger<ClassTeacherAssignmentAppService> logger
) : ApplicationService, IClassTeacherAssignmentAppService
{
	private const int MaximumClassTeacherAssignments = 2;

	public async Task<List<ClassTeacherRowDto>> GetClassTeacherAssignmentsAsync(Guid organizationId)
	{
		logger.LogInformation(
			"Getting class teacher assignments for organization {OrganizationId}",
			organizationId
		);

		var context = await LoadContextAsync(organizationId);
		return BuildRows(context);
	}

	public async Task<ClassTeacherSummaryDto> GetClassTeacherAssignmentSummaryAsync(Guid organizationId)
	{
		var rows = await GetClassTeacherAssignmentsAsync(organizationId);
		return BuildSummary(rows, pendingChanges: 0);
	}

	public async Task<List<ClassTeacherEligibleTeacherDto>> GetEligibleTeachersAsync(
		Guid organizationId,
		Guid? classId = null,
		Guid? divisionId = null,
		string? search = null
	)
	{
		var context = await LoadContextAsync(organizationId);
		var normalizedSearch = search?.Trim().ToLowerInvariant();

		return context.Employees
			.Where(employee =>
				string.IsNullOrWhiteSpace(normalizedSearch) ||
				employee.EmployeeName.ToLowerInvariant().Contains(normalizedSearch) ||
				(employee.EmployeeCode ?? string.Empty).ToLowerInvariant().Contains(normalizedSearch) ||
				(employee.Department ?? string.Empty).ToLowerInvariant().Contains(normalizedSearch)
			)
			.Select(employee => BuildEligibleTeacher(employee, context))
			.OrderByDescending(teacher => teacher.IsEligible)
			.ThenByDescending(teacher => teacher.RecommendationScore)
			.ThenBy(teacher => teacher.TeacherName)
			.ToList();
	}

	public async Task<ClassTeacherValidationResultDto> ValidateClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	)
	{
		var context = await LoadContextAsync(input.OrganizationId);
		var issues = BuildValidationIssues(input, context);
		var incomingRows = BuildRowsFromInput(input, context, issues);

		return new ClassTeacherValidationResultDto
		{
			CanConfirm = !issues.Any(issue => issue.Severity == "Critical"),
			Issues = issues,
			Summary = BuildSummary(incomingRows, pendingChanges: input.Assignments.Count)
		};
	}

	public async Task<List<ClassTeacherAssignmentHistoryDto>> GetClassTeacherAssignmentHistoryAsync(
		Guid organizationId
	)
	{
		var context = await LoadContextAsync(organizationId);
		var standardsById = context.Standards.ToDictionary(standard => standard.Id);
		var divisionsById = context.Divisions.ToDictionary(division => division.Id);
		var teachersById = context.Employees.ToDictionary(employee => employee.Id);
		var changedUserIds = context.ClassTeachers
			.Select(assignment => assignment.LastModifierId ?? assignment.CreatorId)
			.Where(id => id.HasValue)
			.Select(id => id!.Value)
			.Distinct()
			.ToList();
		var users = changedUserIds.Any()
			? await userRepository.GetListByIdsAsync(changedUserIds)
			: new List<IdentityUser>();

		return context.ClassTeachers
			.OrderByDescending(assignment => assignment.LastModificationTime ?? assignment.CreationTime)
			.Select(assignment =>
			{
				standardsById.TryGetValue(assignment.StandardId, out var standard);
				divisionsById.TryGetValue(assignment.DivisionId, out var division);
				teachersById.TryGetValue(assignment.TeacherId, out var teacher);
				var changedById = assignment.LastModifierId ?? assignment.CreatorId;
				var changedBy = changedById.HasValue
					? users.FirstOrDefault(user => user.Id == changedById.Value)
					: null;

				return new ClassTeacherAssignmentHistoryDto
				{
					ClassId = assignment.StandardId,
					ClassName = standard?.Name ?? "Class",
					DivisionId = assignment.DivisionId,
					DivisionName = division?.Name ?? "Division",
					NewTeacherId = assignment.TeacherId,
					NewTeacherName = teacher?.EmployeeName,
					ChangedAt = assignment.LastModificationTime ?? assignment.CreationTime,
					ChangedBy = changedBy == null
						? "System"
						: $"{changedBy.Name} {changedBy.Surname}".Trim(),
					ChangeReason = assignment.LastModificationTime.HasValue
						? "Assignment updated"
						: "Assignment created"
				};
			})
			.ToList();
	}

	public async Task<ApiSuccessResponseDto> SaveClassTeacherAssignmentsAsync(
		SaveClassTeacherAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Saving class teacher assignments for organization {OrganizationId}",
			input.OrganizationId
		);

		var validation = await ValidateClassTeacherAssignmentsAsync(input);
		if (!validation.CanConfirm)
		{
			var message = string.Join(
				" ",
				validation.Issues
					.Where(issue => issue.Severity == "Critical")
					.Take(3)
					.Select(issue => issue.Message)
			);

			throw new BusinessException("ClassManagement:ClassTeacherAssignmentValidationFailed")
				.WithData("Message", message);
		}

		try
		{
			var orgStandards = await standardRepository.GetListAsync(
				standard => standard.EducationMediumStageId == input.OrganizationId
			);
			var orgStandardIds = orgStandards.Select(standard => standard.Id).ToList();
			var classIds = input.Assignments.Select(assignment => assignment.ClassId).Distinct().ToList();
			var standards = await standardRepository.GetListAsync(standard => classIds.Contains(standard.Id));
			var educationMediumStageIds = standards
				.Select(standard => standard.EducationMediumStageId)
				.Distinct()
				.ToList();
			var educationMediumStages = await educationMediumStageRepository.GetListAsync(
				stage => educationMediumStageIds.Contains(stage.Id)
			);
			var stageById = educationMediumStages.ToDictionary(stage => stage.Id);
			var standardById = standards.ToDictionary(standard => standard.Id);

			var existingAssignments = await classTeacherRepository.GetListAsync(
				assignment => orgStandardIds.Contains(assignment.StandardId)
			);
			if (existingAssignments.Any())
			{
				await classTeacherRepository.DeleteManyAsync(existingAssignments, autoSave: true);
			}

			foreach (var assignment in input.Assignments)
			{
				var standard = standardById[assignment.ClassId];
				var stage = stageById[standard.EducationMediumStageId];

				var newAssignment = new ClassTeacher.ClassTeacher(
					guidGenerator.Create(),
					CurrentTenant.Id,
					assignment.ClassId,
					assignment.DivisionId,
					assignment.TeacherId,
					stage.EducationMediumId,
					educationMediumStageId: stage.Id
				);

				await classTeacherRepository.InsertAsync(newAssignment);
			}

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = input.SaveAsDraft
					? "Class teacher assignment draft saved successfully."
					: "Class teacher assignments confirmed successfully."
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to save class teacher assignments");
			throw new BusinessException("ClassManagement:ClassTeacherAssignmentSaveFailed");
		}
	}

	private async Task<ClassTeacherAssignmentContext> LoadContextAsync(Guid organizationId)
	{
		var standards = await standardRepository.GetListAsync(
			standard => standard.EducationMediumStageId == organizationId
		);
		var standardIds = standards.Select(standard => standard.Id).ToList();
		var divisions = standardIds.Any()
			? await divisionRepository.GetListAsync(division => standardIds.Contains(division.StandardId))
			: new List<Division>();
		var classTeachers = standardIds.Any()
			? await classTeacherRepository.GetListAsync(assignment => standardIds.Contains(assignment.StandardId))
			: new List<ClassTeacher.ClassTeacher>();
		var employees = await employeeRepository.GetListAsync();

		return new ClassTeacherAssignmentContext(
			organizationId,
			standards,
			divisions,
			classTeachers,
			employees
		);
	}

	private List<ClassTeacherRowDto> BuildRows(ClassTeacherAssignmentContext context)
	{
		var classTeachersByKey = context.ClassTeachers.ToDictionary(
			assignment => (assignment.StandardId, assignment.DivisionId),
			assignment => assignment
		);
		var employeesById = context.Employees.ToDictionary(employee => employee.Id);

		return context.Standards
			.OrderBy(standard => standard.AgeLowerLimit)
			.ThenBy(standard => standard.Name)
			.SelectMany(standard => context.Divisions
				.Where(division => division.StandardId == standard.Id)
				.OrderBy(division => division.Name)
				.Select(division =>
				{
					classTeachersByKey.TryGetValue((standard.Id, division.Id), out var assignment);
					AssignedTeacherDto? assignedTeacher = null;
					var issues = new List<ClassTeacherValidationIssueDto>();

					if (assignment != null && employeesById.TryGetValue(assignment.TeacherId, out var employee))
					{
						assignedTeacher = BuildAssignedTeacher(employee, context, standard.Id, division.Id);
						issues.AddRange(BuildTeacherIssues(employee, context, standard.Id, division.Id));
					}
					else
					{
						issues.Add(new ClassTeacherValidationIssueDto
						{
							Severity = "Warning",
							Code = "UNASSIGNED",
							ClassId = standard.Id,
							DivisionId = division.Id,
							Message = $"{standard.Name} {division.Name} does not have a class teacher.",
							SuggestedFix = "Assign an active teacher before final confirmation."
						});
					}

					return new ClassTeacherRowDto
					{
						ClassId = standard.Id,
						ClassName = standard.Name,
						DivisionId = division.Id,
						DivisionName = division.Name,
						StudentCount = division.Strength,
						AssignmentStatus = assignedTeacher == null
							? "Unassigned"
							: issues.Any(issue => issue.Severity == "Critical")
								? "Conflict"
								: "Active",
						ApprovalStatus = assignedTeacher == null ? "Not Submitted" : "Approved",
						AcknowledgmentStatus = assignedTeacher == null ? "Not Sent" : "Pending",
						HandoverStatus = assignedTeacher == null ? "Not Required" : "Pending",
						AssignmentType = "Permanent",
						LastUpdatedAt = assignment?.LastModificationTime ?? assignment?.CreationTime,
						ConflictCount = issues.Count(issue => issue.Severity != "Info"),
						Issues = issues,
						AssignedTeacher = assignedTeacher
					};
				}))
			.ToList();
	}

	private List<ClassTeacherRowDto> BuildRowsFromInput(
		SaveClassTeacherAssignmentsDto input,
		ClassTeacherAssignmentContext context,
		List<ClassTeacherValidationIssueDto> issues
	)
	{
		var employeesById = context.Employees.ToDictionary(employee => employee.Id);
		var assignmentsByKey = input.Assignments
			.GroupBy(assignment => (assignment.ClassId, assignment.DivisionId))
			.ToDictionary(group => group.Key, group => group.Last());

		return context.Standards
			.OrderBy(standard => standard.AgeLowerLimit)
			.ThenBy(standard => standard.Name)
			.SelectMany(standard => context.Divisions
				.Where(division => division.StandardId == standard.Id)
				.OrderBy(division => division.Name)
				.Select(division =>
				{
					assignmentsByKey.TryGetValue((standard.Id, division.Id), out var assignment);
					var rowIssues = issues
						.Where(issue => issue.ClassId == standard.Id && issue.DivisionId == division.Id)
						.ToList();

					return new ClassTeacherRowDto
					{
						ClassId = standard.Id,
						ClassName = standard.Name,
						DivisionId = division.Id,
						DivisionName = division.Name,
						StudentCount = division.Strength,
						AssignmentStatus = assignment == null
							? "Unassigned"
							: rowIssues.Any(issue => issue.Severity == "Critical")
								? "Conflict"
								: "Changed",
						ConflictCount = rowIssues.Count(issue => issue.Severity != "Info"),
						Issues = rowIssues,
						AssignedTeacher = assignment != null &&
							employeesById.TryGetValue(assignment.TeacherId, out var employee)
								? BuildAssignedTeacher(employee, context)
								: null
					};
				}))
			.ToList();
	}

	private ClassTeacherSummaryDto BuildSummary(List<ClassTeacherRowDto> rows, int pendingChanges)
	{
		var summary = new ClassTeacherSummaryDto
		{
			TotalClasses = rows.Count,
			Assigned = rows.Count(row => row.AssignedTeacher != null),
			Unassigned = rows.Count(row => row.AssignedTeacher == null),
			Conflicts = rows.Count(row => row.ConflictCount > 0),
			PendingChanges = pendingChanges,
			PendingApproval = rows.Count(row => row.ApprovalStatus == "Pending"),
			TemporaryAssignments = rows.Count(row => row.AssignmentType == "Temporary"),
			AcknowledgmentPending = rows.Count(row => row.AcknowledgmentStatus == "Pending")
		};

		summary.Status = summary.Conflicts > 0
			? "Attention Needed"
			: summary.Unassigned > 0
				? "Partially Assigned"
				: "Ready";

		if (summary.Unassigned > 0)
		{
			summary.Reasons.Add($"{summary.Unassigned} class divisions are unassigned.");
		}

		if (summary.Conflicts > 0)
		{
			summary.Reasons.Add($"{summary.Conflicts} assignments need review.");
		}

		if (summary.PendingChanges > 0)
		{
			summary.Reasons.Add($"{summary.PendingChanges} draft changes are not confirmed.");
		}

		return summary;
	}

	private AssignedTeacherDto BuildAssignedTeacher(
		Employee employee,
		ClassTeacherAssignmentContext context,
		Guid? excludeClassId = null,
		Guid? excludeDivisionId = null
	)
	{
		var eligibleTeacher = BuildEligibleTeacher(employee, context, excludeClassId, excludeDivisionId);

		return new AssignedTeacherDto
		{
			TeacherId = employee.Id,
			TeacherName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			Department = employee.Department,
			IsActive = eligibleTeacher.IsActive,
			CurrentClassTeacherCount = eligibleTeacher.CurrentClassTeacherCount,
			MaximumClassTeacherCount = eligibleTeacher.MaximumClassTeacherCount,
			AvailabilityStatus = eligibleTeacher.AvailabilityStatus,
			WorkloadStatus = eligibleTeacher.WorkloadStatus,
			ConflictStatus = eligibleTeacher.ConflictStatus,
			RecommendationScore = eligibleTeacher.RecommendationScore
		};
	}

	private ClassTeacherEligibleTeacherDto BuildEligibleTeacher(
		Employee employee,
		ClassTeacherAssignmentContext context,
		Guid? excludeClassId = null,
		Guid? excludeDivisionId = null
	)
	{
		var classTeacherCount = context.ClassTeachers.Count(assignment =>
			assignment.TeacherId == employee.Id &&
			!(excludeClassId.HasValue && excludeDivisionId.HasValue &&
			  assignment.StandardId == excludeClassId.Value &&
			  assignment.DivisionId == excludeDivisionId.Value)
		);
		var isActive = (employee.EmployeeStatusType ?? EmployeeStatusType.Active) == EmployeeStatusType.Active;
		var isOverLimit = classTeacherCount >= MaximumClassTeacherAssignments;
		var reasons = new List<string>();

		if (!isActive)
		{
			reasons.Add("Teacher is not active.");
		}

		if (isOverLimit)
		{
			reasons.Add($"Teacher already has {classTeacherCount} class teacher responsibilities.");
		}

		var score = 100 - Math.Min(classTeacherCount * 25, 75);
		if (!isActive)
		{
			score = 0;
		}

		return new ClassTeacherEligibleTeacherDto
		{
			TeacherId = employee.Id,
			TeacherName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			Department = employee.Department,
			CurrentWorkload = classTeacherCount,
			MaximumWorkload = 30,
			CurrentClassTeacherCount = classTeacherCount,
			MaximumClassTeacherCount = MaximumClassTeacherAssignments,
			IsActive = isActive,
			IsEligible = isActive && !isOverLimit,
			HasTimetableConflict = false,
			IsOnLeave = false,
			AvailabilityStatus = isActive ? "Available" : "Inactive",
			WorkloadStatus = isOverLimit
				? "Exceeded"
				: classTeacherCount == MaximumClassTeacherAssignments - 1
					? "Near Capacity"
					: "Normal",
			ConflictStatus = reasons.Any() ? "Needs Review" : "None",
			RecommendationScore = score,
			IneligibilityReasons = reasons
		};
	}

	private List<ClassTeacherValidationIssueDto> BuildValidationIssues(
		SaveClassTeacherAssignmentsDto input,
		ClassTeacherAssignmentContext context
	)
	{
		var issues = new List<ClassTeacherValidationIssueDto>();
		var standardIds = context.Standards.Select(standard => standard.Id).ToHashSet();
		var divisionById = context.Divisions.ToDictionary(division => division.Id);
		var employeesById = context.Employees.ToDictionary(employee => employee.Id);
		var seenClassDivisions = new HashSet<(Guid ClassId, Guid DivisionId)>();

		foreach (var assignment in input.Assignments)
		{
			if (!seenClassDivisions.Add((assignment.ClassId, assignment.DivisionId)))
			{
				issues.Add(new ClassTeacherValidationIssueDto
				{
					Severity = "Critical",
					Code = "DUPLICATE_CLASS_DIVISION",
					ClassId = assignment.ClassId,
					DivisionId = assignment.DivisionId,
					Message = "The same class and division has more than one primary class teacher in this request.",
					SuggestedFix = "Keep only one primary teacher for each class division."
				});
			}

			if (!standardIds.Contains(assignment.ClassId))
			{
				issues.Add(new ClassTeacherValidationIssueDto
				{
					Severity = "Critical",
					Code = "INVALID_CLASS",
					ClassId = assignment.ClassId,
					DivisionId = assignment.DivisionId,
					Message = "One selected class does not belong to the selected organization.",
					SuggestedFix = "Refresh the page and select a class from the current organization."
				});
			}

			if (!divisionById.TryGetValue(assignment.DivisionId, out var division) ||
				division.StandardId != assignment.ClassId)
			{
				issues.Add(new ClassTeacherValidationIssueDto
				{
					Severity = "Critical",
					Code = "INVALID_DIVISION",
					ClassId = assignment.ClassId,
					DivisionId = assignment.DivisionId,
					Message = "One selected division does not belong to the selected class.",
					SuggestedFix = "Refresh the assignment list before saving."
				});
			}

			if (!employeesById.TryGetValue(assignment.TeacherId, out var employee))
			{
				issues.Add(new ClassTeacherValidationIssueDto
				{
					Severity = "Critical",
					Code = "TEACHER_NOT_FOUND",
					ClassId = assignment.ClassId,
					DivisionId = assignment.DivisionId,
					TeacherId = assignment.TeacherId,
					Message = "One selected teacher could not be found.",
					SuggestedFix = "Choose the teacher again from the searchable list."
				});
				continue;
			}

			issues.AddRange(BuildTeacherIssues(employee, context, assignment.ClassId, assignment.DivisionId));

			if (assignment.EffectiveFrom.HasValue &&
				assignment.EffectiveTo.HasValue &&
				assignment.EffectiveTo.Value.Date < assignment.EffectiveFrom.Value.Date)
			{
				issues.Add(new ClassTeacherValidationIssueDto
				{
					Severity = "Critical",
					Code = "INVALID_EFFECTIVE_DATES",
					ClassId = assignment.ClassId,
					DivisionId = assignment.DivisionId,
					TeacherId = assignment.TeacherId,
					Message = "Effective To date cannot be before Effective From date.",
					SuggestedFix = "Correct the assignment date range."
				});
			}
		}

		if (!input.SaveAsDraft)
		{
			var assignedKeys = input.Assignments
				.Select(assignment => (assignment.ClassId, assignment.DivisionId))
				.ToHashSet();
			foreach (var division in context.Divisions)
			{
				if (!assignedKeys.Contains((division.StandardId, division.Id)))
				{
					var standard = context.Standards.FirstOrDefault(item => item.Id == division.StandardId);
					issues.Add(new ClassTeacherValidationIssueDto
					{
						Severity = "Warning",
						Code = "UNASSIGNED",
						ClassId = division.StandardId,
						DivisionId = division.Id,
						Message = $"{standard?.Name ?? "Class"} {division.Name} does not have a class teacher.",
						SuggestedFix = "Assign a teacher or save as draft until allocation is complete."
					});
				}
			}
		}

		return issues;
	}

	private List<ClassTeacherValidationIssueDto> BuildTeacherIssues(
		Employee employee,
		ClassTeacherAssignmentContext context,
		Guid classId,
		Guid divisionId
	)
	{
		var issues = new List<ClassTeacherValidationIssueDto>();
		var isActive = (employee.EmployeeStatusType ?? EmployeeStatusType.Active) == EmployeeStatusType.Active;
		// Exclude the current assignment so a teacher is not flagged for having the
		// assignment that already belongs to this class-division.
		var classTeacherCount = context.ClassTeachers.Count(assignment =>
			assignment.TeacherId == employee.Id &&
			!(assignment.StandardId == classId && assignment.DivisionId == divisionId)
		);

		if (!isActive)
		{
			issues.Add(new ClassTeacherValidationIssueDto
			{
				Severity = "Critical",
				Code = "INACTIVE_TEACHER",
				ClassId = classId,
				DivisionId = divisionId,
				TeacherId = employee.Id,
				Message = $"{employee.EmployeeName} is not active and cannot be assigned as class teacher.",
				SuggestedFix = "Select an active teacher or update the employee status first."
			});
		}

		if (classTeacherCount >= MaximumClassTeacherAssignments)
		{
			issues.Add(new ClassTeacherValidationIssueDto
			{
				Severity = "Critical",
				Code = "WORKLOAD_EXCEEDED",
				ClassId = classId,
				DivisionId = divisionId,
				TeacherId = employee.Id,
				Message = $"{employee.EmployeeName} already has {classTeacherCount} class teacher responsibilities.",
				SuggestedFix = $"Choose a teacher below the {MaximumClassTeacherAssignments} class-teacher limit."
			});
		}
		else if (classTeacherCount == MaximumClassTeacherAssignments - 1)
		{
			issues.Add(new ClassTeacherValidationIssueDto
			{
				Severity = "Warning",
				Code = "NEAR_CAPACITY",
				ClassId = classId,
				DivisionId = divisionId,
				TeacherId = employee.Id,
				Message = $"{employee.EmployeeName} is near the class-teacher workload limit.",
				SuggestedFix = "Review workload before confirming."
			});
		}

		return issues;
	}

	private sealed class ClassTeacherAssignmentContext(
		Guid organizationId,
		List<Standard> standards,
		List<Division> divisions,
		List<ClassTeacher.ClassTeacher> classTeachers,
		List<Employee> employees
	)
	{
		public Guid OrganizationId { get; } = organizationId;
		public List<Standard> Standards { get; } = standards;
		public List<Division> Divisions { get; } = divisions;
		public List<ClassTeacher.ClassTeacher> ClassTeachers { get; } = classTeachers;
		public List<Employee> Employees { get; } = employees;
	}
}
