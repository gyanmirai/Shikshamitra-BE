using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class SubstitutionAppService
{
	private async Task<IQueryable<SubstitutionRequest>> BuildHistoryQueryAsync(GetSubstitutionsInput input,
		IReadOnlySet<Guid>? substituteTeacherReferenceIds, bool applyUserScope)
	{
		var query = await substitutionRepository.GetQueryableAsync();
		if (substituteTeacherReferenceIds is { Count: > 0 })
		{
			var ids = substituteTeacherReferenceIds.ToArray();
			query = query.Where(row => row.SubstituteTeacherId.HasValue && Enumerable.Contains(ids, row.SubstituteTeacherId.Value));
		}
		if (applyUserScope)
			query = ApplyHistoryScope(query, await userScopeAccessService.GetCurrentUserScopeAsync());

		if (input.AcademicYearId.HasValue)
		{
			var years = await academicYearRepository.GetQueryableAsync();
			var year = await AsyncExecuter.FirstOrDefaultAsync(years.Where(item => item.Id == input.AcademicYearId.Value)
				.Select(item => new { item.StartDate, item.EndDate }));
			if (year == null) query = query.Where(row => row.AcademicYearId == input.AcademicYearId);
			else
			{
				var firstDate = year.StartDate.Date;
				var lastDate = year.EndDate.Date;
				query = query.Where(row => row.AcademicYearId == input.AcademicYearId ||
					(!row.AcademicYearId.HasValue && row.Date.Date >= firstDate && row.Date.Date <= lastDate));
			}
		}
		if (input.AcademicMediumId.HasValue && input.AcademicMediumId.Value != Guid.Empty)
		{
			var subjects = (await subjectRepository.GetQueryableAsync())
				.Where(subject => subject.EducationMediumId == input.AcademicMediumId.Value).Select(subject => subject.Id);
			query = query.Where(row => row.SubjectId.HasValue && subjects.Contains(row.SubjectId.Value));
		}
		if (applyUserScope && !IsAdminUser() && !await HasClassTeacherAccessAsync())
		{
			if (!CurrentUser.Id.HasValue) query = query.Where(_ => false);
			else
			{
				var ids = (await GetVisibleTeacherReferenceIdsAsync(CurrentUser.Id.Value)).ToArray();
				query = query.Where(row => (row.OriginalTeacherId.HasValue && Enumerable.Contains(ids, row.OriginalTeacherId.Value)) ||
					(row.SubstituteTeacherId.HasValue && Enumerable.Contains(ids, row.SubstituteTeacherId.Value)));
			}
		}
		if (input.Date.HasValue)
		{
			var date = NormalizeDate(input.Date.Value);
			query = query.Where(row => row.Date == date);
		}
		if (input.FromDate.HasValue)
		{
			var date = NormalizeDate(input.FromDate.Value);
			query = query.Where(row => row.Date >= date);
		}
		if (input.ToDate.HasValue)
		{
			var date = NormalizeDate(input.ToDate.Value);
			query = query.Where(row => row.Date <= date);
		}
		if (input.StandardId.HasValue) query = query.Where(row => row.StandardId == input.StandardId);
		if (input.DivisionId.HasValue) query = query.Where(row => row.DivisionId == input.DivisionId);
		if (input.SubjectId.HasValue) query = query.Where(row => row.SubjectId == input.SubjectId);
		if (input.OriginalTeacherId.HasValue) query = query.Where(row => row.OriginalTeacherId == input.OriginalTeacherId);
		if (input.SubstituteTeacherId.HasValue) query = query.Where(row => row.SubstituteTeacherId == input.SubstituteTeacherId);
		if (input.RoomId.HasValue) query = query.Where(row => row.OriginalRoomId == input.RoomId || row.AssignedRoomId == input.RoomId);
		if (input.Status.HasValue) query = query.Where(row => row.Status == input.Status);
		if (!string.IsNullOrWhiteSpace(input.Reason)) query = query.Where(row => row.Reason == input.Reason);
		if (input.Priority.HasValue) query = query.Where(row => row.Priority == input.Priority);
		if (input.AcknowledgementStatus.HasValue) query = query.Where(row => row.AcknowledgementStatus == input.AcknowledgementStatus);
		if (input.Source.HasValue) query = query.Where(row => row.Source == input.Source);
		if (input.ConflictStatus.HasValue) query = query.Where(row => row.ConflictStatus == input.ConflictStatus);
		if (!string.IsNullOrWhiteSpace(input.ClassName)) query = query.Where(row => row.ClassName == input.ClassName);
		if (!string.IsNullOrWhiteSpace(input.DivisionName)) query = query.Where(row => row.DivisionName == input.DivisionName);
		if (!string.IsNullOrWhiteSpace(input.SubjectName)) query = query.Where(row => row.SubjectName == input.SubjectName);
		if (!string.IsNullOrWhiteSpace(input.TeacherName))
		{
			var names = await AsyncExecuter.ToListAsync(query.Select(row => row.OriginalTeacherName)
				.Union(query.Where(row => row.SubstituteTeacherName != null).Select(row => row.SubstituteTeacherName!)));
			var matchingNames = names.Where(name => string.Equals(name, input.TeacherName, StringComparison.OrdinalIgnoreCase)).ToArray();
			query = query.Where(row => Enumerable.Contains(matchingNames, row.OriginalTeacherName) ||
				(row.SubstituteTeacherName != null && Enumerable.Contains(matchingNames, row.SubstituteTeacherName)));
		}
		return query;
	}

	private static IQueryable<SubstitutionRequest> ApplyHistoryScope(IQueryable<SubstitutionRequest> query, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		var years = scope.AcademicYearIds.ToArray();
		var boardWideYears = scope.WildcardBoardAcademicYearIds.ToArray();
		var standards = scope.StandardIds.ToArray();
		var divisions = scope.DivisionIds.ToArray();
		var wildcardDivisionStandards = scope.WildcardDivisionStandardIds.ToArray();
		return query.Where(row => (!row.AcademicYearId.HasValue || Enumerable.Contains(years, row.AcademicYearId.Value)) &&
			((row.AcademicYearId.HasValue && Enumerable.Contains(boardWideYears, row.AcademicYearId.Value)) ||
			(row.StandardId.HasValue && Enumerable.Contains(standards, row.StandardId.Value)) ||
			(row.DivisionId.HasValue && (Enumerable.Contains(divisions, row.DivisionId.Value) ||
				(scope.HasWildcardDivision && row.StandardId.HasValue && Enumerable.Contains(wildcardDivisionStandards, row.StandardId.Value))))));
	}
}
