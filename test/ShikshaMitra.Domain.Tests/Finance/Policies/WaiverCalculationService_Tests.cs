using System;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class WaiverCalculationService_Tests
{
	private readonly WaiverCalculationService _service = new();

	private static CollectionPolicy BuildPolicy(Action<CollectionPolicyUpdateModelBuilder>? configure = null)
	{
		var builder = new CollectionPolicyUpdateModelBuilder();
		configure?.Invoke(builder);
		return new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), builder.Build());
	}

	[Fact]
	public void Disabled_Waiver_Produces_Zero()
	{
		var policy = BuildPolicy(b => b.EnableWaiver = false);
		var result = _service.Calculate(policy, new WaiverCalculationInput(600, 1000, DateTime.Today));
		Assert.Equal(0, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Waiver_Exceeding_Remaining_Late_Fee_Is_Limited()
	{
		var policy = BuildPolicy();
		var result = _service.Calculate(policy, new WaiverCalculationInput(1200, 1000, DateTime.Today));
		Assert.Equal(1000, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Waiver_Within_Remaining_Late_Fee_Grants_Requested_Amount()
	{
		var policy = BuildPolicy();
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today));
		Assert.Equal(200, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Fee_Head_Outside_Late_Fee_Scope_Is_Not_Waivable()
	{
		var policy = BuildPolicy();
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today, FeeHeadId: Guid.NewGuid()));
		Assert.Equal(0, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Every_Waiver_Requires_Approval_When_Approval_Is_Enabled()
	{
		var policy = BuildPolicy(b => b.RequiresApproval = true);
		var result = _service.Calculate(policy, new WaiverCalculationInput(1, 1000, DateTime.Today));
		Assert.True(result.RequiresApproval);
	}

	[Fact]
	public void Waiver_Does_Not_Require_Approval_When_Approval_Is_Disabled()
	{
		var policy = BuildPolicy(b => b.RequiresApproval = false);
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today));
		Assert.False(result.RequiresApproval);
	}

	[Fact]
	public void Pre_Approved_Waiver_Skips_Approval_And_Still_Respects_Remaining_Late_Fee()
	{
		var policy = BuildPolicy(b => b.RequiresApproval = true);
		var result = _service.Calculate(policy, new WaiverCalculationInput(1200, 1000, DateTime.Today, WaiverPreApproved: true));
		Assert.False(result.RequiresApproval);
		Assert.Equal(1000, result.EffectiveWaiverAmount);
	}
}
