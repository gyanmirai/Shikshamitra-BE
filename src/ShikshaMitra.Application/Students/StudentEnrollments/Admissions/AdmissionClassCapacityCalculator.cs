using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionClassCapacityCalculator
{
	public static (int? RemainingSeats, bool IsFull, bool IsOverCapacity, string? Warning) Calculate(
		int? capacity, int occupied)
	{
		if (capacity is null)
			return (null, false, false, "Assign rooms to this class to calculate admission capacity.");
		var remaining = Math.Max(0, capacity.Value - occupied);
		var isFull = occupied >= capacity.Value;
		return (remaining, isFull, occupied > capacity.Value,
			isFull ? "Class capacity is full. You can continue, but this admission will exceed available seats." : null);
	}
}
