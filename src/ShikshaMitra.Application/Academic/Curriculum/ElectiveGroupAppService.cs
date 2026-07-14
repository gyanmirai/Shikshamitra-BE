using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Academic.Examination.Exams;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service for managing elective groups
/// Implements CRUD operations with academic medium scoping and security validation
/// </summary>
public class ElectiveGroupAppService(
	IRepository<ElectiveGroup, Guid> ElectiveGroupRepository,
	ElectiveGroupManager ElectiveGroupManager,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	ILogger<ElectiveGroupAppService> logger,
	IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository,
	IRepository<WeeklyLectureSlotSubject, Guid> weeklyLectureSlotSubjectRepository,
	IRepository<TimetableSlot, Guid> timetableSlotRepository,
	IRepository<ExamSubject, Guid> examSubjectRepository
) : ApplicationService, IElectiveGroupAppService
{
	/// <summary>
	/// Get all elective groups for a specific education medium with optional search
	/// </summary>
	public async Task<List<ElectiveGroupDto>> GetListAsync(
		Guid academicMediumId,
		string? searchTerm = null
	)
	{
		try
		{
			var query = await ElectiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);

			var filtered = query
				.Where(eg => eg.AcademicMediumId == academicMediumId)
				.WhereIf(
					!string.IsNullOrWhiteSpace(searchTerm),
					eg => eg.Name.Contains(searchTerm!)
						|| (eg.GroupType != null && eg.GroupType.Contains(searchTerm!))
						|| eg.Subjects.Any(s => s.Subject.Name.Contains(searchTerm!))
				)
				.OrderBy(eg => eg.SortOrder ?? int.MaxValue)
				.ThenBy(eg => eg.Name)
				.ToList();

			var result = new List<ElectiveGroupDto>();
			foreach (var electiveGroup in filtered)
			{
				result.Add(await MapElectiveGroupAsync(electiveGroup));
			}
			return result;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving elective groups for medium {MediumId}",
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while retrieving elective groups."
			);
		}
	}

	/// <summary>
	/// Get a single elective group by ID with medium verification
	/// </summary>
	public async Task<ElectiveGroupDto> GetAsync(Guid academicMediumId, Guid id)
	{
		try
		{
			var electiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);

			// Verify the group belongs to the specified medium
			if (electiveGroup.AcademicMediumId != academicMediumId)
			{
				throw new EntityNotFoundException(typeof(ElectiveGroup), id);
			}

			return await MapElectiveGroupAsync(electiveGroup);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving elective group {Id} for medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while retrieving the elective group."
			);
		}
	}

	/// <summary>
	/// Create a new elective group
	/// </summary>
	public async Task<ElectiveGroupDto> CreateAsync(
		Guid academicMediumId,
		CreateUpdateElectiveGroupDto input
	)
	{
		try
		{
			var electiveGroup = await ElectiveGroupManager.CreateAsync(
				input.Name,
				input.Color,
				academicMediumId, // From route parameter
				input.SubjectIds,
				input.GroupType,
				input.Description,
				input.Status,
				input.SortOrder
			);

			// Ensure the entity is saved before re-fetching with details
			await ElectiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

			// Re-fetch with details for proper mapping
			return await GetAsync(academicMediumId, electiveGroup.Id);
		}
		catch (BusinessException ex)
		{
			logger.LogError(ex, "Business error creating elective group: {Code}", ex.Code);
			// Rethrow BusinessException - ABP middleware will handle localization
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error creating elective group '{Name}' for medium {MediumId}",
				input.Name,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while creating the elective group."
			);
		}
	}

	/// <summary>
	/// Update an existing elective group
	/// </summary>
	public async Task<ElectiveGroupDto> UpdateAsync(
		Guid academicMediumId,
		Guid id,
		CreateUpdateElectiveGroupDto input
	)
	{
		try
		{
			await EnsureSubjectRemovalIsAllowedAsync(id, input.SubjectIds);

			var electiveGroup = await ElectiveGroupManager.UpdateAsync(
				id,
				input.Name,
				input.Color,
				academicMediumId, // Verify medium
				input.SubjectIds,
				input.GroupType,
				input.Description,
				input.Status,
				input.SortOrder
			);

			await ElectiveGroupRepository.UpdateAsync(electiveGroup, autoSave: true);

			return await GetAsync(academicMediumId, id);
		}
		catch (BusinessException ex)
		{
			logger.LogError(
				ex,
				"Business error updating elective group {Id}: {Code}",
				id,
				ex.Code
			);
			// Rethrow BusinessException - ABP middleware will handle localization
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error updating elective group {Id} for medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while updating the elective group."
			);
		}
	}

	/// <summary>
	/// Delete an elective group. Blocked if the group is still in use.
	/// </summary>
	public async Task DeleteAsync(Guid academicMediumId, Guid id)
	{
		try
		{
			var electiveGroup = await ElectiveGroupRepository.GetAsync(id);

			// Verify the group belongs to the specified medium
			if (electiveGroup.AcademicMediumId != academicMediumId)
			{
				throw new EntityNotFoundException(typeof(ElectiveGroup), id);
			}

			var usage = await GetUsageCountsAsync(id);
			var usageParts = BuildUsageParts(usage);

			if (usageParts.Count > 0)
			{
				throw new BusinessException(CurriculumErrorCodes.ElectiveGroupHasActiveAllocations)
					.WithData("message", $"Cannot delete elective group '{electiveGroup.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
			}

			await ElectiveGroupRepository.DeleteAsync(id);

			logger.LogInformation(
				"Deleted elective group {Id} from medium {MediumId}",
				id,
				academicMediumId
			);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error deleting elective group {Id} from medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while deleting the elective group."
			);
		}
	}

	public async Task<ElectiveGroupDto> ArchiveAsync(
		Guid academicMediumId,
		Guid id,
		ElectiveGroupStatusChangeDto input
	)
	{
		var electiveGroup = await GetGroupForMediumAsync(academicMediumId, id);
		electiveGroup.Archive(input.Reason);
		await ElectiveGroupRepository.UpdateAsync(electiveGroup, autoSave: true);
		return await GetAsync(academicMediumId, id);
	}

	public async Task<ElectiveGroupDto> RestoreAsync(Guid academicMediumId, Guid id)
	{
		var electiveGroup = await GetGroupForMediumAsync(academicMediumId, id);
		electiveGroup.Restore();
		await ElectiveGroupRepository.UpdateAsync(electiveGroup, autoSave: true);
		return await GetAsync(academicMediumId, id);
	}

	public async Task<ElectiveGroupDuplicateCheckResultDto> CheckDuplicateAsync(
		Guid academicMediumId,
		ElectiveGroupDuplicateCheckDto input
	)
	{
		var normalized = NormalizeName(input.Name);
		var incomingSubjects = input.SubjectIds.Distinct().OrderBy(x => x).ToList();
		var query = await ElectiveGroupRepository.WithDetailsAsync(x => x.Subjects);
		var groups = query
			.Where(x => x.AcademicMediumId == academicMediumId && x.Id != input.ExcludeElectiveGroupId)
			.ToList();

		var matches = groups
			.Select(group => new
			{
				Group = group,
				Normalized = NormalizeName(group.Name),
				SubjectIds = group.Subjects.Select(x => x.SubjectId).Distinct().OrderBy(x => x).ToList()
			})
			.Where(x =>
				x.Normalized == normalized
				|| IsSimilarName(normalized, x.Normalized)
				|| (incomingSubjects.Count >= 2 && x.SubjectIds.SequenceEqual(incomingSubjects))
			)
			.Select(x => new ElectiveGroupDuplicateMatchDto
			{
				Id = x.Group.Id,
				Name = x.Group.Name,
				Status = x.Group.Status,
				MatchType = x.SubjectIds.SequenceEqual(incomingSubjects)
					? "Same Subjects"
					: x.Normalized == normalized ? "Exact" : "Similar"
			})
			.ToList();

		return new ElectiveGroupDuplicateCheckResultDto
		{
			HasExactDuplicate = matches.Any(x => x.MatchType == "Exact" && x.Status == SubjectMasterStatus.Active),
			HasSimilarDuplicate = matches.Any(x => x.MatchType == "Similar"),
			HasSameSubjectCombination = matches.Any(x => x.MatchType == "Same Subjects"),
			Matches = matches
		};
	}

	public async Task<ElectiveGroupUsageDetailDto> GetUsageDetailsAsync(
		Guid academicMediumId,
		Guid id
	)
	{
		var electiveGroup = await GetGroupForMediumAsync(academicMediumId, id);
		var usage = await GetUsageCountsAsync(id);
		var selections = await studentElectiveSelectionRepository.GetListAsync(x => x.ElectiveGroupId == id);
		var subjectIds = selections.Select(x => x.SelectedSubjectId).Distinct().ToList();
		var subjects = subjectIds.Count == 0
			? []
			: await subjectRepository.GetListAsync(x => subjectIds.Contains(x.Id));

		return new ElectiveGroupUsageDetailDto
		{
			ElectiveGroupId = id,
			ElectiveGroupName = electiveGroup.Name,
			CanDelete = usage.Total == 0,
			SafeActions = usage.Total == 0
				? ["Edit", "Delete"]
				: ["Edit color", "Edit description", "Archive"],
			Subjects = electiveGroup.Subjects.Select(x => x.Subject.Name).ToList(),
			Usage = new ElectiveGroupUsageSummaryDto
			{
				ClassAllocationsCount = usage.Allocations,
				StudentSelectionsCount = usage.StudentSelections,
				TimetablePeriodsCount = usage.WeeklySlots + usage.TimetableSlots,
				ExamSchedulesCount = usage.ExamSubjects,
				SubjectWiseStudentCount = subjects.Select(subject => new ElectiveGroupSubjectStudentCountDto
				{
					Subject = subject.Name,
					Count = selections.Count(selection => selection.SelectedSubjectId == subject.Id)
				}).ToList()
			}
		};
	}

	public async Task<List<ElectiveGroupAuditLogDto>> GetAuditLogsAsync(
		Guid academicMediumId,
		Guid id
	)
	{
		var electiveGroup = await GetGroupForMediumAsync(academicMediumId, id);
		var logs = new List<ElectiveGroupAuditLogDto>
		{
			new()
			{
				ChangedDate = electiveGroup.CreationTime,
				ChangedBy = electiveGroup.CreatorId?.ToString(),
				Action = "Elective Group Created",
				AfterValue = electiveGroup.Name
			}
		};

		if (electiveGroup.LastModificationTime.HasValue)
		{
			logs.Add(new ElectiveGroupAuditLogDto
			{
				ChangedDate = electiveGroup.LastModificationTime.Value,
				ChangedBy = electiveGroup.LastModifierId?.ToString(),
				Action = electiveGroup.Status == SubjectMasterStatus.Archived
					? "Elective Group Archived"
					: "Elective Group Updated",
				AfterValue = electiveGroup.Name,
				Reason = electiveGroup.ArchiveReason
			});
		}

		return logs.OrderByDescending(x => x.ChangedDate).ToList();
	}

	private async Task<ElectiveGroup> GetGroupForMediumAsync(Guid academicMediumId, Guid id)
	{
		var electiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);
		if (electiveGroup.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(ElectiveGroup), id);
		}
		return electiveGroup;
	}

	private async Task<ElectiveGroupDto> MapElectiveGroupAsync(ElectiveGroup electiveGroup)
	{
		var usage = await GetUsageCountsAsync(electiveGroup.Id);
		return new ElectiveGroupDto
		{
			Id = electiveGroup.Id,
			Name = electiveGroup.Name,
			Color = electiveGroup.Color,
			GroupType = electiveGroup.GroupType,
			Description = electiveGroup.Description,
			Status = electiveGroup.Status,
			SortOrder = electiveGroup.SortOrder,
			ArchiveReason = electiveGroup.ArchiveReason,
			EducationMediumId = electiveGroup.AcademicMediumId,
			Subjects = ObjectMapper.Map<List<Subject>, List<SubjectDto>>(
				electiveGroup.Subjects.Select(x => x.Subject).ToList()
			),
			UsedInClassesCount = usage.Allocations,
			StudentSelectionsCount = usage.StudentSelections,
			TimetablePeriodsCount = usage.WeeklySlots + usage.TimetableSlots,
			ExamSchedulesCount = usage.ExamSubjects,
			IsUsed = usage.Total > 0,
			CanDelete = usage.Total == 0,
			LastUpdatedAt = electiveGroup.LastModificationTime ?? electiveGroup.CreationTime,
			LastUpdatedBy = (electiveGroup.LastModifierId ?? electiveGroup.CreatorId)?.ToString()
		};
	}

	private async Task EnsureSubjectRemovalIsAllowedAsync(Guid groupId, List<Guid> incomingSubjectIds)
	{
		var existingGroup = await ElectiveGroupRepository.GetAsync(groupId, includeDetails: true);
		var removedSubjectIds = existingGroup.Subjects
			.Select(x => x.SubjectId)
			.Except(incomingSubjectIds)
			.ToList();
		if (removedSubjectIds.Count == 0)
		{
			return;
		}

		var selectedRemovedSubjects = await studentElectiveSelectionRepository.CountAsync(x =>
			x.ElectiveGroupId == groupId && removedSubjectIds.Contains(x.SelectedSubjectId)
		);
		if (selectedRemovedSubjects > 0)
		{
			throw new BusinessException(CurriculumErrorCodes.ElectiveGroupHasActiveAllocations)
				.WithData("message", "This group already has student selections. Direct subject removal is blocked to protect academic history. Please create a new group or manage changes through allocation rules.");
		}
	}

	private async Task<ElectiveGroupUsageCounts> GetUsageCountsAsync(Guid electiveGroupId)
	{
		return new ElectiveGroupUsageCounts
		{
			Allocations = await assignedSubjectRepository.CountAsync(a => a.ElectiveGroupId == electiveGroupId),
			StudentSelections = await studentElectiveSelectionRepository.CountAsync(s => s.ElectiveGroupId == electiveGroupId),
			WeeklySlots = await weeklyLectureSlotSubjectRepository.CountAsync(w => w.ElectiveGroupId == electiveGroupId),
			TimetableSlots = await timetableSlotRepository.CountAsync(t => t.ElectiveGroupId == electiveGroupId),
			ExamSubjects = await examSubjectRepository.CountAsync(e => e.SubjectId == electiveGroupId)
		};
	}

	private static List<string> BuildUsageParts(ElectiveGroupUsageCounts usage)
	{
		var parts = new List<string>();
		if (usage.Allocations > 0)
			parts.Add($"{usage.Allocations} subject allocation(s)");
		if (usage.StudentSelections > 0)
			parts.Add($"{usage.StudentSelections} student elective assignment(s)");
		if (usage.WeeklySlots > 0)
			parts.Add($"{usage.WeeklySlots} weekly lecture slot(s)");
		if (usage.TimetableSlots > 0)
			parts.Add($"{usage.TimetableSlots} timetable slot(s)");
		if (usage.ExamSubjects > 0)
			parts.Add($"{usage.ExamSubjects} exam(s)");
		return parts;
	}

	private static string NormalizeName(string name)
	{
		var normalized = new string(name.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
		return normalized switch
		{
			"SECONDLANG" => "SECONDLANGUAGE",
			"THIRDLANG" => "THIRDLANGUAGE",
			"SKILL" => "SKILLSUBJECT",
			"OPTIONAL" => "OPTIONALSUBJECT",
			"VOCATIONAL" => "VOCATIONALSUBJECT",
			_ => normalized
		};
	}

	private static bool IsSimilarName(string left, string right)
	{
		return left.Length >= 4 && right.Length >= 4 && (left.Contains(right) || right.Contains(left));
	}

	private sealed class ElectiveGroupUsageCounts
	{
		public int Allocations { get; set; }
		public int StudentSelections { get; set; }
		public int WeeklySlots { get; set; }
		public int TimetableSlots { get; set; }
		public int ExamSubjects { get; set; }
		public int Total => Allocations + StudentSelections + WeeklySlots + TimetableSlots + ExamSubjects;
	}
}
