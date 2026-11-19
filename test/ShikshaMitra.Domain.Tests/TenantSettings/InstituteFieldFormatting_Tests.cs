using System.Collections.Generic;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.Formatting;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteFieldFormatting_Tests
{
	[Theory]
	[InlineData("buildings", "buildingName")]
	[InlineData("roomTypes", "shortName")]
	[InlineData("rooms", "roomName")]
	[InlineData("shifts", "shiftName")]
	[InlineData("schoolIdentity", "principalName")]
	[InlineData("schoolDocuments", "notes")]
	public void Each_scope_only_accepts_its_own_fields(string scope, string field)
	{
		var rules = InstituteFieldFormattingCatalog.Defaults(scope);
		rules[field] = AcademicTextCase.CamelCase;
		InstituteFieldFormattingCatalog.Validate(scope, rules);
		InstituteFieldFormattingCatalog.Format(rules, field, "morning shift").ShouldBe("morningShift");
		rules["foreignField"] = AcademicTextCase.Uppercase;
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Validate(scope, rules));
	}

	[Fact]
	public void Missing_fields_and_invalid_modes_are_rejected()
	{
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Defaults("unknown"));
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Validate("rooms", new()));
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Validate("rooms",
			new Dictionary<string, AcademicTextCase> { ["roomName"] = (AcademicTextCase)5, ["featureName"] = AcademicTextCase.Default }));
	}

	[Fact]
	public void Default_preserves_raw_text_and_optional_nulls()
	{
		var rules = InstituteFieldFormattingCatalog.Defaults("schoolDocuments");
		InstituteFieldFormattingCatalog.Format(rules, "name", "  aBc  ").ShouldBe("  aBc  ");
		InstituteFieldFormattingCatalog.Format(rules, "notes", null).ShouldBeNull();
	}

	[Fact]
	public void Persisted_text_limits_and_required_values_are_checked_after_formatting()
	{
		var rules = InstituteFieldFormattingCatalog.Defaults("roomTypes");
		rules["shortName"] = AcademicTextCase.CamelCase;
		InstituteFieldFormattingCatalog.Required(rules, "shortName", "a          b", 2).ShouldBe("aB");
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Required(rules, "shortName", "science lab", 5));
		Should.Throw<AbpValidationException>(() => InstituteFieldFormattingCatalog.Required(rules, "shortName", "   ", 10));
	}
}
