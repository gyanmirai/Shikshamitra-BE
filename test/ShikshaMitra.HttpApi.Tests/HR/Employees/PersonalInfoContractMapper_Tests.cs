using System.Text.Json;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[TestModule(TestTraits.Modules.HR)]
public class PersonalInfoContractMapper_Tests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public void Omitted_Supplementary_Fields_Should_Remain_Unspecified()
	{
		var request = JsonSerializer.Deserialize<PersonalInfoRequest>("{}", JsonOptions)!;
		var dto = PersonalInfoContractMapper.ToUpdateDto(request);

		dto.DesignationSpecified.ShouldBeFalse();
		dto.QualificationSpecified.ShouldBeFalse();
		dto.IdentificationMark1Specified.ShouldBeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Revised value")]
	[TestTier(TestTraits.Tiers.Critical)]
	public void Explicit_Supplementary_Fields_Should_Preserve_Value_And_Presence(string? value)
	{
		var json = JsonSerializer.Serialize(new
		{
			designation = value,
			qualification = value,
			identificationMark1 = value
		}, JsonOptions);
		var request = JsonSerializer.Deserialize<PersonalInfoRequest>(json, JsonOptions)!;

		var dto = PersonalInfoContractMapper.ToUpdateDto(request);

		dto.DesignationSpecified.ShouldBeTrue();
		dto.QualificationSpecified.ShouldBeTrue();
		dto.IdentificationMark1Specified.ShouldBeTrue();
		dto.Designation.ShouldBe(value);
		dto.Qualification.ShouldBe(value);
		dto.IdentificationMark1.ShouldBe(value);
		JsonSerializer.Serialize(request, JsonOptions).ShouldNotContain("Specified");
		JsonSerializer.Serialize(dto, JsonOptions).ShouldNotContain("Specified");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public void Presence_Should_Be_Tracked_Per_Field_And_Not_Accepted_From_Json()
	{
		var request = JsonSerializer.Deserialize<PersonalInfoRequest>(
			"{\"designation\":null,\"qualificationSpecified\":true,\"identificationMark1Specified\":true}",
			JsonOptions)!;

		var dto = PersonalInfoContractMapper.ToUpdateDto(request);

		dto.DesignationSpecified.ShouldBeTrue();
		dto.Designation.ShouldBeNull();
		dto.QualificationSpecified.ShouldBeFalse();
		dto.IdentificationMark1Specified.ShouldBeFalse();
	}
}
