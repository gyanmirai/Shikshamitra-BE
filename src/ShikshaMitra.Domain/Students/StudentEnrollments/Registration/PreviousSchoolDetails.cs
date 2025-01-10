using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class PreviousSchoolDetails(
	string? previousSchoolName,
	string? previousSchoolAddress,
	string? previousSchoolBoard,
	string? previousSchoolMedium,
	string? tcNo,
	string? lastClassPassed,
	float? percentage
	) : ValueObject
{
	public string? PreviousSchoolName { get; private set; } = previousSchoolName;
	public string? PreviousSchoolAddress { get; private set; } = previousSchoolAddress;
	public string? PreviousSchoolBoard { get; private set; } = previousSchoolBoard;
	public string? PreviousSchoolMedium { get; private set; } = previousSchoolMedium;
	public string? TcNo { get; private set; } = tcNo;
	public string? LastClassPassed { get; private set; } = lastClassPassed;
	public float? Percentage { get; private set; } = percentage;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return PreviousSchoolName;
		yield return PreviousSchoolAddress;
		yield return PreviousSchoolBoard;
		yield return PreviousSchoolMedium;
		yield return TcNo;
		yield return LastClassPassed;
		yield return Percentage;
	}
}