using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

[RemoteService(IsEnabled = false)]
public class StudentElectiveAssignmentAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<StudentElectiveSelection, Guid> selectionRepository,
	IGuidGenerator guidGenerator,
	ILogger<StudentElectiveAssignmentAppService> logger
) : ApplicationService, IStudentElectiveAssignmentAppService
{
	public async Task<List<ElectiveSelectionCountDto>> GetSelectionCountsAsync(ElectiveSelectionCountsRequestDto input)
	{
		var groupIds = input.ElectiveGroupIds.Distinct().ToArray();
		var divisionIds = input.DivisionIds.Distinct().ToArray();
		if (groupIds.Length == 0 || divisionIds.Length == 0) return [];
		var groups = await electiveGroupRepository.GetQueryableAsync();
		var foundGroups = await AsyncExecuter.ToListAsync(groups.Where(group => Enumerable.Contains(groupIds, group.Id))
			.Select(group => group.Id));
		var missing = groupIds.Where(id => !foundGroups.Contains(id)).ToList();
		if (missing.Count > 0) throw new EntityNotFoundException(typeof(ElectiveGroup), missing[0]);
		var students = (await studentRepository.GetQueryableAsync()).Where(student =>
			student.AcademicMediumId == input.AcademicMediumId && student.StandardId == input.ClassId &&
			student.DivisionId.HasValue && Enumerable.Contains(divisionIds, student.DivisionId.Value));
		var selections = (await selectionRepository.GetQueryableAsync()).Where(selection =>
			Enumerable.Contains(groupIds, selection.ElectiveGroupId));
		var query = from student in students
			join selection in selections on student.Id equals selection.StudentId
			group selection by new { selection.ElectiveGroupId, DivisionId = student.DivisionId!.Value, selection.SelectedSubjectId } into grouped
			select new ElectiveSelectionCountDto
			{
				ElectiveGroupId = grouped.Key.ElectiveGroupId, DivisionId = grouped.Key.DivisionId,
				SubjectId = grouped.Key.SelectedSubjectId, Count = grouped.Count()
			};
		return await AsyncExecuter.ToListAsync(query.OrderBy(row => row.ElectiveGroupId)
			.ThenBy(row => row.DivisionId).ThenBy(row => row.SubjectId));
	}

	public async Task<PaginatedStudentElectiveResponseDto> GetStudentElectiveAssignmentsAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid electiveGroupId,
		string? search = null,
		bool includeSelectableSubjects = true
	)
	{
		logger.LogInformation(
			"Getting student elective assignments for medium={MediumId}, class={ClassId}, division={DivisionId}, group={GroupId}",
			academicMediumId, classId, divisionId, electiveGroupId
		);

		// Get students in the specified class and division
		var studentsQuery = await studentRepository.GetQueryableAsync();
		studentsQuery = studentsQuery.Where(s =>
			s.AcademicMediumId == academicMediumId &&
			s.StandardId == classId &&
			s.DivisionId == divisionId
		);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var searchLower = search.ToLower();
			studentsQuery = studentsQuery.Where(s =>
				(s.FirstName != null && s.FirstName.ToLower().Contains(searchLower)) ||
				(s.LastName != null && s.LastName.ToLower().Contains(searchLower)) ||
				(s.RollNo != null && s.RollNo.ToString().Contains(searchLower))
			);
		}

		var students = await AsyncExecuter.ToListAsync(studentsQuery
			.OrderBy(s => s.RollNo == null)
			.ThenBy(s => s.RollNo)
			.ThenBy(s => s.Id)
			.Select(student => new { student.Id, student.FirstName, student.MiddleName, student.LastName, student.RollNo }));

		// Get the elective group with subjects
		var electiveGroup = await electiveGroupRepository.GetAsync(
			electiveGroupId,
			includeDetails: true
		);

		// Get subject details
		var subjectIds = electiveGroup.Subjects.Select(s => s.SubjectId).ToList();
		var subjects = await subjectRepository.GetQueryableAsync();
		var subjectOptions = await AsyncExecuter.ToListAsync(subjects.Where(subject => Enumerable.Contains(subjectIds, subject.Id))
			.Select(s => new ElectiveSubjectOptionDto
			{
				SubjectId = s.Id,
				SubjectName = s.Name,
				SubjectColor = s.Color
			}));
		var subjectLookup = subjectOptions.ToDictionary(s => s.SubjectId);

		// Get existing selections
		var studentIds = students.Select(s => s.Id).ToList();
		var selections = await AsyncExecuter.ToListAsync((await selectionRepository.GetQueryableAsync())
			.Where(selection => Enumerable.Contains(studentIds, selection.StudentId) && selection.ElectiveGroupId == electiveGroupId)
			.Select(selection => new { selection.StudentId, selection.SelectedSubjectId, selection.StudentPreference, selection.IsLocked }));
		var selectionsByStudent = selections.GroupBy(selection => selection.StudentId)
			.ToDictionary(group => group.Key, group => group.First());

		var result = new PaginatedStudentElectiveResponseDto
		{
			TotalRecords = students.Count,
			IsLocked = selections.Any() && selections.All(s => s.IsLocked),
			SubjectOptions = subjectOptions,
			Records = students.Select(student =>
			{
				selectionsByStudent.TryGetValue(student.Id, out var selection);
				ElectiveSubjectOptionDto? selectedSubjectOption = null;
				if (selection != null && subjectLookup.TryGetValue(selection.SelectedSubjectId, out var option))
				{
					selectedSubjectOption = option;
				}

				var studentName = string.Join(
					" ",
					new[] { student.FirstName, student.MiddleName, student.LastName }
						.Where(n => !string.IsNullOrWhiteSpace(n))
				);

				return new StudentElectiveRecordDto
				{
					StudentId = student.Id,
					StudentName = studentName,
					RollNo = student.RollNo?.ToString() ?? "N/A",
					SelectedSubjectId = selection?.SelectedSubjectId,
					SelectedSubjectName = selectedSubjectOption?.SubjectName,
					StudentPreference = selection?.StudentPreference,
					SelectableSubjects = includeSelectableSubjects ? subjectOptions : []
				};
			}).ToList()
		};

		return result;
	}

	public async Task<ApiSuccessResponseDto> SaveStudentElectiveAssignmentsAsync(
		SaveStudentElectiveAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Saving student elective assignments for group {GroupId}",
			input.ElectiveGroupId
		);

			try
			{
				// Get all subjects from the elective group
				var electiveGroup = await electiveGroupRepository.GetAsync(
					input.ElectiveGroupId,
					includeDetails: true
				);

				var subjectIds = electiveGroup.Subjects.Select(s => s.SubjectId).ToHashSet();
				var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
				var subjectLookup = subjects.ToDictionary(s => s.Id);

				// Validate all selected subjects belong to the elective group
				var invalidSubjects = input.Assignments
					.Where(a => a.SelectedSubjectId.HasValue && !subjectIds.Contains(a.SelectedSubjectId.Value))
					.Select(a => a.SelectedSubjectId!.Value)
					.Distinct()
					.ToList();

				if (invalidSubjects.Any())
				{
					throw new BusinessException("ClassManagement:SubjectNotInElectiveGroup")
						.WithData("SubjectIds", string.Join(", ", invalidSubjects))
						.WithData("ElectiveGroupId", input.ElectiveGroupId);
				}

				// Get all existing selections in one query
				var studentIds = input.Assignments.Select(a => a.StudentId).ToList();
				var existingSelections = await selectionRepository.GetListAsync(s =>
					studentIds.Contains(s.StudentId) &&
					s.ElectiveGroupId == input.ElectiveGroupId
				);

				if (existingSelections.Any(s => s.IsLocked))
				{
					throw new UserFriendlyException("Elective assignments are locked and cannot be modified.");
				}

				var selectionsToUpdate = new List<StudentElectiveSelection>();
				var selectionsToInsert = new List<StudentElectiveSelection>();
				var selectionsToDelete = new List<StudentElectiveSelection>();

				foreach (var assignment in input.Assignments)
				{
					var existingSelection = existingSelections.FirstOrDefault(s =>
						s.StudentId == assignment.StudentId
					);

					if (!assignment.SelectedSubjectId.HasValue)
					{
						if (existingSelection != null)
						{
							selectionsToDelete.Add(existingSelection);
						}
						continue;
					}

					// Validate subject exists
					if (!subjectLookup.TryGetValue(assignment.SelectedSubjectId.Value, out var selectedSubject))
					{
						throw new BusinessException("ClassManagement:SubjectNotFound")
							.WithData("SubjectId", assignment.SelectedSubjectId);
					}

					if (existingSelection != null)
					{
						// Update existing selection
						existingSelection.UpdateSelection(selectedSubject.Id);
						selectionsToUpdate.Add(existingSelection);
					}
					else
					{
						// Create new selection
						var newSelection = new StudentElectiveSelection(
							guidGenerator.Create(),
							CurrentTenant.Id,
							assignment.StudentId,
							input.ElectiveGroupId,
							selectedSubject.Id,
							input.AcademicMediumId
						);
						selectionsToInsert.Add(newSelection);
					}
				}

				// Batch operations
				if (selectionsToDelete.Any())
				{
					await selectionRepository.DeleteManyAsync(selectionsToDelete);
				}
				if (selectionsToUpdate.Any())
				{
					await selectionRepository.UpdateManyAsync(selectionsToUpdate);
				}
				if (selectionsToInsert.Any())
				{
					await selectionRepository.InsertManyAsync(selectionsToInsert);
				}

				return new ApiSuccessResponseDto
				{
					Success = true,
					Message = $"Successfully saved {input.Assignments.Count} student elective assignment changes."
				};
			}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to save student elective assignments");
			throw new UserFriendlyException(
				"Failed to save student elective assignments. Please try again later."
			);
		}
	}

	public async Task<ApiSuccessResponseDto> LockStudentElectiveAssignmentsAsync(
		LockStudentElectiveAssignmentsDto input
	)
	{
		logger.LogInformation(
			"Locking student elective assignments for medium={MediumId}, class={ClassId}, division={DivisionId}, group={GroupId}",
			input.AcademicMediumId, input.ClassId, input.DivisionId, input.ElectiveGroupId
		);

		try
		{
			// Get students in the specified class and division
			var studentsQuery = await studentRepository.GetQueryableAsync();
			var students = await AsyncExecuter.ToListAsync(
				studentsQuery.Where(s =>
					s.AcademicMediumId == input.AcademicMediumId &&
					s.StandardId == input.ClassId &&
					s.DivisionId == input.DivisionId
				)
			);

			if (!students.Any())
			{
				throw new UserFriendlyException("No students found in the specified class and division.");
			}

			var studentIds = students.Select(s => s.Id).ToList();

			// Get existing selections
			var selections = await selectionRepository.GetListAsync(s =>
				studentIds.Contains(s.StudentId) &&
				s.ElectiveGroupId == input.ElectiveGroupId
			);

			// Verify that every student has a saved selection
			var selectionStudentIds = selections.Select(s => s.StudentId).ToHashSet();
			var studentsWithoutSelection = students.Where(s => !selectionStudentIds.Contains(s.Id)).ToList();

			if (studentsWithoutSelection.Any())
			{
				throw new UserFriendlyException("All students must have an elective subject assigned and saved before locking.");
			}

			// If already locked, just return success
			if (selections.All(s => s.IsLocked))
			{
				return new ApiSuccessResponseDto
				{
					Success = true,
					Message = "Student elective assignments are already locked."
				};
			}

			// Lock all selections
			foreach (var selection in selections)
			{
				selection.LockSelection();
			}

			await selectionRepository.UpdateManyAsync(selections);

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = "Student elective assignments locked successfully."
			};
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to lock student elective assignments");
			throw new UserFriendlyException(
				"Failed to lock student elective assignments. Please try again later."
			);
		}
	}
}
