using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormEmailValueNormalizer_Tests
{
	[Theory]
	[InlineData("\" Parent@Example.COM \"", "\"parent@example.com\"")]
	[InlineData("Parent@Example.COM", "parent@example.com")]
	[InlineData("\"\"", "\"\"")]
	[InlineData("null", "null")]
	[InlineData(null, null)]
	[InlineData("", "")]
	[InlineData("[\"One@Example.COM\",\"\",null,\"TWO@Example.COM\"]", "[\"one@example.com\",\"\",null,\"two@example.com\"]")]
	[InlineData("{\"Email\":\"DO NOT MODIFY\"}", "{\"Email\":\"DO NOT MODIFY\"}")]
	public void Known_email_fields_normalize_only_email_strings(string? input, string? expected) =>
		AdmissionFormEmailValueNormalizer.Normalize(Field(), input).ShouldBe(expected);

	[Fact]
	public void Encoded_repeatable_email_arrays_keep_the_existing_outer_string_contract()
	{
		var input = JsonSerializer.Serialize("[\"One@Example.COM\",\"TWO@Example.COM\"]");
		var normalized = AdmissionFormEmailValueNormalizer.Normalize(Field(), input);
		JsonSerializer.Deserialize<string>(normalized!).ShouldBe("[\"one@example.com\",\"two@example.com\"]");
	}

	[Fact]
	public void Plus_address_tags_remain_part_of_the_email()
	{
		var normalized = AdmissionFormEmailValueNormalizer.Normalize(Field(), "\" Parent+Tag@Example.COM \"");
		JsonSerializer.Deserialize<string>(normalized!).ShouldBe("parent+tag@example.com");
	}

	[Fact]
	public void Metadata_email_is_supported_without_rewriting_text_or_readonly_answers()
	{
		const string value = "\"Parent@Example.COM\"";
		AdmissionFormEmailValueNormalizer.Normalize(Field(AdmissionFormFieldType.Text, "{\"format\":\"email\"}"), value)
			.ShouldBe("\"parent@example.com\"");
		AdmissionFormEmailValueNormalizer.Normalize(Field(AdmissionFormFieldType.Text), value).ShouldBe(value);
		AdmissionFormEmailValueNormalizer.Normalize(Field(AdmissionFormFieldType.Text, "malformed"), value).ShouldBe(value);
		AdmissionFormEmailValueNormalizer.Normalize(Field(readOnly: true), value).ShouldBe(value);
	}

	private static AdmissionFormFieldDefinition Field(AdmissionFormFieldType type = AdmissionFormFieldType.Email,
		string? validation = null, bool readOnly = false) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"EMAIL", "Email", type, AdmissionFormBindingType.Custom, null, false, readOnly, 0,
		null, null, null, validation, null);
}
