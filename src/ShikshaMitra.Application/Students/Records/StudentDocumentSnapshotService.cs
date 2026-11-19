using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

public record StudentDocumentSnapshot(Guid StudentId, Guid? AcademicYearId, StudentRecordsPolicy Policy,
	Dictionary<string, string?> Fields, Guid? BoardId = null, Guid? MediumId = null, Guid? OrganizationId = null);

[RemoteService(false)]
public class StudentDocumentSnapshotService(StudentRecordsAccessService access, IStudentRepository students) : ApplicationService, ITransientDependency
{
	public async Task<StudentDocumentSnapshot> CaptureAsync(Guid studentId)
	{
		var permission = await access.EnsureWritableAsync(studentId);
		if (permission.Policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync())) throw new BusinessException("Students:Records:DocumentFieldsRestricted");
		var query = await students.GetQueryableWithoutAutoIncludesAsync();
		var row = await AsyncExecuter.FirstAsync(query.Where(s => s.Id == studentId && s.TenantId == CurrentTenant.Id).Select(s => new
		{
			s.FirstName, s.MiddleName, s.LastName, s.GrNumber, s.GrNo, s.AcademicYearId, s.PhotoBlobName, s.AcademicBoardId, s.AcademicMediumId, s.AcademicMediumStageId,
			AcademicYear = s.AcademicYear != null ? s.AcademicYear.Name : null,
			Class = s.Standard != null ? s.Standard.Name : null, Division = s.Division != null ? s.Division.Name : null,
			Birth = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfBirth : null
		}));
		return new(studentId, row.AcademicYearId, permission.Policy, new()
		{
			["StudentName"] = string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			["GRNumber"] = row.GrNumber ?? row.GrNo?.ToString(), ["AcademicYear"] = row.AcademicYear, ["Class"] = row.Class,
			["Division"] = row.Division, ["DateOfBirth"] = row.Birth?.ToString("yyyy-MM-dd"), ["_PhotoBlobName"] = row.PhotoBlobName
		}, row.AcademicBoardId, row.AcademicMediumId == Guid.Empty ? null : row.AcademicMediumId, row.AcademicMediumStageId);
	}
}
