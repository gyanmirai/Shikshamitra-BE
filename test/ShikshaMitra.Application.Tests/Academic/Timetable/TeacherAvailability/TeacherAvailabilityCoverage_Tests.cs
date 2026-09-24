using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class TeacherAvailabilityCoverage_Tests
{
	[Fact]
	public void Apply_Should_Keep_Partially_Covered_Availability_Actionable()
	{
		var coveredSubstitutionId = Guid.NewGuid();
		var availability = new TeacherAvailabilityDto();
		var affectedLectures = new[]
		{
			new AffectedLectureDto
			{
				ExistingSubstitutionId = coveredSubstitutionId,
				ExistingSubstitutionStatus = SubstitutionStatus.Acknowledged.ToString(),
				CanCreateSubstitution = false
			},
			new AffectedLectureDto { CanCreateSubstitution = true }
		};

		TeacherAvailabilityCoverage.Apply(availability, affectedLectures);

		availability.AffectedLectureCount.ShouldBe(2);
		availability.CoveredLectureCount.ShouldBe(1);
		availability.UncoveredLectureCount.ShouldBe(1);
		availability.CanCreateSubstitution.ShouldBeTrue();
		availability.ActiveSubstitutionIds.ShouldBe([coveredSubstitutionId]);
	}

	[Fact]
	public void Apply_Should_Disable_Creation_Only_After_All_Lectures_Are_Covered()
	{
		var availability = new TeacherAvailabilityDto();
		var affectedLectures = new[]
		{
			new AffectedLectureDto
			{
				ExistingSubstitutionId = Guid.NewGuid(),
				ExistingSubstitutionStatus = SubstitutionStatus.Assigned.ToString(),
				CanCreateSubstitution = false
			},
			new AffectedLectureDto
			{
				ExistingSubstitutionId = Guid.NewGuid(),
				ExistingSubstitutionStatus = SubstitutionStatus.Acknowledged.ToString(),
				CanCreateSubstitution = false
			}
		};

		TeacherAvailabilityCoverage.Apply(availability, affectedLectures);

		availability.CoveredLectureCount.ShouldBe(2);
		availability.UncoveredLectureCount.ShouldBe(0);
		availability.CanCreateSubstitution.ShouldBeFalse();
	}
}
