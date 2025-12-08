using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service for Subject Assignment operations
/// </summary>
[RemoteService(IsEnabled = false)]
public class SubjectAssignmentAppService(
	IRepository<StandardSubjectAssignment, Guid> assignmentRepository,
	IRepository<Subject, Guid> subjectRepository,
	StandardSubjectAssignmentManager assignmentManager,
	IGuidGenerator guidGenerator,
	ILogger<SubjectAssignmentAppService> logger
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task<SubjectAssignmentGroupResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateSubjectAssignmentGroupDto input
	)
	{
		logger.LogInformation(
			"Creating subject assignment group '{GroupName}' for standard {StandardId} in medium {MediumId}",
			input.SubjectName,
			input.ClassId,
			academicMediumId
		);

		try
		{
			// Convert int to enum
			var status = (SubjectStatus)input.Status;
			var subjectType = (SubjectType)input.SubjectType;

			// Validate group name uniqueness
			await assignmentManager.ValidateGroupNameAsync(input.ClassId, input.SubjectName);

			// Validate subject codes uniqueness
			var subjectCodes = input.Subjects.Select(s => s.SubjectCode).ToList();
			await assignmentManager.ValidateSubjectCodesAsync(input.ClassId, subjectCodes);

			// Get subjects by name and validate they belong to the medium
			var subjectNames = input.Subjects.Select(s => s.SubjectName).Distinct().ToList();
			var subjects = await subjectRepository.GetListAsync(s =>
				subjectNames.Contains(s.Name) && s.EducationMediumId == academicMediumId
			);

			if (subjects.Count != subjectNames.Count)
			{
				var foundNames = subjects.Select(s => s.Name).ToList();
				var missingNames = subjectNames.Where(n => !foundNames.Contains(n)).ToList();
				throw new BusinessException("SubjectAssignment:SubjectsNotFound")
					.WithData("MissingSubjects", string.Join(", ", missingNames));
			}

			// Generate group ID for all assignments
			var groupId = guidGenerator.Create();
			var assignments = new List<StandardSubjectAssignment>();

			// Create assignment for each subject
			foreach (var subjectDetail in input.Subjects)
			{
				var subject = subjects.First(s => s.Name == subjectDetail.SubjectName);

				var assignment = assignmentManager.AssignSubject(
					standardId: input.ClassId,
					subjectId: subject.Id,
					subjectCode: subjectDetail.SubjectCode,
					educationMediumId: academicMediumId,
					status: status,
					subjectType: subjectType,
					learningGoals: subjectDetail.LearningGoal,
					prerequisites: subjectDetail.Prerequisites,
					description: subjectDetail.Description,
					subjectColor: subjectDetail.SubjectColor
				);

				// Set group info
				assignment.SetGroupInfo(input.SubjectName, groupId);
				assignment.SetIsSubjectOpen(subjectDetail.IsSubjectOpen);

				// Set documents
				if (subjectDetail.Documents?.Any() == true)
				{
					var documentsJson = JsonSerializer.Serialize(subjectDetail.Documents);
					assignment.SetDocuments(documentsJson);
				}

				// Set teachers
				if (!string.IsNullOrEmpty(subjectDetail.PrimaryTeacher) &&
					Guid.TryParse(subjectDetail.PrimaryTeacher, out var primaryTeacherId))
				{
					assignment.SetPrimaryTeacher(primaryTeacherId);
				}

				if (subjectDetail.SecondaryTeacher?.Any() == true)
				{
					var secondaryTeacherIds = subjectDetail.SecondaryTeacher
						.Where(id => Guid.TryParse(id, out _))
						.Select(id => Guid.Parse(id))
						.ToList();

					if (secondaryTeacherIds.Any())
					{
						assignment.SetSecondaryTeachers(secondaryTeacherIds);
					}
				}

				assignments.Add(assignment);
			}

			// Save all assignments
			await assignmentRepository.InsertManyAsync(assignments, autoSave: true);

			logger.LogInformation(
				"Successfully created {Count} subject assignments in group '{GroupName}'",
				assignments.Count,
				input.SubjectName
			);

			// Return the first assignment as representative (they all share the same group)
			return await MapToResponseDtoAsync(assignments.First(), assignments);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to create subject assignment group '{GroupName}'",
				input.SubjectName
			);
			throw new UserFriendlyException(
				"Failed to create subject assignment. Please try again later."
			);
		}
	}

	public async Task<List<SubjectAssignmentGroupResponseDto>> GetListAsync(
		Guid academicMediumId,
		Guid classId,
		int? status = null
	)
	{
		logger.LogInformation(
			"Getting subject assignments for standard {StandardId} in medium {MediumId} with status {Status}",
			classId,
			academicMediumId,
			status
		);

		var query = await assignmentRepository.GetQueryableAsync();

		query = query.Where(a =>
			a.StandardId == classId &&
			a.EducationMediumId == academicMediumId
		);

		if (status.HasValue)
		{
			var statusEnum = (SubjectStatus)status.Value;
			query = query.Where(a => a.Status == statusEnum);
		}

		var assignments = query.ToList();

		// Group by AssignmentGroupId
		var groups = assignments
			.Where(a => a.AssignmentGroupId.HasValue)
			.GroupBy(a => a.AssignmentGroupId!.Value)
			.ToList();

		var result = new List<SubjectAssignmentGroupResponseDto>();

		foreach (var group in groups)
		{
			var firstAssignment = group.First();
			var groupAssignments = group.ToList();
			var responseDto = await MapToResponseDtoAsync(firstAssignment, groupAssignments);
			result.Add(responseDto);
		}

		return result;
	}

	public async Task<SubjectAssignmentGroupResponseDto> GetAsync(Guid assignmentId)
	{
		logger.LogInformation("Getting subject assignment group {AssignmentId}", assignmentId);

		var assignment = await assignmentRepository.FindAsync(assignmentId);

		if (assignment == null)
		{
			throw new BusinessException("SubjectAssignment:NotFound")
				.WithData("AssignmentId", assignmentId);
		}

		// Get all assignments in the same group
		List<StandardSubjectAssignment> groupAssignments;

		if (assignment.AssignmentGroupId.HasValue)
		{
			groupAssignments = await assignmentRepository.GetListAsync(a =>
				a.AssignmentGroupId == assignment.AssignmentGroupId.Value
			);
		}
		else
		{
			groupAssignments = new List<StandardSubjectAssignment> { assignment };
		}

		return await MapToResponseDtoAsync(assignment, groupAssignments);
	}

	public async Task<SubjectAssignmentGroupResponseDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		UpdateSubjectAssignmentGroupDto input
	)
	{
		logger.LogInformation(
			"Updating subject assignment group {AssignmentId}",
			assignmentId
		);

		try
		{
			var assignment = await assignmentRepository.GetAsync(assignmentId);

			if (assignment.EducationMediumId != academicMediumId)
			{
				throw new BusinessException("SubjectAssignment:MediumMismatch");
			}

			// Validate group name uniqueness (exclude current group)
			await assignmentManager.ValidateGroupNameAsync(
				input.ClassId,
				input.SubjectName,
				assignment.AssignmentGroupId
			);

			// Get all assignments in the group
			var groupAssignments = assignment.AssignmentGroupId.HasValue
				? await assignmentRepository.GetListAsync(a =>
					a.AssignmentGroupId == assignment.AssignmentGroupId.Value)
				: new List<StandardSubjectAssignment> { assignment };

			// Delete all existing assignments in the group
			await assignmentRepository.DeleteManyAsync(groupAssignments, autoSave: true);

			// Create new assignments (similar to Create logic)
			var status = (SubjectStatus)input.Status;
			var subjectType = (SubjectType)input.SubjectType;

			// Validate subject codes
			var subjectCodes = input.Subjects.Select(s => s.SubjectCode).ToList();
			await assignmentManager.ValidateSubjectCodesAsync(input.ClassId, subjectCodes);

			// Get subjects
			var subjectNames = input.Subjects.Select(s => s.SubjectName).Distinct().ToList();
			var subjects = await subjectRepository.GetListAsync(s =>
				subjectNames.Contains(s.Name) && s.EducationMediumId == academicMediumId
			);

			var groupId = assignment.AssignmentGroupId ?? guidGenerator.Create();
			var newAssignments = new List<StandardSubjectAssignment>();

			foreach (var subjectDetail in input.Subjects)
			{
				var subject = subjects.First(s => s.Name == subjectDetail.SubjectName);

				var newAssignment = assignmentManager.AssignSubject(
					standardId: input.ClassId,
					subjectId: subject.Id,
					subjectCode: subjectDetail.SubjectCode,
					educationMediumId: academicMediumId,
					status: status,
					subjectType: subjectType,
					learningGoals: subjectDetail.LearningGoal,
					prerequisites: subjectDetail.Prerequisites,
					description: subjectDetail.Description,
					subjectColor: subjectDetail.SubjectColor
				);

				newAssignment.SetGroupInfo(input.SubjectName, groupId);
				newAssignment.SetIsSubjectOpen(subjectDetail.IsSubjectOpen);

				if (subjectDetail.Documents?.Any() == true)
				{
					var documentsJson = JsonSerializer.Serialize(subjectDetail.Documents);
					newAssignment.SetDocuments(documentsJson);
				}

				if (!string.IsNullOrEmpty(subjectDetail.PrimaryTeacher) &&
					Guid.TryParse(subjectDetail.PrimaryTeacher, out var primaryTeacherId))
				{
					newAssignment.SetPrimaryTeacher(primaryTeacherId);
				}

				if (subjectDetail.SecondaryTeacher?.Any() == true)
				{
					var secondaryTeacherIds = subjectDetail.SecondaryTeacher
						.Where(id => Guid.TryParse(id, out _))
						.Select(id => Guid.Parse(id))
						.ToList();

					if (secondaryTeacherIds.Any())
					{
						newAssignment.SetSecondaryTeachers(secondaryTeacherIds);
					}
				}

				newAssignments.Add(newAssignment);
			}

			await assignmentRepository.InsertManyAsync(newAssignments, autoSave: true);

			logger.LogInformation(
				"Successfully updated subject assignment group {AssignmentId}",
				assignmentId
			);

			return await MapToResponseDtoAsync(newAssignments.First(), newAssignments);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to update subject assignment group {AssignmentId}",
				assignmentId
			);
			throw new UserFriendlyException(
				"Failed to update subject assignment. Please try again later."
			);
		}
	}

	public async Task<PublishResponseDto> PublishAsync(Guid assignmentId)
	{
		logger.LogInformation("Publishing subject assignment group {AssignmentId}", assignmentId);

		var assignment = await assignmentRepository.GetAsync(assignmentId);

		if (assignment.Status == SubjectStatus.Published)
		{
			throw new BusinessException("SubjectAssignment:AlreadyPublished");
		}

		// Get all assignments in the group and publish them
		var groupAssignments = assignment.AssignmentGroupId.HasValue
			? await assignmentRepository.GetListAsync(a =>
				a.AssignmentGroupId == assignment.AssignmentGroupId.Value)
			: new List<StandardSubjectAssignment> { assignment };

		var publishedAt = DateTime.UtcNow;

		foreach (var groupAssignment in groupAssignments)
		{
			groupAssignment.Publish();
		}

		await assignmentRepository.UpdateManyAsync(groupAssignments, autoSave: true);

		logger.LogInformation(
			"Successfully published {Count} assignments in group",
			groupAssignments.Count
		);

		return new PublishResponseDto
		{
			Id = assignmentId,
			Status = (int)SubjectStatus.Published,
			PublishedAt = publishedAt
		};
	}

	public async Task DeleteAsync(Guid assignmentId)
	{
		logger.LogInformation("Deleting subject assignment group {AssignmentId}", assignmentId);

		var assignment = await assignmentRepository.FindAsync(assignmentId);

		if (assignment == null)
		{
			throw new BusinessException("SubjectAssignment:NotFound")
				.WithData("AssignmentId", assignmentId);
		}

		// Get all assignments in the group
		var groupAssignments = assignment.AssignmentGroupId.HasValue
			? await assignmentRepository.GetListAsync(a =>
				a.AssignmentGroupId == assignment.AssignmentGroupId.Value)
			: new List<StandardSubjectAssignment> { assignment };

		await assignmentRepository.DeleteManyAsync(groupAssignments, autoSave: true);

		logger.LogInformation(
			"Successfully deleted {Count} assignments in group",
			groupAssignments.Count
		);
	}

	private async Task<SubjectAssignmentGroupResponseDto> MapToResponseDtoAsync(
		StandardSubjectAssignment representativeAssignment,
		List<StandardSubjectAssignment> groupAssignments
	)
	{
		var response = new SubjectAssignmentGroupResponseDto
		{
			Id = representativeAssignment.AssignmentGroupId ?? representativeAssignment.Id,
			ClassId = representativeAssignment.StandardId,
			SubjectName = representativeAssignment.AssignmentGroupName ?? "Unnamed Group",
			Status = (int)representativeAssignment.Status,
			SubjectType = (int)representativeAssignment.SubjectType,
			AcademicMediumId = representativeAssignment.EducationMediumId,
			CreatedAt = representativeAssignment.CreationTime,
			PublishedAt = representativeAssignment.PublishedAt,
			Subjects = new List<SubjectAssignmentDetailResponseDto>()
		};

		// Get all subject IDs to load subject details
		var subjectIds = groupAssignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));

		foreach (var assignment in groupAssignments)
		{
			var subject = subjects.FirstOrDefault(s => s.Id == assignment.SubjectId);

			var subjectResponse = new SubjectAssignmentDetailResponseDto
			{
				Id = assignment.Id,
				SubjectCode = assignment.SubjectCode,
				SubjectId = assignment.SubjectId,
				SubjectName = subject?.Name ?? "Unknown",
				SubjectColor = assignment.SubjectColor ?? "#000000",
				LearningGoal = assignment.LearningGoals,
				Prerequisites = assignment.Prerequisites,
				Description = assignment.Description,
				IsSubjectOpen = assignment.IsSubjectOpen
			};

			// Deserialize documents
			if (!string.IsNullOrEmpty(assignment.Documents))
			{
				try
				{
					subjectResponse.Documents = JsonSerializer.Deserialize<List<SubjectDocumentDto>>(
						assignment.Documents
					) ?? new List<SubjectDocumentDto>();
				}
				catch
				{
					subjectResponse.Documents = new List<SubjectDocumentDto>();
				}
			}

			// Get teacher info
			var primaryTeacher = assignment.Teachers.FirstOrDefault(t =>
				t.TeacherType == SubjectTeacherType.Primary
			);

			if (primaryTeacher != null)
			{
				subjectResponse.PrimaryTeacher = primaryTeacher.TeacherId.ToString();
				// TODO: Fetch teacher name from User/Teacher repository
				subjectResponse.PrimaryTeacherName = "Teacher"; // Placeholder
			}

			var secondaryTeachers = assignment.Teachers
				.Where(t => t.TeacherType == SubjectTeacherType.Secondary)
				.ToList();

			if (secondaryTeachers.Any())
			{
				subjectResponse.SecondaryTeacher = secondaryTeachers
					.Select(t => t.TeacherId.ToString())
					.ToList();

				subjectResponse.SecondaryTeacherInfo = secondaryTeachers
					.Select(t => new TeacherInfoDto
					{
						Id = t.TeacherId.ToString(),
						Name = "Teacher" // Placeholder
					})
					.ToList();
			}

			response.Subjects.Add(subjectResponse);
		}

		return response;
	}
}
