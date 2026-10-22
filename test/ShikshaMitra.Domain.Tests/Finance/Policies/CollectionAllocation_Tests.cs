using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionAllocation_Tests
{
	[Theory]
	[InlineData(CollectionAllocationBasis.Installment)]
	[InlineData(CollectionAllocationBasis.FeeHead)]
	public void Basis_Is_Preserved_In_Immutable_Snapshot(CollectionAllocationBasis basis)
	{
		var model = new CollectionPolicyUpdateModelBuilder { Status = CollectionPolicyStatus.Draft, EnableLateFee = false }.Build() with { AllocationBasis = basis };
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), model);
		var snapshot = CollectionPolicySnapshot.Deserialize(policy.ToSnapshot().Serialize());
		policy.Update(model with { AllocationBasis = CollectionAllocationBasis.Legacy });
		CollectionPolicy.FromSnapshot(policy.Id, null, policy.AcademicYearId, 1, snapshot).AllocationBasis.ShouldBe(basis);
	}

	[Fact]
	public void Historical_Snapshot_Without_Basis_Remains_Legacy() =>
		CollectionPolicySnapshot.Deserialize("{}").AllocationBasis.ShouldBe(CollectionAllocationBasis.Legacy);

	[Fact]
	public void Invalid_Basis_Does_Not_Change_Policy()
	{
		var model = new CollectionPolicyUpdateModelBuilder { Status = CollectionPolicyStatus.Draft, EnableLateFee = false }.Build();
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), model);
		Should.Throw<BusinessException>(() => policy.Update(model with { AllocationBasis = (CollectionAllocationBasis)99, PolicyName = "Changed name" }));
		policy.PolicyName.ShouldBe(model.PolicyName);
		policy.Version.ShouldBe(1);
	}

	[Fact]
	public void Allocates_In_Supplied_Screen_Order_With_Exact_Decimal_Remainder()
	{
		var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
		var result = SequentialCollectionAllocation.Allocate([
			new(first, CollectionAllocationComponent.Principal, 4000m),
			new(second, CollectionAllocationComponent.Principal, 1800m),
			new(third, CollectionAllocationComponent.Principal, 500m)], 5000.25m);
		result.Select(line => line.Id).ShouldBe(new[] { first, second });
		result.Select(line => line.Amount).ShouldBe(new[] { 4000m, 1000.25m });
		result.Sum(line => line.Amount).ShouldBe(5000.25m);
	}

	[Fact]
	public void Principal_Then_Late_Fee_Then_Charges_Uses_One_Wallet()
	{
		var due = Guid.NewGuid(); var charge = Guid.NewGuid();
		var result = SequentialCollectionAllocation.Allocate([
			new(due, CollectionAllocationComponent.Principal, 100m),
			new(due, CollectionAllocationComponent.LateFee, 0.10m),
			new(charge, CollectionAllocationComponent.Charge, 0.20m)], 100.30m);
		result.Count.ShouldBe(3);
		result.Sum(line => line.Amount).ShouldBe(100.30m);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(100.001)]
	[InlineData(101)]
	public void Rejects_Invalid_Or_Excess_Amount(double received) =>
		Should.Throw<BusinessException>(() => SequentialCollectionAllocation.Allocate([
			new(Guid.NewGuid(), CollectionAllocationComponent.Principal, 100m)], (decimal)received));

	[Fact]
	public void Duplicate_Target_Is_Rejected_Even_After_Amount_Is_Exhausted()
	{
		var target = new CollectionAllocationTarget(Guid.NewGuid(), CollectionAllocationComponent.Principal, 100m);
		Should.Throw<BusinessException>(() => SequentialCollectionAllocation.Allocate([target, target], 1m));
	}
}
