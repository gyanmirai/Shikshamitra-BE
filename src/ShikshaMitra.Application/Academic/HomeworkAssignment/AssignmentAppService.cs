using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service for Assignment API
/// Implements multi-division assignment management with performance optimizations
/// </summary>
[Authorize]
public class AssignmentAppService : ApplicationService, IAssignmentAppService
{
	private readonly IRepository<AssignmentEntity, Guid> _assignmentRepository;
	private readonly IRepository<AssignmentDivisionSchedule, Guid> _scheduleRepository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IAssignmentTypeRepository _assignmentTypeRepository;
	private readonly IRepository<IdentityUser, Guid> _userRepository;
	private readonly IUserScopeAccessService _userScopeAccessService;
	private readonly AssignmentManager _assignmentManager;
	private readonly IAsyncQueryableExecuter _asyncExecuter;
	private readonly IRepository<StudentSubmissionEntity, Guid> _studentSubmissionRepository;

	public AssignmentAppService(
		IRepository<AssignmentEntity, Guid> assignmentRepository,
		IRepository<AssignmentDivisionSchedule, Guid> scheduleRepository,
		IDivisionRepository divisionRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Subject, Guid> subjectRepository,
		IAssignmentTypeRepository assignmentTypeRepository,
		IRepository<IdentityUser, Guid> userRepository,
		IUserScopeAccessService userScopeAccessService,
		AssignmentManager assignmentManager,
		IAsyncQueryableExecuter asyncExecuter,
		IRepository<StudentSubmissionEntity, Guid> studentSubmissionRepository
	)
	{
		_assignmentRepository = assignmentRepository;
		_scheduleRepository = scheduleRepository;
		_divisionRepository = divisionRepository;
		_standardRepository = standardRepository;
		_subjectRepository = subjectRepository;
		_assignmentTypeRepository = assignmentTypeRepository;
		_userRepository = userRepository;
		_userScopeAccessService = userScopeAccessService;
		_assignmentManager = assignmentManager;
		_asyncExecuter = asyncExecuter;
		_studentSubmissionRepository = studentSubmissionRepository;
	}

	/// <summary>
	/// Create a new assignment with multi-division scheduling
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add
	)]
	public async Task<AssignmentCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateAssignmentDto input
	)
	{
		// 1. Validate academic medium exists
		if (academicMediumId == Guid.Empty)
		{
			throw new BusinessException("ShikshaMitra.Assignment:InvalidAcademicMedium");
		}

		// 2. Validate assignment type
		await _assignmentManager.ValidateAssignmentTypeAsync(input.AssignmentTypeId);

		// 3. Get academic year from medium (assuming relationship exists)
		// For now, we'll pass null and let the entity handle it
		var academicYearId = (Guid?) null; // TODO: Get from medium relationship

		// 4. Validate divisions and dates
		var divisionData = input
			.Divisions.Select(d => (d.DivisionId, d.PublishDate, d.DueDate))
			.ToList();
		if (academicYearId.HasValue)
		{
			await _assignmentManager.ValidateAssignmentAsync(
				academicYearId.Value,
				input.ClassId,
				divisionData
			);
		}

		// 5. Create assignment entity
		var assignment = new AssignmentEntity(
			GuidGenerator.Create(),
			input.ClassId,
			input.SubjectId,
			input.Title,
			input.Description,
			input.AssignmentTypeId,
			(SubmissionType) input.SubmissionType,
			totalMarks: 0m,
			tenantId: CurrentTenant.Id,
			academicYearId: academicYearId,
			attachmentsJson: input.Attachments != null && input.Attachments.Any()
				? JsonSerializer.Serialize(input.Attachments)
				: null,
			educationMediumId: academicMediumId
		);

		// 6. Add division schedules
		foreach (var divisionInput in input.Divisions)
		{
			var schedule = new AssignmentDivisionSchedule(
				GuidGenerator.Create(),
				assignment.Id,
				divisionInput.DivisionId,
				divisionInput.PublishDate,
				divisionInput.DueDate,
				CurrentTenant.Id
			);
			assignment.AddDivisionSchedule(schedule);
		}

		// 7. Save to database
		await _assignmentRepository.InsertAsync(assignment, autoSave: true);

		// 8. Map to DTO and return
		var dto = await MapToAssignmentDtoAsync(assignment);

		return new AssignmentCreateResponseDto
		{
			Message = L["ShikshaMitra.Assignment:CreatedSuccessfully"],
			Data = dto
		};
	}

	/// <summary>
	/// Get all assignments with filters and UserScope filtering
	/// Optimized with eager loading and projection
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View
	)]
	public async Task<AssignmentListResponseDto> GetAllAsync(
		Guid academicMediumId,
		GetAssignmentFilterDto filter
	)
	{
		// 1. Build query with eager loading
		var query = await _assignmentRepository.WithDetailsAsync(a =>
			a.DivisionSchedules,
			a => a.AssignmentType
		);

		query = await _userScopeAccessService.ApplyCurrentUserScopeAsync(query);

		// 2. Apply medium filter
		query = query.Where(a => a.EducationMediumId == academicMediumId);

		// 3. Apply optional filters
		if (filter.ClassId.HasValue)
		{
			query = query.Where(a => a.StandardId == filter.ClassId.Value);
		}

		if (filter.SubjectId.HasValue)
		{
			query = query.Where(a => a.SubjectId == filter.SubjectId.Value);
		}

		if (!string.IsNullOrEmpty(filter.Status))
		{
			switch (filter.Status.ToLower())
			{
				case "draft":
					query = query.Where(a => !a.DivisionSchedules.Any(d => d.IsPublished));
					break;
				case "published":
					query = query.Where(a => a.DivisionSchedules.All(d => d.IsPublished));
					break;
				case "partially_published":
					query = query.Where(a =>
						a.DivisionSchedules.Any(d => d.IsPublished)
						&& a.DivisionSchedules.Any(d => !d.IsPublished)
					);
					break;
			}
		}

		// 4. Order by creation time (newest first)
		query = query.OrderByDescending(a => a.CreationTime);

		// 5. Execute query
		var assignments = await _asyncExecuter.ToListAsync(query);

		// 6. Batch-load related entities to prevent N+1 queries
		var standardIds = assignments.Select(a => a.StandardId).Distinct().ToList();
		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var divisionIds = assignments
			.SelectMany(a => a.DivisionSchedules.Select(d => d.DivisionId))
			.Distinct()
			.ToList();
		var creatorIds = assignments
			.Where(a => a.CreatorId.HasValue)
			.Select(a => a.CreatorId!.Value)
			.Distinct()
			.ToList();

		var standards = await _standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
		var subjects = await _subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var divisions = await _divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
		var creators = creatorIds.Any()
			? await _userRepository.GetListAsync(u => creatorIds.Contains(u.Id))
			: new List<IdentityUser>();
		var assignmentIds = assignments.Select(a => a.Id).ToList();
		var submissions = assignmentIds.Any()
			? await _studentSubmissionRepository.GetListAsync(s =>
				s.AssignmentId.HasValue && assignmentIds.Contains(s.AssignmentId.Value))
			: new List<StudentSubmissionEntity>();
		var submissionLookup = submissions
			.GroupBy(s => s.AssignmentId!.Value)
			.ToDictionary(g => g.Key, g => BuildSubmissionSummary(g));

		// Create lookup dictionaries for O(1) access
		var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
		var subjectLookup = subjects.ToDictionary(s => s.Id, s => s.Name);
		var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
		var creatorLookup = creators.ToDictionary(
			c => c.Id,
			c => $"{c.Name} {c.Surname}".Trim()
		);

		// 7. Map to DTOs with batch-loaded data
		var dtos = new List<AssignmentDto>();
		foreach (var assignment in assignments)
		{
			var dto = await MapToAssignmentDtoAsync(
				assignment,
				standardLookup,
				subjectLookup,
				divisionLookup,
				creatorLookup,
				submissionLookup.GetValueOrDefault(assignment.Id)
			);
			dtos.Add(dto);
		}

		return new AssignmentListResponseDto { Data = dtos };
	}

	/// <summary>
	/// Get assignment by ID with full details
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View
	)]
	public async Task<AssignmentGetResponseDto> GetAsync(Guid id, Guid academicMediumId)
	{
		// 1. Get assignment with eager loading
		var queryable = await _assignmentRepository.WithDetailsAsync(a =>
			a.DivisionSchedules,
			a => a.AssignmentType
		);

		var assignment = await _asyncExecuter.FirstOrDefaultAsync(
			queryable.Where(a => a.Id == id && a.EducationMediumId == academicMediumId)
		);

		if (assignment == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);
		}

		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			assignment,
			operation: nameof(GetAsync));

		// 3. Map to DTO
		var dto = await MapToAssignmentDtoAsync(assignment);

		return new AssignmentGetResponseDto { Data = dto };
	}

	/// <summary>
	/// Update assignment (only if not published)
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add
	)]
	public async Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateAssignmentDto input
	)
	{
		// 1. Validate can update (not published)
		await _assignmentManager.ValidateCanUpdateAsync(id);

		// 2. Get assignment
		var assignment = await _assignmentRepository.GetAsync(id);

		if (assignment.EducationMediumId != academicMediumId)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);
		}
		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			assignment,
			operation: nameof(UpdateAsync));

		// 3. Validate assignment type
		await _assignmentManager.ValidateAssignmentTypeAsync(input.AssignmentTypeId);

		// 4. Validate divisions and dates
		var divisionData = input
			.Divisions.Select(d => (d.DivisionId, d.PublishDate, d.DueDate))
			.ToList();
		if (assignment.AcademicYearId.HasValue)
		{
			await _assignmentManager.ValidateAssignmentAsync(
				assignment.AcademicYearId.Value,
				input.ClassId,
				divisionData
			);
		}

		// 5. Update basic info
		assignment.UpdateBasicInfo(
			input.Title,
			input.Description,
			totalMarks: 0m,
			attachmentsJson: input.Attachments != null && input.Attachments.Any()
				? JsonSerializer.Serialize(input.Attachments)
				: null
		);

		// Update other properties
		assignment.StandardId = input.ClassId;
		assignment.SubjectId = input.SubjectId;
		assignment.AssignmentTypeId = input.AssignmentTypeId;
		assignment.SubmissionType = (SubmissionType) input.SubmissionType;
		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			assignment,
			operation: $"{nameof(UpdateAsync)}:TargetScope");

		// 6. Update division schedules (delete old, add new)
		// Capture existing schedules before clearing
		var schedulesToDelete = assignment.DivisionSchedules.ToList();
		await _scheduleRepository.DeleteManyAsync(schedulesToDelete);
		assignment.ClearDivisionSchedules();

		foreach (var divisionInput in input.Divisions)
		{
			var schedule = new AssignmentDivisionSchedule(
				GuidGenerator.Create(),
				assignment.Id,
				divisionInput.DivisionId,
				divisionInput.PublishDate,
				divisionInput.DueDate,
				CurrentTenant.Id
			);
			assignment.AddDivisionSchedule(schedule);
		}

		// 7. Save changes
		await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

		return new MessageResponseDto
		{
			Message = L["ShikshaMitra.Assignment:UpdatedSuccessfully"]
		};
	}

	/// <summary>
	/// Manually publish assignment for all divisions
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add
	)]
	public async Task<AssignmentPublishResponseDto> PublishAsync(
		Guid id,
		Guid academicMediumId
	)
	{
		// 1. Validate can publish
		var assignment = await _assignmentManager.ValidateCanPublishAsync(id);

		if (assignment.EducationMediumId != academicMediumId)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);
		}
		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			assignment,
			operation: nameof(PublishAsync));

		// 2. Publish all divisions
		if (assignment.Status == AssignmentStatus.Draft)
		{
			assignment.ChangeStatus(AssignmentStatus.Published);
		}

		assignment.PublishAll();

		// 3. Save changes
		await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

		// 4. Get earliest publish date
		var publishedAt = assignment.DivisionSchedules
			.Where(d => d.PublishedAt.HasValue)
			.Select(d => d.PublishedAt!.Value)
			.DefaultIfEmpty(DateTime.UtcNow)
			.Min();

		return new AssignmentPublishResponseDto
		{
			Message = L["ShikshaMitra.Assignment:PublishedSuccessfully"],
			Data = new PublishDataDto { Id = assignment.Id, PublishedAt = publishedAt }
		};
	}

	/// <summary>
	/// Delete assignment (soft delete)
	/// </summary>
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Delete
	)]
	public async Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId)
	{
		// 1. Get assignment
		var q = await _assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
		var assignment = await _asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == id))
			?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);

		if (assignment.EducationMediumId != academicMediumId)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);
		}
		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			assignment,
			operation: nameof(DeleteAsync));

		if (assignment.Status != AssignmentStatus.Draft || assignment.HasPublishedDivisions)
		{
			throw new BusinessException("ShikshaMitra.Assignment:CannotDeletePublished")
				.WithData("AssignmentId", id)
				.WithData("Status", assignment.Status);
		}

		var submissionsCount = await _studentSubmissionRepository.CountAsync(s => s.AssignmentId == id);
		if (submissionsCount > 0)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.AssignmentIsInUse)
				.WithData("message", $"Cannot delete assignment '{assignment.Title}' because it is in use: {submissionsCount} student submission(s). Please remove these submissions first.");
		}

		// 2. Soft delete
		await _assignmentRepository.DeleteAsync(assignment);

		return new MessageResponseDto
		{
			Message = L["ShikshaMitra.Assignment:DeletedSuccessfully"]
		};
	}

	#region Helper Methods

	/// <summary>
	/// Map AssignmentEntity to AssignmentDto with related data
	/// Performance optimized with batched queries when lookups provided
	/// </summary>
	/// <param name="assignment">The assignment entity to map</param>
	/// <param name="standardLookup">Optional pre-loaded standard names by ID</param>
	/// <param name="subjectLookup">Optional pre-loaded subject names by ID</param>
	/// <param name="divisionLookup">Optional pre-loaded division names by ID</param>
	/// <param name="creatorLookup">Optional pre-loaded creator names by ID</param>
	private async Task<AssignmentDto> MapToAssignmentDtoAsync(
		AssignmentEntity assignment,
		Dictionary<Guid, string>? standardLookup = null,
		Dictionary<Guid, string>? subjectLookup = null,
		Dictionary<Guid, string>? divisionLookup = null,
		Dictionary<Guid, string>? creatorLookup = null,
		LearningTaskSubmissionSummary? submissionSummary = null
	)
	{
		// 1. Base mapping
		var dto = ObjectMapper.Map<AssignmentEntity, AssignmentDto>(assignment);

		// 1.1. Deserialize JSON fields
		if (!string.IsNullOrEmpty(assignment.ChapterIdsJson))
		{
			var chapterIds = JsonSerializer.Deserialize<Guid[]>(assignment.ChapterIdsJson);
			if (chapterIds != null)
			{
				dto.Chapters = chapterIds.Select(id => new ChapterInfoDto
				{
					ChapterId = id,
					ChapterName = string.Empty // Will be populated below if needed
				}).ToList();
			}
		}

		if (!string.IsNullOrEmpty(assignment.AttachmentsJson))
		{
			dto.Attachments = AssignmentAttachmentSerializer.ReadLegacyList(assignment.AttachmentsJson);
		}

		// 2. Get Standard name
		if (standardLookup != null && standardLookup.TryGetValue(assignment.StandardId, out var standardName))
		{
			dto.ClassName = standardName;
		}
		else
		{
			var standard = await _standardRepository.FindAsync(assignment.StandardId);
			dto.ClassName = standard?.Name ?? string.Empty;
		}

		// 3. Get Subject name
		if (subjectLookup != null && subjectLookup.TryGetValue(assignment.SubjectId, out var subjectName))
		{
			dto.SubjectName = subjectName;
		}
		else
		{
			var subject = await _subjectRepository.FindAsync(assignment.SubjectId);
			dto.SubjectName = subject?.Name ?? string.Empty;
		}

		// 4. Get AssignmentType name
		dto.AssignmentTypeName = assignment.AssignmentType?.Name ?? string.Empty;
		dto.DefaultSubmissionType = assignment.AssignmentType?.DefaultSubmissionType.HasValue == true
			? (int) assignment.AssignmentType.DefaultSubmissionType.Value
			: null;
		dto.DefaultMarks = assignment.AssignmentType?.DefaultMarks;
		dto.DefaultDueDays = assignment.AssignmentType?.DefaultDueDays;
		dto.EvaluationRequired = assignment.AssignmentType?.EvaluationRequired ?? true;
		dto.DefaultEvaluationMethod = assignment.AssignmentType?.DefaultEvaluationMethod.HasValue == true
			? (int) assignment.AssignmentType.DefaultEvaluationMethod.Value
			: null;
		dto.AssignmentTypeStatus = assignment.AssignmentType != null
			? (int) assignment.AssignmentType.Status
			: null;

		// 5. Get Division names
		if (dto.Divisions != null && dto.Divisions.Any())
		{
			if (divisionLookup != null)
			{
				// Use pre-loaded lookup
				foreach (var divSchedule in dto.Divisions)
				{
					divSchedule.DivisionName = divisionLookup.TryGetValue(
						divSchedule.DivisionId,
						out var divName
					)
						? divName
						: string.Empty;
				}
			}
			else
			{
				// Fallback to individual query
				var divisionIds = dto.Divisions.Select(d => d.DivisionId).ToList();
				var divisions = await _divisionRepository.GetListAsync(d =>
					divisionIds.Contains(d.Id)
				);

				foreach (var divSchedule in dto.Divisions)
				{
					var division = divisions.FirstOrDefault(d => d.Id == divSchedule.DivisionId);
					divSchedule.DivisionName = division?.Name ?? string.Empty;
				}
			}
		}

		// 6. Get creator info
		if (assignment.CreatorId.HasValue)
		{
			if (
				creatorLookup != null
				&& creatorLookup.TryGetValue(assignment.CreatorId.Value, out var creatorName)
			)
			{
				dto.CreatedBy = new CreatedByDto
				{
					Id = assignment.CreatorId.Value,
					Name = creatorName
				};
			}
			else
			{
				var creator = await _userRepository.FindAsync(assignment.CreatorId.Value);
				dto.CreatedBy = new CreatedByDto
				{
					Id = assignment.CreatorId.Value,
					Name = creator != null ? $"{creator.Name} {creator.Surname}".Trim() : string.Empty
				};
			}
		}

		if (submissionSummary == null)
		{
			var submissions = await _studentSubmissionRepository.GetListAsync(s => s.AssignmentId == assignment.Id);
			submissionSummary = BuildSubmissionSummary(submissions);
		}

		dto.TotalStudents = submissionSummary.TotalStudents;
		dto.SubmittedCount = submissionSummary.SubmittedCount;
		dto.PendingCount = submissionSummary.PendingCount;
		dto.LateCount = submissionSummary.LateCount;
		dto.CheckedCount = submissionSummary.CheckedCount;
		dto.EvaluationPendingCount = submissionSummary.EvaluationPendingCount;

		return dto;
	}

	private static LearningTaskSubmissionSummary BuildSubmissionSummary(IEnumerable<StudentSubmissionEntity> submissions)
	{
		var list = submissions.ToList();
		var submittedCount = list.Count(s =>
			s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.Reviewed);
		var lateCount = list.Count(s => s.Status == SubmissionStatus.Late);
		var checkedCount = list.Count(s => s.Status == SubmissionStatus.Reviewed);
		var evaluationPendingCount = list.Count(s =>
			s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview);

		return new LearningTaskSubmissionSummary(
			TotalStudents: 0,
			SubmittedCount: submittedCount,
			PendingCount: 0,
			LateCount: lateCount,
			CheckedCount: checkedCount,
			EvaluationPendingCount: evaluationPendingCount
		);
	}

	private sealed record LearningTaskSubmissionSummary(
		int TotalStudents,
		int SubmittedCount,
		int PendingCount,
		int LateCount,
		int CheckedCount,
		int EvaluationPendingCount
	);

	#endregion
}
