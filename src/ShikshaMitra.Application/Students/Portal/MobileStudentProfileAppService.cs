using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Portal;

[Authorize]
[Volo.Abp.RemoteService(false)]
public class MobileStudentProfileAppService(StudentPortalAccessService access, MobileAcademicContextAppService context,
	StudentRecordRegistrationReader forms, StudentRecordCustomDetailsService values, StudentRecordFormProjection projection,
	PortalStudentProfileSectionReader sections, PortalStudentProfileOptions options,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, IRepository<EducationMedium, Guid> mediums)
	: ApplicationService, IMobileStudentProfileAppService
{
	public async Task<MobileStudentProfileDto> GetAsync(Guid studentId, Guid? academicYearId = null)
	{
		var student = await access.ResolveStudentAsync(studentId);
		var years = await context.GetYearsForStudentAsync(student);
		var yearId = academicYearId ?? student.AcademicYearId;
		var year = years.SingleOrDefault(y => y.Id == yearId);
		if (year == null) throw MobileAcademicContextAppService.Invalid("Select an enrolled academic year.");
		var placements = await enrollments.GetListAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId == studentId && e.AcademicYearId == year.Id);
		var eligible = placements.Where(e => e.IsActive || year.EndDate.Date < MobileAcademicContextAppService.SchoolToday).ToList();
		if (eligible.Count > 1) throw MobileAcademicContextAppService.Invalid("The student enrollment is ambiguous.");
		var placement = eligible.SingleOrDefault();
		if (placements.Count > 0 && placement == null) throw MobileAcademicContextAppService.Invalid("No active enrollment is available.");
		var legacy = placements.Count == 0 && student.AcademicYearId == year.Id;
		var standardId = placement?.StandardId ?? (legacy ? student.StandardId : null);
		var divisionId = placement?.DivisionId ?? (legacy ? student.DivisionId : null);
		var standard = await AsyncExecuter.FirstOrDefaultAsync((await standards.GetQueryableAsync())
			.Where(s => s.Id == standardId && s.TenantId == CurrentTenant.Id && s.EducationMediumStage.TenantId == CurrentTenant.Id &&
				s.EducationMediumStage.EducationMedium.TenantId == CurrentTenant.Id && s.EducationMediumStage.EducationMedium.EducationBoard.TenantId == CurrentTenant.Id &&
				s.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == year.Id)
			.Select(s => new { s.Name, MediumName = s.EducationMediumStage.EducationMedium.Name }));
		var division = divisionId.HasValue ? await divisions.FindAsync(d => d.Id == divisionId && d.StandardId == standardId && d.TenantId == CurrentTenant.Id) : null;
		if (standardId.HasValue && standard == null || divisionId.HasValue && division == null)
			throw MobileAcademicContextAppService.Invalid("The class assignment does not match the academic year.");
		var medium = legacy ? await mediums.FindAsync(m => m.Id == student.AcademicMediumId && m.TenantId == CurrentTenant.Id) : null;
		var result = new MobileStudentProfileDto
		{
			StudentId = studentId, AcademicYear = new(year.Id, year.Name),
			Summary = new() { Name = MobileAcademicContextAppService.Name(student), GrNumber = student.GrNumber ?? student.GrNo?.ToString(),
				StandardName = standard?.Name, DivisionName = division?.Name, MediumName = standard?.MediumName ?? medium?.Name }
		};
		var sources = await forms.GetForPortalAsync(studentId);
		await values.PopulateCurrentValuesAsync(studentId, sources);
		await projection.PopulateForPortalAsync(studentId, sources, section => sections.ReadAsync(studentId, section));
		foreach (var source in sources)
		{
			await options.PopulateAsync(source);
			foreach (var field in source.Definition!.Sections.SelectMany(s => s.Fields)) field.DefaultValue = null;
			var codes = source.Definition.Sections.SelectMany(s => s.Fields).Select(f => f.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
			result.FormSources.Add(new()
			{
				SubmissionId = source.SubmissionId, FormName = source.FormName, FormVersion = source.FormVersion,
				Definition = source.Definition, FormMaskedFieldCodes = source.FormMaskedFieldCodes,
				CurrentValues = source.CurrentValues.Where(v => codes.Contains(v.Key)).ToDictionary(v => v.Key,
					v => source.FormMaskedFieldCodes.Contains(v.Key) ? JsonSerializer.SerializeToElement<object?>(null) : v.Value),
				OptionsByField = source.OptionsByField, OptionsByFieldPath = source.OptionsByFieldPath
			});
		}
		result.Photo = await LazyServiceProvider.LazyGetRequiredService<IMobileStudentPhotoAppService>().GetInfoAsync(studentId);
		return result;
	}
}
