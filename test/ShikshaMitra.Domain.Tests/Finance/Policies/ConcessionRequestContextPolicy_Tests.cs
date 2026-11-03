using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionRequestContextPolicy_Tests
{
	[Theory]
	[InlineData(null, ConcessionApplyOn.AllFees, true)]
	[InlineData(ConcessionApplyOn.AllOutstanding, ConcessionApplyOn.AllFees, true)]
	[InlineData(ConcessionApplyOn.AllFees, ConcessionApplyOn.AllOutstanding, false)]
	public async Task Selected_policy_controls_paid_installment_eligibility_instead_of_assignment_snapshot(
		ConcessionApplyOn? assignmentScope, ConcessionApplyOn selectedScope, bool expectPaid)
	{
		var f = Fixture(assignmentScope, selectedScope);
		var context = await f.Manager.GetContextAsync(f.YearId, f.StudentId, f.AssignmentId,
			f.Selected.PolicyId, f.SelectedVersion.Id);

		Assert.Equal(selectedScope, context.ApplyOn);
		Assert.Equal(expectPaid, context.EligibleDueFees.Any(x => x.Id == f.Paid.Id));
		Assert.Contains(context.EligibleDueFees, x => x.Id == f.Unpaid.Id);
		Assert.All(context.EligibleDueFees, due =>
		{
			Assert.Equal(f.StudentId, due.StudentId);
			Assert.Equal(f.AssignmentId, due.StudentFeeStructureId);
		});
		Assert.Equal(10000m, f.Paid.PaidAmount);
		Assert.Equal(0m, f.Paid.ConcessionAmount);
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData(ConcessionApplyOn.AllOutstanding, false)]
	[InlineData(ConcessionApplyOn.AllFees, true)]
	public async Task Context_without_explicit_selection_retains_assignment_policy_behavior(
		ConcessionApplyOn? assignmentScope, bool expectPaid)
	{
		var f = Fixture(assignmentScope, ConcessionApplyOn.AllFees);
		var context = await f.Manager.GetContextAsync(f.YearId, f.StudentId, f.AssignmentId);
		Assert.Equal(expectPaid, context.EligibleDueFees.Any(x => x.Id == f.Paid.Id));
		Assert.Contains(context.EligibleDueFees, x => x.Id == f.Unpaid.Id);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Selected_context_rejects_unpaired_or_mismatched_policy_version(bool mismatched)
	{
		var f = Fixture(null, ConcessionApplyOn.AllFees);
		var exception = await Assert.ThrowsAsync<BusinessException>(() => f.Manager.GetContextAsync(
			f.YearId, f.StudentId, f.AssignmentId,
			mismatched ? Guid.NewGuid() : f.Selected.PolicyId,
			mismatched ? f.SelectedVersion.Id : null));
		Assert.Equal(ConcessionPolicyErrorCodes.PolicyVersionNotFound, exception.Code);
		Assert.Equal(0m, f.Paid.ConcessionAmount);
	}

	private static (ConcessionRequestManager Manager, Guid YearId, Guid StudentId, Guid AssignmentId,
		ConcessionPolicySnapshot Selected, FinancePolicyVersion SelectedVersion, DueFee Paid, DueFee Unpaid)
		Fixture(ConcessionApplyOn? assignmentScope, ConcessionApplyOn selectedScope)
	{
		var year = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), null, "Synthetic", null, "Student", year,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		ConcessionPolicySnapshot Policy(ConcessionApplyOn applyOn) => new(Guid.NewGuid(), year,
			"Context policy", "CONTEXT", ConcessionPolicyStatus.Active, false,
			ConcessionValueSource.PolicyFixed, ConcessionCalculationMethod.FixedAmount,
			null, 1538m, null, null, applyOn, RoundingMethodType.RoundUp, 1,
			AutoCreateRefund: applyOn == ConcessionApplyOn.AllFees);
		FinancePolicyVersion Version(ConcessionPolicySnapshot policy) => new(Guid.NewGuid(), null,
			"concession", policy.PolicyId, year, 1, policy.PolicyName, "Active",
			JsonSerializer.Serialize(policy), "created");
		var assigned = assignmentScope.HasValue ? Policy(assignmentScope.Value) : null;
		var assignedVersion = assigned is null ? null : Version(assigned);
		var selected = Policy(selectedScope);
		var selectedVersion = Version(selected);
		var structure = new FeeStructure(Guid.NewGuid(), null, Guid.NewGuid(), student.StandardId!.Value, "Context structure");
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, student.Id, structure.Id, year,
			assigned?.PolicyId, assignedVersion?.Id, ConcessionFeeHeadScope.EntireFeeStructure, []);
		DueFee Due(Guid studentId, Guid assignmentId, decimal amount)
		{
			var due = new DueFee(Guid.NewGuid(), null, studentId, Guid.NewGuid(), MonthEnum.January,
				2026, false, amount, new DateTime(2026, 1, 10));
			due.BindAssignment(assignmentId);
			return due;
		}
		var paid = Due(student.Id, assignment.Id, 10000m);
		paid.ApplyPayment(10000m);
		var unpaid = Due(student.Id, assignment.Id, 2000m);
		var otherStudent = Due(Guid.NewGuid(), assignment.Id, 1000m);
		var otherAssignment = Due(student.Id, Guid.NewGuid(), 1000m);
		var versions = new List<FinancePolicyVersion> { selectedVersion };
		if (assignedVersion is not null) versions.Add(assignedVersion);
		var generator = Substitute.For<IGuidGenerator>();
		generator.Create().Returns(_ => Guid.NewGuid());
		var manager = new ConcessionRequestManager(Substitute.For<IConcessionRequestRepository>(),
			Repository(student), Repository<ConcessionType>(), Repository(paid, unpaid, otherStudent, otherAssignment),
			Repository(assignment), Repository(structure), Repository<FeeHead>(), Repository(versions.ToArray()),
			new ConcessionPolicyCalculationService(), generator, NullLogger<ConcessionRequestManager>.Instance);
		return (manager, year, student.Id, assignment.Id, selected, selectedVersion, paid, unpaid);
	}

	private static IRepository<TEntity, Guid> Repository<TEntity>(params TEntity[] items)
		where TEntity : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetListAsync(Arg.Any<Expression<Func<TEntity, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.Where(call.Arg<Expression<Func<TEntity, bool>>>().Compile()).ToList());
		repository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.SingleOrDefault(item => item.Id == call.Arg<Guid>()));
		repository.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => items.Single(item => item.Id == call.Arg<Guid>()));
		return repository;
	}
}
