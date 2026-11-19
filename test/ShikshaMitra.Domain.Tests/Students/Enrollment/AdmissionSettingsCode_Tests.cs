using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionSettingsCode_Tests
{
	[Theory]
	[InlineData("INQ")]
	[InlineData("COUNSELLOR")]
	[InlineData("WF")]
	[InlineData("FORM")]
	[InlineData("CYCLE")]
	public void Generated_reference_should_fit_settings_contract_and_keep_full_record_identity(string prefix)
	{
		var id = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
		var otherId = Guid.Parse("12345678-1234-1234-1234-1234567890ac");
		var code = AdmissionSettingsCode.ForCreate(null, prefix, id);

		code.Length.ShouldBeLessThanOrEqualTo(64);
		System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z][A-Z0-9_]*$").ShouldBeTrue();
		code.ShouldStartWith(prefix + "_");
		Guid.ParseExact(code[(prefix.Length + 1)..], "N").ShouldBe(id);
		code.ShouldNotBe(AdmissionSettingsCode.ForCreate(null, prefix, otherId));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" \t ")]
	public void Omitted_create_reference_should_generate_from_record_id(string? suppliedCode)
	{
		var id = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
		var code = AdmissionSettingsCode.ForCreate(suppliedCode, "WF", id);
		code.ShouldBe("WF_123456781234123412341234567890AB");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" \t ")]
	public void Omitted_update_reference_should_preserve_existing_reference_exactly(string? suppliedCode)
	{
		AdmissionSettingsCode.ForUpdate(suppliedCode, "Legacy-reference").ShouldBe("Legacy-reference");
	}

	[Theory]
	[InlineData("legacy_code", "LEGACY_CODE")]
	[InlineData(" legacy-code_01 ", "LEGACY-CODE_01")]
	public void Supplied_legacy_reference_should_stay_supported_and_normalized(string suppliedCode, string expected)
	{
		AdmissionSettingsCode.ForCreate(suppliedCode, "FORM", Guid.NewGuid()).ShouldBe(expected);
		AdmissionSettingsCode.ForUpdate(suppliedCode, "PREVIOUS_CODE").ShouldBe(expected);
	}
}
