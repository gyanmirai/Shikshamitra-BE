using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCycle_Tests
{
	[Fact]
	public void New_cycle_starts_as_draft_and_normalizes_code()
	{
		var cycle = new AdmissionCycle(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Admissions 2027-28",
			" adm-2027 ",
			Guid.NewGuid(),
			new DateTime(2026, 10, 1),
			new DateTime(2027, 3, 31));

		cycle.Status.ShouldBe(AdmissionCycleStatus.Draft);
		cycle.Code.ShouldBe("ADM-2027");
	}

	[Fact]
	public void Date_range_must_be_positive()
	{
		Should.Throw<BusinessException>(() => new AdmissionCycle(
			Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2027, 3, 31), new DateTime(2027, 3, 1)))
			.Code.ShouldBe(AdmissionErrorCodes.CycleDateRangeInvalid);
	}

	[Fact]
	public void Published_cycle_is_immutable_and_can_close()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2026, 10, 1), new DateTime(2027, 3, 31));

		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 9, 1));
		cycle.Status.ShouldBe(AdmissionCycleStatus.Published);
		cycle.Close();
		cycle.Status.ShouldBe(AdmissionCycleStatus.Closed);
		Should.Throw<BusinessException>(() => cycle.UpdateDetails("Changed", "NEW", cycle.AcademicYearId,
			cycle.StartDate, cycle.EndDate)).Code.ShouldBe(AdmissionErrorCodes.CycleCannotBeModified);
	}
}
