using System;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Finance.Setup;

public class FinanceSetupProgress_Tests
{
	[Fact]
	public void Progress_is_metadata_only_and_review_is_not_readiness()
	{
		var year = (AcademicYear)Activator.CreateInstance(typeof(AcademicYear), nonPublic: true)!;
		year.SetProperty("AcademicSetupGuideProgressV1", "academic-marker");
		year.SetProperty("Sibling", "keep");
		FinanceSetupProgress.Read(year).CurrentStageKey.ShouldBe("accounts");
		FinanceSetupProgress.Read(year).CurrentSettingKey.ShouldBe("income-categories");
		var actor = Guid.NewGuid(); var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
		FinanceSetupProgress.Save(year, "review", null, ["fee-structures"], true, actor, now);
		FinanceSetupProgress.Read(year).ReviewedAt.ShouldBe(now);
		FinanceSetupProgress.Save(year, "accounts", "income-categories", [], false, actor, now);
		FinanceSetupProgress.Read(year).ReviewedAt.ShouldBeNull();
		year.GetProperty<string>("AcademicSetupGuideProgressV1").ShouldBe("academic-marker");
		year.GetProperty<string>("Sibling").ShouldBe("keep");
	}

	[Theory]
	[InlineData("welcome", null)]
	[InlineData("accounts", "fee-categories")]
	[InlineData("assignment", "fee-structures")]
	public void Invalid_stage_setting_pairs_are_rejected(string stage, string? key)
	{
		FinanceSetupProgress.IsValid(stage, key, []).ShouldBeFalse();
	}

	[Fact]
	public void Invalid_deferred_finish_and_corrupt_metadata_do_not_overwrite_state()
	{
		var year = (AcademicYear)Activator.CreateInstance(typeof(AcademicYear), nonPublic: true)!;
		FinanceSetupProgress.IsValid("review", null, ["bank-accounts", "bank-accounts"]).ShouldBeFalse();
		FinanceSetupProgress.IsValid("review", null, ["unknown"]).ShouldBeFalse();
		Should.Throw<BusinessException>(() => FinanceSetupProgress.Save(year, "operations", null, [], true, Guid.NewGuid(), DateTime.UtcNow));
		year.SetProperty(FinanceSetupProgress.Property, "{\"Version\":2}");
		Should.Throw<BusinessException>(() => FinanceSetupProgress.Read(year)).Code.ShouldBe(FinanceSetupKeys.InvalidStoredState);
		year.GetProperty<string>(FinanceSetupProgress.Property).ShouldBe("{\"Version\":2}");
	}
}
