using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordProfileAppService(IStudentRepository students, StudentRecordsAccessService access,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, StudentRecordsEnrollmentScopeService enrollmentScope)
	: ApplicationService, IStudentRecordProfileAppService
{
	public async Task<StudentRecordProfileDto> GetAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		var recordsAccess = await access.EnsureReadableAsync(studentId);
		var query = await students.GetQueryableWithoutAutoIncludesAsync();
		var result = await AsyncExecuter.FirstOrDefaultAsync(query.Where(student => student.Id == studentId && student.TenantId == CurrentTenant.Id).Select(student => new StudentRecordProfileDto
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
		result.RecordsAccess = recordsAccess.Access;
		result.EntrySource = recordsAccess.EntrySource;
		result.PlacementEditable = recordsAccess.PlacementEditable;
		if (recordsAccess.Access == "Full")
		{
			var enrollmentQuery = await enrollmentScope.GetQueryableAsync();
			var yearQuery = await years.GetQueryableAsync();
			var standardQuery = await standards.GetQueryableAsync();
			var divisionQuery = await divisions.GetQueryableAsync();
			result.EnrollmentHistory = await AsyncExecuter.ToListAsync((from e in enrollmentQuery
				where e.StudentId == studentId && e.TenantId == CurrentTenant.Id
				join y in yearQuery.Where(y => y.TenantId == CurrentTenant.Id) on e.AcademicYearId equals y.Id
				join s in standardQuery.Where(s => s.TenantId == CurrentTenant.Id) on e.StandardId equals (Guid?)s.Id into classRows
				from s in classRows.DefaultIfEmpty()
				join d in divisionQuery.Where(d => d.TenantId == CurrentTenant.Id) on e.DivisionId equals (Guid?)d.Id into divisionRows
				from d in divisionRows.DefaultIfEmpty()
				orderby e.CreationTime descending, e.Id
				select new StudentEnrollmentHistoryDto { Id = e.Id, AcademicYearId = e.AcademicYearId, AcademicYearName = y.Name,
					StandardId = e.StandardId, StandardName = s != null ? s.Name : null, DivisionId = e.DivisionId,
					DivisionName = d != null ? d.Name : null, RollNumber = e.RollNumber, IsActive = e.IsActive, OnboardingCaseId = e.OnboardingCaseId }));
		}
		else { result.DateOfBirth = null; result.Gender = null; }
		if (recordsAccess.Policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync()))
		{
			result.FirstName = result.MiddleName = result.LastName = result.GrNumber = null;
			result.GrNo = null; result.DateOfBirth = null; result.Gender = null; result.MaskedFields.Add("Identity");
		}
		return result;
	}
}
