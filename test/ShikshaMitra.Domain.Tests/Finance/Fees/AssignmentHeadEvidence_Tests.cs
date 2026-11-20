using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class AssignmentHeadEvidence_Tests
{
	[Fact]
	public void Selected_zero_heads_are_indexed_without_excluded_heads_or_duplicates()
	{
		var selected = Guid.NewGuid(); var excluded = Guid.NewGuid();
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		assignment.CaptureAssignment("evidence", JsonSerializer.Serialize(new { Heads = new[] {
			new { FeeHeadId = selected, Included = true, Amount = 0 },
			new { FeeHeadId = selected, Included = true, Amount = 0 },
			new { FeeHeadId = excluded, Included = false, Amount = 10 } } }));
		var property = typeof(StudentFeeStructure).GetProperty("IncludedFeeHeadIds");
		property.ShouldNotBeNull();
		((Guid[]?)property.GetValue(assignment)).ShouldBe(new[] { selected });
	}
	[Theory]
	[InlineData("{invalid")]
	[InlineData("null")]
	[InlineData("{\"Heads\":null}")]
	[InlineData("{\"Heads\":[{\"Included\":true,\"FeeHeadId\":\"invalid\"}]}")]
	public void Invalid_evidence_keeps_conservative_fallback(string snapshot)
	{
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		assignment.CaptureAssignment("fallback", snapshot);
		var property = typeof(StudentFeeStructure).GetProperty("IncludedFeeHeadIds");
		property.ShouldNotBeNull(); property.GetValue(assignment).ShouldBeNull();
	}
	[Fact]
	public void Empty_selection_is_distinct_from_missing_evidence()
	{
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		assignment.CaptureAssignment("empty", "{\"Heads\":[]}");
		var property = typeof(StudentFeeStructure).GetProperty("IncludedFeeHeadIds");
		property.ShouldNotBeNull(); ((Guid[]?)property.GetValue(assignment)).ShouldBeEmpty();
	}

	[Fact]
	public void Unpaid_correction_refreshes_index_and_retains_prior_snapshot()
	{
		var id=Guid.NewGuid(); var student=Guid.NewGuid(); var year=Guid.NewGuid(); var first=Guid.NewGuid(); var second=Guid.NewGuid();
		var assignment=new StudentFeeStructure(id,null,student,Guid.NewGuid(),year);
		var before=JsonSerializer.Serialize(new { Heads=new[] { new { FeeHeadId=first, Included=true } } });
		assignment.CaptureAssignment("before",before);
		var replacement=new StudentFeeStructure(id,null,student,Guid.NewGuid(),year);
		assignment.CorrectUnpaidAssignment(replacement,"after",JsonSerializer.Serialize(new { Heads=new[] { new { FeeHeadId=second, Included=true } } }),
			1,"[]","unused schedule correction",Guid.NewGuid(),DateTimeOffset.UtcNow);
		var property=typeof(StudentFeeStructure).GetProperty("IncludedFeeHeadIds"); property.ShouldNotBeNull();
		((Guid[]?)property.GetValue(assignment)).ShouldBe(new[] { second });
		assignment.ExtraProperties["AssignmentCorrectionHistory"].ToString()!.ShouldContain(first.ToString());
	}
}
