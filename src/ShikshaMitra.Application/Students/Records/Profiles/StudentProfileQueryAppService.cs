using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

namespace ShikshaMitra.Students.Records.Profiles;

public class StudentProfileQueryAppService(
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService)
	: StudentProfileBaseAppService(studentRepository, studentScopeAccessService), IStudentProfileQueryAppService
{
	public async Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(
		string searchTerm,
		int maxResultCount = 20)
	{
		var normalized = (searchTerm ?? string.Empty).Trim().ToLowerInvariant();
		if (normalized.Length < 2) return [];
		var resultLimit = Math.Clamp(maxResultCount, 1, 50);
		var query = (await studentRepository.GetQueryableAsync()).Where(student =>
			student.TenantId == CurrentTenant.Id &&
			student.Status != StudentLifeCycleStatusEnum.Left &&
			((student.FirstName != null && student.FirstName.ToLower().Contains(normalized)) ||
			 (student.MiddleName != null && student.MiddleName.ToLower().Contains(normalized)) ||
			 (student.LastName != null && student.LastName.ToLower().Contains(normalized))));
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		var rows = await AsyncExecuter.ToListAsync(query
			.OrderBy(student => student.FirstName)
			.ThenBy(student => student.MiddleName)
			.ThenBy(student => student.LastName)
			.ThenBy(student => student.Id)
			.Take(resultLimit)
			.Select(student => new
			{
				student.Id, student.FirstName, student.MiddleName, student.LastName, student.GrNo, student.Status,
				FormNo = student.AdditionalDetail != null ? student.AdditionalDetail.RegistrationFormNo : null,
				AcademicBoard = student.AcademicBoard != null ? student.AcademicBoard.Type : (EducationBoardType?)null,
				EducationMedium = student.AcademicMedium != null ? student.AcademicMedium.Name : null,
				Standard = student.Standard != null ? student.Standard.Name : null,
				Division = student.Division != null ? student.Division.Name : null
			}));

		return rows.Select(row =>
		{
			var name = string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part)));
			var classText = string.Join(" - ", new[] { row.Standard, row.Division }
				.Where(part => !string.IsNullOrWhiteSpace(part)));
			var result = new StudentSearchResultDto
			{
				Id = row.Id, StudentName = name, FormNo = row.FormNo, GrNo = row.GrNo,
				AcademicBoard = row.AcademicBoard?.ToString(), EducationMedium = row.EducationMedium,
				Standard = row.Standard, Division = row.Division, Status = row.Status
			};
			var board = string.IsNullOrWhiteSpace(result.AcademicBoard) ? string.Empty : $" [{result.AcademicBoard}]";
			var classSuffix = string.IsNullOrWhiteSpace(classText) ? string.Empty : $" ({classText})";
			result.Student = $"{name}{board}{classSuffix}".Trim();
			return result;
		}).ToList();
	}
}
