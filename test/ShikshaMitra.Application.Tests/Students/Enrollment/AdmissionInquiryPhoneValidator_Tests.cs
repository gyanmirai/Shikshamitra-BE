using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryPhoneValidator_Tests
{
	[Theory]
	[InlineData("123")]
	[InlineData("5123456789")]
	[InlineData("987654321")]
	public void Invalid_Indian_mobile_numbers_are_rejected(string phone)
	{
		var error = Should.Throw<BusinessException>(() =>
			AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(phone));

		error.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
		error.Message.ShouldContain("10-digit Indian guardian mobile number");
	}

	[Fact]
	public void A_valid_mobile_is_normalized()
	{
		AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile("98765 43210")
			.ShouldBe("9876543210");
	}

	[Theory]
	[InlineData("9876543210", true)]
	[InlineData("123", false)]
	[InlineData("5123456789", false)]
	public void Mobile_readiness_uses_the_same_business_rule(string phone, bool expected)
	{
		AdmissionInquiryPhoneValidator.IsValidIndianMobile(phone).ShouldBe(expected);
	}
}
