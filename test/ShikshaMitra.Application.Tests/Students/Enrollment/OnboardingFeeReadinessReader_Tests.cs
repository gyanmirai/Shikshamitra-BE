using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class OnboardingFeeReadinessReader_Tests
{
	[Theory]
	[InlineData(3000, FeeCollectionStatus.Partial, false)]
	[InlineData(6000, FeeCollectionStatus.InReconciliation, false)]
	[InlineData(6000, FeeCollectionStatus.Reversed, false)]
	[InlineData(6000, FeeCollectionStatus.Paid, true)]
	public async Task Reads_existing_finance_evidence_without_changing_finance(int paid, FeeCollectionStatus status, bool ready)
	{
		var studentId = Guid.NewGuid(); var yearId = Guid.NewGuid();
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, studentId, Guid.NewGuid(), yearId);
		var first = new DueFee(Guid.NewGuid(), null, studentId, Guid.NewGuid(), (MonthEnum)5, 2026, false, 6000m, new DateTime(2026, 6, 10));
		var later = new DueFee(Guid.NewGuid(), null, studentId, Guid.NewGuid(), (MonthEnum)11, 2026, false, 6000m, new DateTime(2026, 12, 10));
		first.BindAssignment(assignment.Id); later.BindAssignment(assignment.Id);
		first.ApplyPayment(paid);
		assignment.CaptureAssignment("test", JsonSerializer.Serialize(new {
			StudentId = studentId, AcademicYearId = yearId, ProratedTotal = 12000m,
			Lines = new[] { new { DueFeeId = first.Id }, new { DueFeeId = later.Id } }
		}));
		var receipt = new FeeRecordMetadata(Guid.NewGuid(), null, studentId, yearId, paid, "QA", status);
		var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), null, receipt.Id, first.Id, paid);
		var assignments = Substitute.For<IRepository<StudentFeeStructure, Guid>>();
		assignments.FindAsync(Arg.Any<Expression<Func<StudentFeeStructure, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => call.Arg<Expression<Func<StudentFeeStructure, bool>>>().Compile()(assignment) ? assignment : null);
		var dues = Substitute.For<IRepository<DueFee, Guid>>();
		dues.GetListAsync(Arg.Any<Expression<Func<DueFee, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => new[] { first, later }.Where(call.Arg<Expression<Func<DueFee, bool>>>().Compile()).ToList());
		var allocations = Substitute.For<IRepository<FeeCollectionDueFeeAllocation, Guid>>();
		allocations.GetListAsync(Arg.Any<Expression<Func<FeeCollectionDueFeeAllocation, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new List<FeeCollectionDueFeeAllocation> { allocation });
		var receipts = Substitute.For<IRepository<FeeRecordMetadata, Guid>>();
		receipts.GetListAsync(Arg.Any<Expression<Func<FeeRecordMetadata, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => new[] { receipt }.Where(call.Arg<Expression<Func<FeeRecordMetadata, bool>>>().Compile()).ToList());
		var reader = new OnboardingFeeReadinessReader(assignments, dues, allocations, receipts);
		(await reader.GetAsync(studentId, yearId)).FirstInstallmentSettled.ShouldBe(ready);
		(await reader.GetAsync(studentId, Guid.NewGuid())).Assigned.ShouldBeFalse();
		later.OutstandingAmount.ShouldBe(6000m);
		foreach (var repository in new object[] { assignments, dues, allocations, receipts })
			repository.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name.StartsWith("Get") || call.GetMethodInfo().Name == "FindAsync");
	}
}
