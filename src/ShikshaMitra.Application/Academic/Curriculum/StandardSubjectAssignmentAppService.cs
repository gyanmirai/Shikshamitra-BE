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

			await assignmentRepository.InsertAsync(assignment, autoSave: true);

			return objectMapper.Map<StandardSubjectAssignment, StandardSubjectAssignmentResponseDto>(assignment);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to assign subject for StandardId: {StandardId}, SubjectId: {SubjectId}", createAssignmentDto.StandardId, createAssignmentDto.SubjectId);
			throw new BusinessException("SubjectAssignmentCreationFailed", "Failed to create subject assignment.");
		}
	}

	public Task ChangeStatusAsync(Guid assignmentId, SubjectStatus status) => throw new NotImplementedException();
	public Task<List<StandardSubjectAssignmentResponseDto>> GetListAsync(Guid standardId) => throw new NotImplementedException();
	public Task UpdateTeachersAsync(Guid assignmentId, Guid primaryId, List<Guid> secondaryIds) => throw new NotImplementedException();
}
