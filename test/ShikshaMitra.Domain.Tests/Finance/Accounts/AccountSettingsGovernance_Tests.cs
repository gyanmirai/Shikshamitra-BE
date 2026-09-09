using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using Shouldly;
using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountSettingsGovernance_Tests
{
	[Fact]
	public void Account_head_normalizes_identity_and_starts_at_version_one()
	{
		var head = CreateHead();

		head.Name.ShouldBe("Tuition Fee Income");
		head.NormalizedName.ShouldBe("TUITION FEE INCOME");
		head.Version.ShouldBe(1);
		head.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void Updating_a_head_preserves_identity_and_versions_the_change()
	{
		var head = CreateHead();
		var id = head.Id;
		head.Update("Tuition Revenue", AccountHeadType.Income, null, "Fee receipts");
		head.Id.ShouldBe(id);
		head.NormalizedName.ShouldBe("TUITION REVENUE");
		head.Version.ShouldBe(2);
	}

	[Fact]
	public void Deactivation_requires_a_reason_and_versions_the_change()
	{
		var head = CreateHead();

		var exception = Should.Throw<BusinessException>(() => head.Deactivate(" "));
		exception.Code.ShouldBe(AccountSettingErrorCodes.DeactivationReasonRequired);

		head.Deactivate("Replaced after bank migration");
		head.IsActive.ShouldBeFalse();
		head.Version.ShouldBe(2);
		head.DeactivationReason.ShouldBe("Replaced after bank migration");
	}

	[Fact]
	public void A_head_cannot_be_its_own_parent()
	{
		var id = Guid.NewGuid();
		var exception = Should.Throw<BusinessException>(() => new AccountHead(
			id, Guid.NewGuid(), Guid.NewGuid(), "Late Fee Income", AccountHeadType.Income, id, null));

		exception.Code.ShouldBe(AccountSettingErrorCodes.ParentAccountHeadCycle);
	}

	private static AccountHead CreateHead() => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " Tuition Fee Income ",
		AccountHeadType.Income, null, null);
}
