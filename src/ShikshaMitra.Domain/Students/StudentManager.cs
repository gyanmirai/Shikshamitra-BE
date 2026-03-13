using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;

public class StudentManager(IRepository<Student, Guid> studentRepository) : DomainService
{
public async Task<List<Student>> AssignRollNumbersAsync(Guid divisionId, List<(Guid StudentId, int? RollNo)> studentRollNumbers)
	{
		// Fetch all students related to the input list to verify existence
		var studentIds = studentRollNumbers.Select(x => x.StudentId).ToList();
		var students = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));

		if (students.Count != studentRollNumbers.Count)
		{
			throw new UserFriendlyException("Some students in the list were not found.");
		}

		// Check for uniqueness of roll numbers in the division
		// We only care about non-null roll numbers
		var rollNumbersToCheck = studentRollNumbers.Where(x => x.RollNo.HasValue).Select(x => x.RollNo!.Value).ToList();

		if (rollNumbersToCheck.Any())
		{
			var existingStudentsWithRollNo = await studentRepository.GetListAsync(s => s.DivisionId == divisionId && s.RollNo != null);

			foreach (var (studentId, newRollNo) in studentRollNumbers)
			{
				var student = students.Single(s => s.Id == studentId);

				if (student.DivisionId != divisionId)
				{
					throw new UserFriendlyException($"Student {student.FirstName} does not belong to the specified division.");
				}

				if (newRollNo.HasValue)
				{
					// Check if any OTHER student in the division already has this roll number
					var duplicate = existingStudentsWithRollNo.FirstOrDefault(s => s.RollNo == newRollNo && s.Id != studentId);
					if (duplicate != null)
					{
						throw new UserFriendlyException($"Roll no: {newRollNo} already exists for student: {duplicate.FirstName} {duplicate.LastName}.");
					}

					// Also check regarding the Input List itself for duplicates
					if (studentRollNumbers.Count(x => x.RollNo == newRollNo) > 1)
					{
						throw new UserFriendlyException($"Roll no: {newRollNo} is assigned to multiple students in the request.");
					}
				}

				// Assign
				student.AssignRollNo(newRollNo);
			}
		}
		else
		{
			// All nulls? Just assign.
			foreach (var (studentId, newRollNo) in studentRollNumbers)
			{
				var student = students.Single(s => s.Id == studentId);
				student.AssignRollNo(newRollNo);
			}
		}

		return students;
	}
}
