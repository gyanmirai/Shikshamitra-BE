using System;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class LeavePolicyImpactFingerprint_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Reversed_Dependency_Enumeration_Preserves_Fingerprint_But_Changed_Stamp_Does_Not(bool reversePayroll)
	{
		var year = Guid.Parse("00000000-0000-0000-0000-000000000001");
		var first = "00000000-0000-0000-0000-000000000002:stamp-a";
		var second = "00000000-0000-0000-0000-000000000003:stamp-b";
		var input = new LeavePolicyImpactRequest { AcademicYearId = year, AsOfDate = new DateTime(2026, 8, 1) };
		var result = new LeavePolicyImpactDto();
		var population = new LeaveImpactPopulation(true, [], [first, second]);
		var references = new LeaveImpactReferences([], [], [], [], [], 0, [first, second]);
		string Hash(LeaveImpactPopulation staff, LeaveImpactReferences refs) =>
			LeavePolicyImpactFingerprintBuilder.Build(year, year, year, input, result, [], staff, refs, true);
		var original = Hash(population, references);
		var reversed = population with { AssignmentDependencies = population.AssignmentDependencies.AsEnumerable().Reverse().ToList() };
		var reversedReferences = reversePayroll
			? references with { PayrollDependencies = references.PayrollDependencies.AsEnumerable().Reverse().ToList() }
			: references;
		Hash(reversed, reversedReferences).ShouldBe(original);
		Hash(reversed with { AssignmentDependencies = [first, second.Replace("stamp-b", "stamp-c", StringComparison.Ordinal)] }, reversedReferences)
			.ShouldNotBe(original);
	}
}
