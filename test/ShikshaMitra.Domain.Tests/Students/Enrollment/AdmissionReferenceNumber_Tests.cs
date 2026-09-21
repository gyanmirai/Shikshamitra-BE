using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReferenceNumber_Tests
{
	[Theory]
	[InlineData(AdmissionReferenceKind.Inquiry, "INQ-2026-000001")]
	[InlineData(AdmissionReferenceKind.Application, "APP-2026-000001")]
	[InlineData(AdmissionReferenceKind.Family, "FAM-2026-000001")]
	public void FormatsReadableSequentialReferences(AdmissionReferenceKind kind, string expected)
	{
		AdmissionReferenceNumber.Format(kind, 2026, 1).ShouldBe(expected);
	}

	[Fact]
	public void SequenceReturnsCurrentValueBeforeAdvancing()
	{
		var sequence = AdmissionReferenceSequence.Create(
			System.Guid.NewGuid(), null, 2026, AdmissionReferenceKind.Application);

		sequence.TakeNext().ShouldBe(1);
		sequence.TakeNext().ShouldBe(2);
	}
}
