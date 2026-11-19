using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class ChargeWaiverApplication_Tests
{
	[Fact]
	public void Constructor_Records_Reason_Amount_And_Command_Key()
	{
		var entity = new ChargeWaiverApplication(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 500m, 275m, "Financial hardship", Guid.NewGuid(), "command-1");

		entity.EffectiveAmount.ShouldBe(275m);
		entity.Reason.ShouldBe("Financial hardship");
		entity.IdempotencyKey.ShouldBe("command-1");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Constructor_Rejects_Missing_Reason(string reason)
	{
		Should.Throw<BusinessException>(() => new ChargeWaiverApplication(Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, 275m, reason, Guid.NewGuid(), "command-1"))
			.Code.ShouldBe(FeeWaiverErrorCodes.ReasonRequired);
	}
}
