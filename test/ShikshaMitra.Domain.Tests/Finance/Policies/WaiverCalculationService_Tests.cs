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
	public void Waiver_Exceeding_Maximum_Amount_Is_Capped()
	{
		// collection-policy-flow.md §7.3
		var policy = BuildPolicy(b => b.MaximumWaiverAmount = 500);
		var result = _service.Calculate(policy, new WaiverCalculationInput(600, 1000, DateTime.Today));
		Assert.Equal(500, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Waiver_Exceeding_Maximum_Percentage_Of_Late_Fee_Is_Capped()
	{
		var policy = BuildPolicy(b => b.MaximumWaiverPercentage = 50);
		var result = _service.Calculate(policy, new WaiverCalculationInput(600, 1000, DateTime.Today));
		Assert.Equal(500, result.EffectiveWaiverAmount); // 50% of 1000
	}

	[Fact]
	public void Waiver_Within_Caps_Grants_The_Full_Requested_Amount()
	{
		var policy = BuildPolicy(b => { b.MaximumWaiverAmount = 1000; b.MaximumWaiverPercentage = 80; });
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today));
		Assert.Equal(200, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Expired_Waiver_Window_Blocks_The_Waiver()
	{
		// collection-policy-flow.md §7.14
		var policy = BuildPolicy(b => b.WaiverExpiry = new DateTime(2026, 6, 30));
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, new DateTime(2026, 7, 15)));
		Assert.Equal(0, result.EffectiveWaiverAmount);
		Assert.True(result.IsExpired);
	}

	[Fact]
	public void Waiver_On_Or_Before_Expiry_Date_Is_Still_Allowed()
	{
		var policy = BuildPolicy(b => b.WaiverExpiry = new DateTime(2026, 6, 30));
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, new DateTime(2026, 6, 30)));
		Assert.False(result.IsExpired);
		Assert.Equal(200, result.EffectiveWaiverAmount);
	}

	[Fact]
	public void Waiver_Above_Threshold_Requires_Approval_When_Not_Pre_Approved()
	{
		var policy = BuildPolicy(b => { b.RequiresApproval = true; b.ApprovalThresholdAmount = 100; });
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today));
		Assert.True(result.RequiresApproval);
	}

	[Fact]
	public void Waiver_Below_Threshold_Does_Not_Require_Approval()
	{
		var policy = BuildPolicy(b => { b.RequiresApproval = true; b.ApprovalThresholdAmount = 500; });
		var result = _service.Calculate(policy, new WaiverCalculationInput(200, 1000, DateTime.Today));
		Assert.False(result.RequiresApproval);
	}

	[Fact]
	public void Pre_Approved_Waiver_Skips_The_Approval_Step_But_Still_Respects_Caps()
	{
		// implementation plan Phase 2: "pre-approval waives the approval step, not the ceiling"
		var policy = BuildPolicy(b => { b.RequiresApproval = true; b.ApprovalThresholdAmount = 100; b.MaximumWaiverAmount = 500; });
		var result = _service.Calculate(policy, new WaiverCalculationInput(600, 1000, DateTime.Today, WaiverPreApproved: true));
		Assert.False(result.RequiresApproval);
		Assert.Equal(500, result.EffectiveWaiverAmount); // cap still applies
	}
}
