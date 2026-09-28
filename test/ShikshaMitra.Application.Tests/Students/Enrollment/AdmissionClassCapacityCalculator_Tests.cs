using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionClassCapacityCalculator_Tests
{
	[Fact]
	public void Unconfigured_class_has_unknown_capacity_not_zero_seats()
	{
		var result = AdmissionClassCapacityCalculator.Calculate(null, 12);
		result.RemainingSeats.ShouldBeNull();
		result.IsFull.ShouldBeFalse();
		result.Warning.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(40, 39, 1, false, false)]
	[InlineData(40, 40, 0, true, false)]
	[InlineData(40, 41, 0, true, true)]
	public void Full_or_over_capacity_only_warns(
		int capacity, int occupied, int remaining, bool isFull, bool isOverCapacity)
	{
		var result = AdmissionClassCapacityCalculator.Calculate(capacity, occupied);
		result.RemainingSeats.ShouldBe(remaining);
		result.IsFull.ShouldBe(isFull);
		result.IsOverCapacity.ShouldBe(isOverCapacity);
		(result.Warning != null).ShouldBe(isFull);
	}
}
