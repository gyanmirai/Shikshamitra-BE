using System;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Fees.Collections;
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
		var reader = new AdmissionFeeSelectionOnboardingReader(repository, Substitute.For<IAdmissionFinanceIntegration>());
		(await reader.GetBlockerAsync(application)).ShouldNotBeNull().ShouldContain(matches ? "Confirm" : "Finance");
		(await reader.GetBlockerAsync(application, false) == null).ShouldBe(matches);
		application.ConfirmOnboardingFeeSelection(Guid.NewGuid(), DateTimeOffset.UtcNow);
		(await reader.GetBlockerAsync(application) == null).ShouldBe(matches);
		repository.ReceivedCalls().ShouldAllBe(c => c.GetMethodInfo().Name == "FindAsync");
	}

	[Fact]
	public async Task No_saved_admissions_choice_preserves_existing_onboarding()
	{
		var repository = Substitute.For<IRepository<StudentFeeStructure, Guid>>();
		(await new AdmissionFeeSelectionOnboardingReader(repository, Substitute.For<IAdmissionFinanceIntegration>()).GetBlockerAsync(Application())).ShouldBeNull();
		repository.ReceivedCalls().ShouldBeEmpty();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Admission_confirmation_is_derived_from_current_finance_settlement_without_a_manual_stamp(bool settled)
	{
		var application = PaidAdmission();
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(settled);
		var reader = new AdmissionFeeSelectionOnboardingReader(Substitute.For<IRepository<StudentFeeStructure, Guid>>(), finance);
		(await reader.IsConfirmedAsync(application)).ShouldBe(settled);
		(await reader.GetBlockerAsync(application) == null).ShouldBe(settled);
		application.GetFeeSelection()!.OnboardingConfirmedAt.ShouldBeNull();
		// No persistent cache: a reversal/reopened balance must be reflected on the next read.
		finance.IsSettledForOnboardingAsync(application.Id).Returns(!settled);
		(await reader.IsConfirmedAsync(application)).ShouldBe(!settled);
	}

	[Theory]
	[InlineData("unpaid")]
	[InlineData("not-finalized")]
	[InlineData("onboarding-choice")]
	[InlineData("deferred")]
	public async Task Other_fee_flows_keep_explicit_onboarding_confirmation(string scenario)
	{
		var application = PaidAdmission(scenario);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(true);
		var reader = new AdmissionFeeSelectionOnboardingReader(Substitute.For<IRepository<StudentFeeStructure, Guid>>(), finance);
		(await reader.IsConfirmedAsync(application)).ShouldBeFalse();
		(await reader.GetBlockerAsync(application)).ShouldNotBeNull().ShouldContain("Confirm");
		finance.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task A_conflicting_finance_assignment_cannot_be_automatically_confirmed()
	{
		var application = PaidAdmission();
		var repository = Substitute.For<IRepository<StudentFeeStructure, Guid>>();
		repository.FindAsync(Arg.Any<Expression<Func<StudentFeeStructure, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new StudentFeeStructure(Guid.NewGuid(), null, application.StudentId!.Value, Guid.NewGuid(), application.GetFeeSelection()!.AcademicYearId));
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(true);
		var reader = new AdmissionFeeSelectionOnboardingReader(repository, finance);
		(await reader.IsConfirmedAsync(application)).ShouldBeFalse();
		(await reader.GetBlockerAsync(application)).ShouldNotBeNull().ShouldContain("Finance assignment differs");
		finance.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Explicit_existing_confirmation_preserves_its_audit_evidence()
	{
		var application = PaidAdmission();
		var at = DateTimeOffset.UtcNow;
		application.ConfirmOnboardingFeeSelection(Guid.NewGuid(), at);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		var reader = new AdmissionFeeSelectionOnboardingReader(Substitute.For<IRepository<StudentFeeStructure, Guid>>(), finance);
		(await reader.IsConfirmedAsync(application)).ShouldBeTrue();
		application.GetFeeSelection()!.OnboardingConfirmedAt.ShouldBe(at);
		finance.ReceivedCalls().ShouldBeEmpty();
	}

	private static AdmissionApplication PaidAdmission(string scenario = "paid")
	{
		var application = Application();
		var at = DateTimeOffset.UtcNow;
		var stage = Guid.NewGuid();
		application.SaveFeeSelection(new(Guid.NewGuid(), "QA", 2, Guid.NewGuid(), Guid.NewGuid(), stage, "INR",
			[new(Guid.NewGuid(), "Tuition", 200m, false, true, null, null)], Guid.NewGuid(),
			scenario == "onboarding-choice" ? at.AddDays(1) : at.AddMinutes(-1)), false);
		if (scenario == "deferred")
		{
			application.SetCurrentStage(stage);
			application.DeferFeeCollection(stage, "Pay during Onboarding", Guid.NewGuid(), at);
		}
		if (scenario != "unpaid") application.RecordFinanceFeePayment(Guid.NewGuid());
		application.Submit(); application.Approve(Guid.NewGuid(), null);
		if (scenario != "not-finalized") application.FinalizeApprovedDirectAdmission(Guid.NewGuid(), Guid.NewGuid(), at.UtcDateTime);
		return application;
	}

	private static AdmissionApplication Application() => new(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
