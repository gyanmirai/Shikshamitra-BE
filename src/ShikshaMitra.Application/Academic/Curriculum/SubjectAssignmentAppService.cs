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
	IRepository<Subject, Guid> subjectRepository,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	IGuidGenerator guidGenerator,
	ILogger<SubjectAssignmentAppService> logger
) : ApplicationService, ISubjectAssignmentAppService
{
	public async Task CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	)
	{
		try
		{
			var assignedSubjects = new List<AssignedSubject>();
			foreach (var subject in createSubjectAssignmentDto.Subjects)
			{
				var assignedSubject =
					new AssignedSubject(
						guidGenerator.Create(),
						CurrentTenant.Id,
						createSubjectAssignmentDto.ClassId,
						subject.SubjectId,
						subject.SubjectCode,
						createSubjectAssignmentDto.Status,
						(subject.ElectiveGroupId == Guid.Empty || subject.ElectiveGroupId == null) ? SubjectType.Core : SubjectType.Elective,
						subject.ElectiveGroupId,
						subject.LearningGoal,
						subject.Prerequisites,
						subject.Description
					);

				if (subject.PrimaryTeacherId != null)
				{
					assignedSubject.SetPrimaryTeacher(subject.PrimaryTeacherId.Value);
				}

				if (subject.SecondaryTeacherId != null)
				{
					assignedSubject.SetSecondaryTeachers(subject.SecondaryTeacherId);
				}

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
				}
				assignedSubjects.Add(assignedSubject);
			}

			await assignmentRepository.InsertManyAsync(assignedSubjects, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to create subject assignment");
			throw new UserFriendlyException(
				"Failed to create subject assignment. Please try again later."
			);
		}
	}

	public async Task<SubjectAssignmentPagedResponse> GetListAsync(
		Guid classId,
		int page,
		int pageSize
	)
	{
		var query = await assignmentRepository.GetQueryableAsync();

		query = query.Where(a =>
			a.StandardId == classId
		);

		var assignments = await AsyncExecuter.ToListAsync(query);

		// Group assignments
		var groupedAssignments = new List<SubjectAssignmentGroupDto>();
		
		var groupedByElective = assignments
			.Where(a => a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue)
			.GroupBy(a => a.ElectiveGroupId)
			.ToList();

		var coreSubjects = assignments
			.Where(a => a.SubjectType == SubjectType.Core || (a.SubjectType == SubjectType.Elective && !a.ElectiveGroupId.HasValue))
			.ToList();


        
        // I need to fetch all subjects for these assignments to get Name and Color.
        var subjectIds = assignments.Select(x => x.SubjectId).Distinct().ToList();
        var subjectsDict = (await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id)))
                           .ToDictionary(s => s.Id);
        
        var electiveGroupIds = assignments.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId.Value).Distinct().ToList();
        var electiveGroupsDict = (await electiveGroupRepository.GetListAsync(g => electiveGroupIds.Contains(g.Id)))
                                 .ToDictionary(g => g.Id);

        foreach (var coreAssignment in coreSubjects)
        {
            subjectsDict.TryGetValue(coreAssignment.SubjectId, out var subject);
            
            groupedAssignments.Add(new SubjectAssignmentGroupDto
            {
                SubjectType = coreAssignment.SubjectType,
                Status = coreAssignment.Status,
                Color = subject?.Color, // Use subject color for core
                Subjects = new List<SubjectLiteDto> 
                {
                    new SubjectLiteDto
                    {
                        SubjectType = coreAssignment.SubjectType,
                        SubjectId = coreAssignment.SubjectId,
                        SubjectName = subject?.Name,
                        SubjectCode = coreAssignment.SubjectCode, // Or subject.Code? 
                        Color = subject?.Color
                    }
                }
            });
        }

        foreach (var group in groupedByElective)
        {
            var first = group.First();
            electiveGroupsDict.TryGetValue(first.ElectiveGroupId.Value, out var electiveGroup);

            groupedAssignments.Add(new SubjectAssignmentGroupDto
            {
                SubjectType = SubjectType.Elective,
                Status = first.Status,
                ElectiveGroupId = first.ElectiveGroupId,
                ElectiveGroupName = electiveGroup?.Name,
                Color = electiveGroup?.Color,
                Subjects = group.Select(a => 
                {
                    subjectsDict.TryGetValue(a.SubjectId, out var subj);
                    return new SubjectLiteDto
                    {
                        SubjectType = a.SubjectType,
                        SubjectId = a.SubjectId,
                        SubjectName = subj?.Name,
                        SubjectCode = a.SubjectCode,
                        Color = subj?.Color
                    };
                }).ToList()
            });
        }
        
        // Pagination
        var totalCount = groupedAssignments.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        
        var pagedItems = groupedAssignments
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();

		return new SubjectAssignmentPagedResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedItems
		};
	}

	public async Task<SubjectAssignmentDto> GetAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		
		List<AssignedSubject> groupAssignments;
		if (assignment.ElectiveGroupId.HasValue)
		{
			groupAssignments = await assignmentRepository.GetListAsync(a => 
				a.ElectiveGroupId == assignment.ElectiveGroupId && a.StandardId == assignment.StandardId
			);
		}
		else
		{
			groupAssignments = new List<AssignedSubject> { assignment };
		}

		//return await MapToResponseDtoAsync(groupAssignments);
		return null;
	}

	public async Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto input
	)
	{
		// 1. Identify the group being updated
		var existingEntity = await assignmentRepository.GetAsync(assignmentId);
		List<AssignedSubject> existingGroup;

		if (existingEntity.ElectiveGroupId.HasValue)
		{
			existingGroup = await assignmentRepository.GetListAsync(x => 
				x.ElectiveGroupId == existingEntity.ElectiveGroupId && x.StandardId == existingEntity.StandardId
			);
		}
		else
		{
			existingGroup = new List<AssignedSubject> { existingEntity };
		}

		// 2. Process updates
		// We match existing subjects with input subjects by SubjectId
		var subjectsToDelete = new List<AssignedSubject>();
		var processedSubjectIds = new HashSet<Guid>();

		foreach (var inputSubject in input.Subjects)
		{
			processedSubjectIds.Add(inputSubject.SubjectId);
			var existingSubject = existingGroup.FirstOrDefault(x => x.SubjectId == inputSubject.SubjectId);

			if (existingSubject != null)
			{
				// Update existing
				existingSubject.SubjectCode = inputSubject.SubjectCode ?? existingSubject.SubjectCode;
				existingSubject.LearningGoals = inputSubject.LearningGoal;
				existingSubject.Prerequisites = inputSubject.Prerequisites;
				existingSubject.Description = inputSubject.Description;
				existingSubject.ChangeStatus(input.Status);
				existingSubject.StandardId = input.ClassId; // Allow moving standard?
				
				// Update Teachers
				if (inputSubject.PrimaryTeacherId.HasValue)
				{
					existingSubject.SetPrimaryTeacher(inputSubject.PrimaryTeacherId.Value);
				}
				if (inputSubject.SecondaryTeacherId != null)
				{
					existingSubject.SetSecondaryTeachers(inputSubject.SecondaryTeacherId);
				}

				// Update specific fields like Elective Group ID if shifted? 
				// For now assuming we keep them in the same group or the input defines the new group ID? 
				// The input defines the structure.
				existingSubject.ElectiveGroupId = inputSubject.ElectiveGroupId;
				existingSubject.SubjectType = (inputSubject.ElectiveGroupId == null || inputSubject.ElectiveGroupId == Guid.Empty) 
					? SubjectType.Core : SubjectType.Elective;

				await assignmentRepository.UpdateAsync(existingSubject);
			}
			else
			{
				// Create new in this group
				var newSubject = new AssignedSubject(
					guidGenerator.Create(),
					CurrentTenant.Id,
					input.ClassId,
					inputSubject.SubjectId,
					inputSubject.SubjectCode,
					input.Status,
					(inputSubject.ElectiveGroupId == Guid.Empty || inputSubject.ElectiveGroupId == null) ? SubjectType.Core : SubjectType.Elective,
					inputSubject.ElectiveGroupId,
					inputSubject.LearningGoal,
					inputSubject.Prerequisites,
					inputSubject.Description
				);

				if (inputSubject.PrimaryTeacherId.HasValue)
					newSubject.SetPrimaryTeacher(inputSubject.PrimaryTeacherId.Value);
				
				if (inputSubject.SecondaryTeacherId != null)
					newSubject.SetSecondaryTeachers(inputSubject.SecondaryTeacherId);

				if (inputSubject.Documents != null)
				{
					foreach (var doc in inputSubject.Documents)
					{
						newSubject.AddDocument(guidGenerator.Create(), doc);
					}
				}

				await assignmentRepository.InsertAsync(newSubject);
				existingGroup.Add(newSubject); // Add to local list for return mapping
			}
		}

		// 3. Delete removed subjects
		foreach (var subject in existingGroup)
		{
			if (!processedSubjectIds.Contains(subject.SubjectId))
			{
				await assignmentRepository.DeleteAsync(subject);
			}
		}

		// Re-fetch or use modified list. 
		// Since we modified existingGroup in memory (for updates) and adding new ones, 
		// wait, new ones were added to repo, but we need to include them in response.
		// Let's re-fetch to be safe and accurate, or construct response manually.
		// Since we might have deleted the one with 'assignmentId', we return the updated group.
		// If 'assignmentId' was deleted, what ID do we return? 
		// The controller returns the DTO. The DTO Id is usually the first one.
		
		// If all deleted? 
		if (processedSubjectIds.Count == 0) return null; // Should not happen due to validation

		// Re-fetch the group based on any of the surviving IDs or the new configuration
		// If we switched from Core to Elective or vice versa, logic holds.
		
		// For response, we can just use GetList logic behavior for this group.
		// But to be fast, let's map what we have if we tracked it well.
		// Safest is to Refetch using one of the current IDs.
		var representativeId = existingGroup.FirstOrDefault(x => processedSubjectIds.Contains(x.SubjectId))?.Id 
			?? existingGroup.LastOrDefault()?.Id; // Fallback

		if (representativeId.HasValue)
		{
			return await GetAsync(representativeId.Value);
		}

		return null;
	}

	public async Task<PublishResponseDto> PublishAsync(List<Guid> assignmentIds)
	{
		try
		{
			var assignments = await assignmentRepository.GetListAsync(a =>
				assignmentIds.Contains(a.Id)
			);
			
			// We should probably find the GROUPS for these IDs and publish the whole groups?
			// Assumption: User selects items in UI, if they select an elective, do they select the whole group?
			// Usually yes. But let's be safe and publish what is requested. 
			// If input is list of IDs, we publish those IDs.

			if (assignments.Count == 0)
			{
				throw new BusinessException("SubjectAssignment:NotFound")
					.WithData("AssignmentIds", JsonSerializer.Serialize(assignmentIds));
			}

			foreach (var assignment in assignments)
			{
				assignment.Status = SubjectStatus.Published;
				assignment.PublishedAt = DateTime.UtcNow;
			}
			
			await assignmentRepository.UpdateManyAsync(assignments, autoSave: true);
			
			return new PublishResponseDto
			{
				Status = SubjectStatus.Published,
				PublishedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to publish assignments");
			throw new UserFriendlyException("Failed to publish assignments.");
		}
	}

	public async Task DeleteAsync(Guid assignmentId)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		
		List<AssignedSubject> groupAssignments;
		if (assignment.ElectiveGroupId.HasValue)
		{
			groupAssignments = await assignmentRepository.GetListAsync(a => 
				a.ElectiveGroupId == assignment.ElectiveGroupId && a.StandardId == assignment.StandardId
			);
		}
		else
		{
			groupAssignments = new List<AssignedSubject> { assignment };
		}

		await assignmentRepository.DeleteManyAsync(groupAssignments, autoSave: true);
	}

	public async Task<List<CoreElectiveSubjectResponseDto>> GetCoreElectiveSubjectsAsync(Guid academicMediumId)
	{
		try
		{
			var coreSubjects = await subjectRepository.GetListAsync(s =>
				s.EducationMediumId == academicMediumId
			);
			var electiveGroups = await electiveGroupRepository.GetListAsync(eg =>
				eg.AcademicMediumId == academicMediumId
			);

			var result = new List<CoreElectiveSubjectResponseDto>();
			foreach (var coreSubject in coreSubjects)
			{
				var subjectDto = new CoreElectiveSubjectResponseDto	
				{
					Id = coreSubject.Id,
					Name = coreSubject.Name,
					SubjectType = SubjectType.Core,
					Color = coreSubject.Color,
				};
				result.Add(subjectDto);

			}

			foreach (var electiveGroup in electiveGroups)
			{
				var subjectDto = new CoreElectiveSubjectResponseDto	
				{
					Id = electiveGroup.Id,
					Name = electiveGroup.Name,
					SubjectType = SubjectType.Elective,
					Color = electiveGroup.Color,
					Subjects = electiveGroup.Subjects.Select(es => new CoreElectiveSubjectDetailDto
					{
						Id = es.SubjectId,
						Name = es.Subject.Name,
						Color = es.Subject.Color,
					}).ToList()
				};
				result.Add(subjectDto);
			}
				return result;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to retrieve core and elective subjects");
			throw new UserFriendlyException("Failed to retrieve subjects. Please try again later.");
		}
	}
}
