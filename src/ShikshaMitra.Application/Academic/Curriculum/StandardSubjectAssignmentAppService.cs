using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class StandardSubjectAssignmentAppService(
	IRepository<AssignedSubject, Guid> assignmentRepository,
	StandardSubjectAssignmentManager subjectAssignmentManager,
	IObjectMapper objectMapper,
	ILogger<StandardSubjectAssignmentAppService> logger
) : ApplicationService, IStandardSubjectAssignmentAppService
{
	public async Task<StandardSubjectAssignmentResponseDto> CreateAssignmentAsync(
		CreateStandardSubjectAssignmentDto createAssignmentDto
	)
	{
		// Validate EducationMediumId is provided and not empty
		if (createAssignmentDto.EducationMediumId == Guid.Empty)
		{
			throw new BusinessException(
				"ShikshaMitra:SubjectAssignment:EducationMediumIdRequired",
				"Education Medium ID is required and cannot be empty."
			);
		}

		try
		{
			// Use SubjectType from DTO, default to Core if not provided
			var subjectType = createAssignmentDto.SubjectType ?? SubjectType.Core;

			var assignment = await subjectAssignmentManager.AssignSubjectAsync(
				standardId: createAssignmentDto.StandardId,
				subjectId: createAssignmentDto.SubjectId,
				subjectCode: createAssignmentDto.SubjectCode,
				status: createAssignmentDto.Status,
				subjectType: subjectType,
				learningGoals: createAssignmentDto.LearningGoals,
				prerequisites: createAssignmentDto.Prerequisites,
				description: createAssignmentDto.Description
			);

			if (createAssignmentDto.PrimaryTeacherId.HasValue)
			{
				assignment.SetPrimaryTeacher(createAssignmentDto.PrimaryTeacherId.Value);
			}

			if (
				createAssignmentDto.SecondaryTeacherIds != null
				&& createAssignmentDto.SecondaryTeacherIds.Count > 0
			)
			{
				assignment.SetSecondaryTeachers(createAssignmentDto.SecondaryTeacherIds);
			}

			await assignmentRepository.InsertAsync(assignment);

			return objectMapper.Map<
				AssignedSubject,
				StandardSubjectAssignmentResponseDto
			>(assignment);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to assign subject for StandardId: {StandardId}, SubjectId: {SubjectId}",
				createAssignmentDto.StandardId,
				createAssignmentDto.SubjectId
			);
			throw new UserFriendlyException(
				"SubjectAssignmentCreationFailed",
				"Failed to create subject assignment. Please try again later."
			);
		}
	}

	public async Task ChangeStatusAsync(Guid assignmentId, SubjectStatus status)
	{
		try
		{
			var assignment =
				await assignmentRepository.FindAsync(assignmentId)
				?? throw new BusinessException(
					"SubjectAssignmentNotFound",
					"Subject assignment not found."
				);

			assignment.ChangeStatus(status);

			await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to change status for assignmentId: {AssignmentId}",
				assignmentId
			);
			throw new BusinessException(
				"SubjectAssignmentStatusChangeFailed",
				"Failed to change subject assignment status."
			);
		}
	}

	public async Task<List<StandardSubjectAssignmentResponseDto>> GetListAsync(Guid standardId)
	{
		try
		{
			var assignments = await assignmentRepository.GetListAsync(a =>
				a.StandardId == standardId
			);
			return objectMapper.Map<
				List<AssignedSubject>,
				List<StandardSubjectAssignmentResponseDto>
			>(assignments);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to get list of assignments for StandardId: {StandardId}",
				standardId
			);
			throw new BusinessException(
				"SubjectAssignmentListRetrievalFailed",
				"Failed to retrieve subject assignment list."
			);
		}
	}

	public async Task UpdateTeachersAsync(
		Guid assignmentId,
		Guid primaryId,
		List<Guid> secondaryIds
	)
	{
		try
		{
			var assignment = await assignmentRepository.GetAsync(assignmentId);
			assignment.SetPrimaryTeacher(primaryId);
			assignment.SetSecondaryTeachers(secondaryIds);
			await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to update teachers for assignmentId: {AssignmentId}",
				assignmentId
			);
			throw new BusinessException(
				"SubjectAssignmentTeacherUpdateFailed",
				"Failed to update subject assignment teachers."
			);
		}
	}
}
