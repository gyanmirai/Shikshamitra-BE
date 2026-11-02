using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	private static bool ValidSelectionMembers(StudentIdCardSelectionDto selection) =>
		selection.BoardIds != null && selection.MediumIds != null && selection.StandardIds != null && selection.DivisionIds != null && selection.StudentIds != null &&
		selection.AcademicYearId != Guid.Empty && (selection.StudentIds.Count == 0 || selection.AcademicYearId.HasValue) &&
		new[] { selection.BoardIds, selection.MediumIds, selection.StandardIds, selection.DivisionIds, selection.StudentIds }
			.All(ids => !ids.Contains(Guid.Empty) && ids.Distinct().Count() == ids.Count);

	private async Task<List<StudentIdCardTemplateDto>> VisibleTemplatesAsync(List<StudentIdCardTemplateDto> result)
	{
		foreach (var invalid in result.Where(t => !ValidSelectionMembers(t.Selection!))) Restrict(invalid);
		var selections = result.Where(t => !t.SelectionRestricted && HasSelectionContext(t.Selection!)).GroupBy(t => t.Selection!.AcademicYearId);
		foreach (var group in selections)
		{
			var options = await LazyServiceProvider.LazyGetRequiredService<StudentDirectoryOptionsService>().GetAsync(group.Key);
			foreach (var template in group)
				if (!SelectionFiltersVisible(template.Selection!, options)) Restrict(template);
		}
		var selected = result.Where(t => !t.SelectionRestricted && t.Selection!.StudentIds.Count > 0).ToList();
		if (selected.Count == 0) return result;
		// Authorize the union as scalar data: adding templates or members in one year does not add DB round trips.
		var ids = selected.SelectMany(t => t.Selection!.StudentIds).Distinct().ToArray();
		var years = selected.Select(t => t.Selection!.AcademicYearId!.Value).Distinct().ToArray();
		var scope = LazyServiceProvider.LazyGetRequiredService<StudentRecordsEnrollmentScopeService>();
		var students = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
		var query = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var visible = await AsyncExecuter.ToListAsync(query.Where(s => Enumerable.Contains(ids, s.Id)).Select(s => new { s.Id, s.AcademicYearId }));
		var visibleById = visible.ToDictionary(s => s.Id);
		var enrollmentQuery = await scope.GetQueryableAsync();
		var enrolled = await AsyncExecuter.ToListAsync(enrollmentQuery.Where(e => Enumerable.Contains(ids, e.StudentId) && Enumerable.Contains(years, e.AcademicYearId))
			.Select(e => new { e.StudentId, e.AcademicYearId }).Distinct());
		var membership = enrolled.Select(e => (e.StudentId, e.AcademicYearId)).ToHashSet();
		var roles = await access.GetDatabaseRoleNamesAsync();
		var maskedYears = new HashSet<Guid?>();
		foreach (var year in years.Select(y => (Guid?)y).Concat(visible.Select(s => s.AcademicYearId)).Distinct())
			if ((await policies.ResolveAsync(year)).Policy.IsMasked("Identity", roles)) maskedYears.Add(year);
		foreach (var template in selected)
		{
			var selection = template.Selection!; var year = selection.AcademicYearId!.Value;
			if (maskedYears.Contains(year) || selection.StudentIds.Any(id => !visibleById.TryGetValue(id, out var student) ||
				maskedYears.Contains(student.AcademicYearId) || student.AcademicYearId != year && !membership.Contains((id, year)))) Restrict(template);
		}
		return result;
	}
	private static void Restrict(StudentIdCardTemplateDto template) { template.Selection = null; template.SelectionRestricted = true; }
	private static bool HasSelectionContext(StudentIdCardSelectionDto selection) => selection.AcademicYearId.HasValue ||
		selection.BoardIds.Count > 0 || selection.MediumIds.Count > 0 || selection.StandardIds.Count > 0 || selection.DivisionIds.Count > 0;
	private static bool SelectionFiltersVisible(StudentIdCardSelectionDto selection, StudentDirectoryOptionsDto options) =>
		(!selection.AcademicYearId.HasValue || options.AcademicYears.Any(y => y.Id == selection.AcademicYearId)) &&
		selection.BoardIds.All(id => options.Boards.Any(o => o.Id == id)) && selection.MediumIds.All(id => options.Mediums.Any(o => o.Id == id)) &&
		selection.StandardIds.All(id => options.Standards.Any(o => o.Id == id)) && selection.DivisionIds.All(id => options.Divisions.Any(o => o.Id == id));

	private async Task ValidateAffectedMembersAsync(StudentIdCardSelectionDto? previous, StudentIdCardSelectionDto? next)
	{
		var selections = new[] { previous, next }.Where(s => s != null).Cast<StudentIdCardSelectionDto>().ToList();
		if (selections.Any(s => !ValidSelectionMembers(s))) throw new BusinessException("Students:Records:InvalidIdTemplate");
		foreach (var group in selections.Where(HasSelectionContext).GroupBy(s => s.AcademicYearId))
		{
			var options = await LazyServiceProvider.LazyGetRequiredService<StudentDirectoryOptionsService>().GetAsync(group.Key);
			if (group.Any(s => !SelectionFiltersVisible(s, options))) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}
		var members = selections.SelectMany(s => s.StudentIds.Select(id => (Student: id, Year: s.AcademicYearId!.Value))).Distinct().ToList();
		if (members.Count == 0) return;
		var roles = await access.GetDatabaseRoleNamesAsync();
		var scope = LazyServiceProvider.LazyGetRequiredService<StudentRecordsEnrollmentScopeService>();
		foreach (var member in members)
		{
			// Every affected student retains the authoritative current actor, tenant, scope and Records eligibility checks.
			StudentRecordsAccess allowed;
			try { allowed = await access.EnsureWritableAsync(member.Student); }
			catch (BusinessException ex) when (previous?.StudentIds.Contains(member.Student) == true && ex.Code == UserScopeErrorCodes.AccessDenied)
			{
				// The caller may know only the template ID. Do not disclose its opaque saved members in error data.
				throw new BusinessException(UserScopeErrorCodes.AccessDenied);
			}
			catch (EntityNotFoundException ex) when (previous?.StudentIds.Contains(member.Student) == true && ex.EntityType == typeof(Student))
			{
				throw new EntityNotFoundException(typeof(Student));
			}
			if (allowed.Policy.IsMasked("Identity", roles) || (await policies.ResolveAsync(member.Year)).Policy.IsMasked("Identity", roles))
				throw new BusinessException("Students:Records:DocumentFieldsRestricted");
			if (allowed.Student.AcademicYearId != member.Year && !await AsyncExecuter.AnyAsync((await scope.GetQueryableAsync())
				.Where(e => e.StudentId == member.Student && e.AcademicYearId == member.Year)))
				throw new BusinessException("Students:Records:InvalidIdStudentSelection");
		}
	}
}
