using System;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeSelectionOnboardingReader_Tests
{
	[Theory]
	[InlineData("match", true)]
	[InlineData("student", false)]
	[InlineData("year", false)]
	[InlineData("structure", false)]
	[InlineData("revision", false)]
	[InlineData("heads", false)]
	[InlineData("malformed", false)]
	[InlineData("missing", false)]
	public async Task Reads_exact_assignment_identity_revision_and_included_heads_without_writing_finance(string scenario, bool matches)
	{
		var application = Application(); var head = Guid.NewGuid(); var structure = Guid.NewGuid(); var year = Guid.NewGuid();
		application.SaveFeeSelection(new(structure, "QA", 2, year, Guid.NewGuid(), Guid.NewGuid(), "INR",
			[new(head, "Form", 200m, false, true, null, null)], Guid.NewGuid(), DateTimeOffset.UtcNow), false);
		application.Submit(); application.Approve(Guid.NewGuid(), null);
		application.FinalizeApprovedDirectAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, application.StudentId!.Value, structure, year);
		var snapshot = new StudentFeeAssignmentPreviewDto { StudentId = application.StudentId.Value, AcademicYearId = year,
			StructureId = structure, RevisionNumber = 2,
			Heads = [new() { FeeHeadId = head, Included = true }, new() { FeeHeadId = Guid.NewGuid(), Included = false }] };
		if (scenario == "student") snapshot.StudentId = Guid.NewGuid();
		if (scenario == "year") snapshot.AcademicYearId = Guid.NewGuid();
		if (scenario == "structure") snapshot.StructureId = Guid.NewGuid();
		if (scenario == "revision") snapshot.RevisionNumber++;
		if (scenario == "heads") snapshot.Heads[0].FeeHeadId = Guid.NewGuid();
		if (scenario != "missing") assignment.CaptureAssignment("test", scenario == "malformed" ? "{" : JsonSerializer.Serialize(snapshot));
		var repository = Substitute.For<IRepository<StudentFeeStructure, Guid>>();
		repository.FindAsync(Arg.Any<Expression<Func<StudentFeeStructure, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(assignment);
		var reader = new AdmissionFeeSelectionOnboardingReader(repository);
		(await reader.GetBlockerAsync(application)).ShouldContain(matches ? "Confirm" : "Finance");
		(await reader.GetBlockerAsync(application, false) == null).ShouldBe(matches);
		application.ConfirmOnboardingFeeSelection(Guid.NewGuid(), DateTimeOffset.UtcNow);
		(await reader.GetBlockerAsync(application) == null).ShouldBe(matches);
		repository.ReceivedCalls().ShouldAllBe(c => c.GetMethodInfo().Name == "FindAsync");
	}

	[Fact]
	public async Task No_saved_admissions_choice_preserves_existing_onboarding()
	{
		var repository = Substitute.For<IRepository<StudentFeeStructure, Guid>>();
		(await new AdmissionFeeSelectionOnboardingReader(repository).GetBlockerAsync(Application())).ShouldBeNull();
		repository.ReceivedCalls().ShouldBeEmpty();
	}

	private static AdmissionApplication Application() => new(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
