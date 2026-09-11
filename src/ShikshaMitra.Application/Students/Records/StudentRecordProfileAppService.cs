using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordProfileAppService(IStudentRepository students, StudentScopeAccessService scope)
	: ApplicationService, IStudentRecordProfileAppService
{
	public async Task<StudentRecordProfileDto> GetAsync(Guid studentId)
	{
		await scope.GetStudentAsync(studentId, "view student record profile");
		var query = await students.GetQueryableWithoutAutoIncludesAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(student => student.Id == studentId).Select(student => new StudentRecordProfileDto
		{
			Id = student.Id,
			StudentId = student.Id,
			FirstName = student.FirstName,
			MiddleName = student.MiddleName,
			LastName = student.LastName,
			GrNo = student.GrNo,
			GrNumber = student.GrNumber,
			RollNo = student.RollNo,
			StandardId = student.StandardId,
			StandardName = student.Standard != null ? student.Standard.Name : null,
			DivisionId = student.DivisionId,
			DivisionName = student.Division != null ? student.Division.Name : null,
			AcademicMediumId = student.AcademicMediumId,
			AcademicMediumName = student.AcademicMedium != null ? student.AcademicMedium.Name : null,
			Status = student.Status,
			DateOfBirth = student.AdditionalDetail != null ? student.AdditionalDetail.DateOfBirth : null,
			Gender = student.AdditionalDetail != null ? student.AdditionalDetail.Gender : null
		})) ?? throw new EntityNotFoundException(typeof(Student), studentId);
	}
}
