using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

internal static class TeacherAvailabilityCoverage
{
	internal static void Apply(
		TeacherAvailabilityDto availability,
		IReadOnlyCollection<AffectedLectureDto> affectedLectures)
	{
		availability.AffectedLectureCount = affectedLectures.Count;
		availability.CoveredLectureCount = affectedLectures.Count(x => x.ExistingSubstitutionId.HasValue);
		availability.UncoveredLectureCount = affectedLectures.Count - availability.CoveredLectureCount;
		availability.CanCreateSubstitution = affectedLectures.Any(x => x.CanCreateSubstitution);
		availability.ActiveSubstitutionStatuses = affectedLectures
			.Where(x => x.ExistingSubstitutionId.HasValue && !string.IsNullOrWhiteSpace(x.ExistingSubstitutionStatus))
			.Select(x => x.ExistingSubstitutionStatus!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		availability.ActiveSubstitutionIds = affectedLectures
			.Where(x => x.ExistingSubstitutionId.HasValue)
			.Select(x => x.ExistingSubstitutionId!.Value)
			.Distinct()
			.ToList();
		availability.PendingSubstitutionIds = affectedLectures
			.Where(x => x.ExistingSubstitutionId.HasValue &&
				string.Equals(
					x.ExistingSubstitutionStatus,
					SubstitutionStatus.Pending.ToString(),
					StringComparison.OrdinalIgnoreCase))
			.Select(x => x.ExistingSubstitutionId!.Value)
			.Distinct()
			.ToList();
	}
}
