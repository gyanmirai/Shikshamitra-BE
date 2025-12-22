using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
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
	IRepository<AssignedSubject, Guid> assignmentRepository,
	IGuidGenerator guidGenerator,
	ILogger<SubjectAssignmentAppService> logger
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task<SubjectAssignmentDto> CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentGroupDto input
	)
	{
		try
		{
			var assignedSubjects = new List<AssignedSubject>();
			foreach ( var subject in input.Subjects ) {
				var assignedSubject = 
					new AssignedSubject(
						guidGenerator.Create(),
						CurrentTenant.Id,
						input.ClassId,
						subject.AssignedSubjectId,
						subject.SubjectCode,
						input.Status,
						(subject.ElectiveGroupId == Guid.Empty || subject.ElectiveGroupId == null) ? SubjectType.Core : SubjectType.Elective,
						subject.ElectiveGroupId,
						subject.LearningGoal,
						subject.Prerequisites,
						subject.Description
					);

				assignedSubject.SetPrimaryTeacher(subject.PrimaryTeacher);
				assignedSubject.SetSecondaryTeachers(subject.SecondaryTeachers);

				// Set documents
				if (subject.Documents != null && subject.Documents.Any())
				{
					foreach (var document in subject.Documents)
					{
						assignedSubject.AddDocument(
								guidGenerator.Create(),
								document
						);
					}
					assignedSubjects.Add(assignedSubject);
				}
			}

			await assignmentRepository.InsertManyAsync(assignedSubjects, autoSave: true);
			return await MapToResponseDtoAsync(assignedSubjects);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new UserFriendlyException(
				"Failed to create subject assignment. Please try again later."
			);
		}
	}

	public async Task<List<SubjectAssignmentDto>> GetListAsync(
		Guid academicMediumId,
		Guid classId
	)
	{
		var query = await assignmentRepository.GetQueryableAsync();

		query = query.Where(a =>
			a.StandardId == classId
		);

		// Use ABP's AsyncExecuter for asynchronous query execution
		var assignments = await AsyncExecuter.ToListAsync(query);

		var result = new List<SubjectAssignmentDto>();

		// group assignments if it has electiveGroupId and make a one response subject, for others make individual response
		var groupedAssignments = assignments
			.GroupBy(a => a.ElectiveGroupId.HasValue ? a.ElectiveGroupId : a.Id)
			.ToList();

		foreach (var group in groupedAssignments)
		{
			var subjectAssignmentDto = await MapToResponseDtoAsync(group.ToList());
		}

		return result;
	}

	public async Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentGroupDto input
	) => throw new NotImplementedException();

	public async Task<PublishResponseDto> PublishAsync(List<Guid> assignmentId)
	{
		try
		{
			var assignments = await assignmentRepository.GetListAsync(a =>
				assignmentId.Contains(a.Id)
			);
			if (assignments.Count == 0)
			{
				throw new BusinessException("SubjectAssignment:NotFound")
					.WithData("AssignmentIds", JsonSerializer.Serialize(assignmentId));
			}
			foreach (var assignment in assignments)
			{
				assignment.Status = SubjectStatus.Published;
				assignment.PublishedAt = DateTime.UtcNow;
			}
			await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);
			logger.LogInformation(
				"Successfully published {Count} assignments",
				assignments.Count
			);
			return new PublishResponseDto
			{
				Status = SubjectStatus.Published,
				PublishedAt = DateTime.UtcNow
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to publish subject assignment group {AssignmentId}",
				JsonSerializer.Serialize(assignmentId)
			);
			throw new UserFriendlyException(
				"Failed to publish subject assignment. Please try again later."
			);
		}
	}

	public async Task DeleteAsync(Guid assignmentId)
	{
		var assignments = await assignmentRepository.GetListAsync(a =>
			a.Id == assignmentId
		);
		if (assignments.Count == 0)
		{
			throw new BusinessException("SubjectAssignment:NotFound")
				.WithData("AssignmentId", assignmentId.ToString());
		}
		await assignmentRepository.DeleteManyAsync(assignments, autoSave: true);
	}

	private async Task<SubjectAssignmentDto> MapToResponseDtoAsync(
		List<AssignedSubject> groupAssignments
	)
	{
		var subjectAssignmentDto = new SubjectAssignmentDto
		{
			Id = groupAssignments.First().Id,
			Status = groupAssignments.First().Status,
			SubjectType = groupAssignments.First().SubjectType,
			Subjects = new List<SubjectAssignmentDetailResponseDto>()
		};

		foreach (var subject in groupAssignments)
		{
			var subjectDto = new SubjectAssignmentDetailResponseDto
			{
				AssignedId = subject.Id,
				SubjectCode = subject.SubjectCode,
				Subject = new SubjectDto { 
					Name = subject.Subject.Name,
					Color = subject.Subject.Color,
				},
				ElectiveGroupId = subject.ElectiveGroupId,
				LearningGoal = subject.LearningGoals,
				Prerequisites = subject.Prerequisites,
				Description = subject.Description,
				PrimaryTeacher = subject.Teachers
					.Where(t => t.TeacherType == SubjectTeacherType.Primary)
					.Select(t => t.TeacherId)
					.FirstOrDefault(),
				SecondaryTeachers = subject.Teachers
					.Where(t => t.TeacherType == SubjectTeacherType.Secondary)
					.Select(t => t.TeacherId)
					.ToList(),
				Documents = subject.Documents.Select(d => d.BlobName).ToList()
			};
			subjectAssignmentDto.Subjects.Add(subjectDto);
		}

		return subjectAssignmentDto;
	}

	public Task<SubjectAssignmentDto> GetAsync(Guid assignmentId) => throw new NotImplementedException();
}
