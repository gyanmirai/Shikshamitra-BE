using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class StandardSubjectAssignmentAppService(
	IRepository<StandardSubjectAssignment, Guid> assignmentRepository,
	StandardSubjectAssignmentManager manager,
	IObjectMapper objectMapper,
	ILogger<StandardSubjectAssignmentAppService> logger
) : ApplicationService, IStandardSubjectAssignmentAppService
{
	public async Task<StandardSubjectAssignmentResponseDto> CreateAssignmentAsync(CreateStandardSubjectAssignmentDto createAssignmentDto, Guid academicMediumId)
	{
		try
		{
			var assignment = manager.AssignSubject(
				academicMediumId,
				createAssignmentDto.SubjectId,
				createAssignmentDto.StandardId,
				createAssignmentDto.SubjectCode,
				createAssignmentDto.LearningGoals,
				createAssignmentDto.Prerequisites,
				createAssignmentDto.Description,
				createAssignmentDto.Status
			);

			if (createAssignmentDto.PrimaryTeacherId.HasValue)
			{
				assignment.SetPrimaryTeacher(createAssignmentDto.PrimaryTeacherId.Value);
			}

			if (createAssignmentDto.SecondaryTeacherIds != null && createAssignmentDto.SecondaryTeacherIds.Count > 0)
			{
				assignment.SetSecondaryTeachers(createAssignmentDto.SecondaryTeacherIds);
			}

			await assignmentRepository.InsertAsync(assignment, autoSave: true);

			return objectMapper.Map<StandardSubjectAssignment, StandardSubjectAssignmentResponseDto>(assignment);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to assign subject for StandardId: {StandardId}, SubjectId: {SubjectId}", createAssignmentDto.StandardId, createAssignmentDto.SubjectId);
			throw new BusinessException("SubjectAssignmentCreationFailed", "Failed to create subject assignment.");
		}
	}

	public async Task ChangeStatusAsync(Guid assignmentId, SubjectStatus status)
	{
		try
		{
			var assignment = await assignmentRepository.FindAsync(assignmentId) ??
				throw new BusinessException("SubjectAssignmentNotFound", "Subject assignment not found.");

			assignment.ChangeStatus(status);

			await assignmentRepository.UpdateAsync(assignment, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to change status for assignmentId: {AssignmentId}", assignmentId);
			throw new BusinessException("SubjectAssignmentStatusChangeFailed", "Failed to change subject assignment status.");
		}
	}

	public async Task<List<StandardSubjectAssignmentResponseDto>> GetListAsync(Guid standardId)
	{
		try
		{
			var assignments = await assignmentRepository.GetListAsync(a => a.StandardId == standardId);
			return objectMapper.Map<List<StandardSubjectAssignment>, List<StandardSubjectAssignmentResponseDto>>(assignments);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to get list of assignments for StandardId: {StandardId}", standardId);
			throw new BusinessException("SubjectAssignmentListRetrievalFailed", "Failed to retrieve subject assignment list.");
		}
	}

	public async Task UpdateTeachersAsync(Guid assignmentId, Guid primaryId, List<Guid> secondaryIds)
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
			logger.LogError(ex, "Failed to update teachers for assignmentId: {AssignmentId}", assignmentId);
			throw new BusinessException("SubjectAssignmentTeacherUpdateFailed", "Failed to update subject assignment teachers.");
		}
	}
}
