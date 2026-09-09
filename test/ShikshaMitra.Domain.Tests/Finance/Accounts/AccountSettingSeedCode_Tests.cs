using System;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountSettingSeedCode_Tests
{
	[Fact]
	public void Codes_Should_Preserve_Uniqueness_When_Sequential_Ids_Share_A_Prefix()
	{
		var first = Guid.Parse("3a238da4-1234-5678-9abc-000000000001");
		var second = Guid.Parse("3a238da4-1234-5678-9abc-000000000002");
		var firstCode = AccountSettingSeedCode.FromId(first);
		var secondCode = AccountSettingSeedCode.FromId(second);
		firstCode.ShouldNotBe(secondCode);
		firstCode.Length.ShouldBeLessThanOrEqualTo(AccountSettingConsts.MaxCodeLength);
		secondCode.Length.ShouldBeLessThanOrEqualTo(AccountSettingConsts.MaxCodeLength);
		Guid.ParseExact(firstCode, "N").ShouldBe(first);
		Guid.ParseExact(secondCode, "N").ShouldBe(second);
		AccountSettingConsts.NormalizeCode(firstCode).ShouldBe(firstCode);
	}
}
