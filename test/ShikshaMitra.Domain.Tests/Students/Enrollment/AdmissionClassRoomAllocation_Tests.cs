using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionClassRoomAllocation_Tests
{
	[Fact]
	public void Allocation_is_scoped_to_academic_year_and_class_not_to_admission_track()
	{
		var yearId = Guid.NewGuid();
		var standardId = Guid.NewGuid();
		var roomId = Guid.NewGuid();
		var allocation = new AdmissionClassRoomAllocation(Guid.NewGuid(), Guid.NewGuid(),
			yearId, standardId, roomId);

		allocation.AcademicYearId.ShouldBe(yearId);
		allocation.StandardId.ShouldBe(standardId);
		allocation.RoomId.ShouldBe(roomId);
	}
}
