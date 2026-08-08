using System;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmLeadContact_Tests
{
	[Theory]
	[InlineData("9999900807")]
	[InlineData("+91 99999 00807")]
	[InlineData("+1 (604) 555-0123")]
	[InlineData("020-7946-0958")]
	public void Valid_international_contact_numbers_are_normalized(string contact)
	{
		var lead = CreateLead(contact);

		lead.PrimaryContactNo.ShouldBe(contact);
		lead.NormalizedPrimaryContactNo.ShouldBe(new string(contact.Where(char.IsDigit).ToArray()));
	}

	[Theory]
	[InlineData("abc1")]
	[InlineData("123456")]
	[InlineData("1234567890123456")]
	[InlineData("+91 99999 CALLME")]
	[InlineData("+1 (604 555-0123")]
	[InlineData("++91 9999900807")]
	public void Malformed_contact_numbers_are_rejected(string contact)
	{
		var exception = Should.Throw<BusinessException>(() => CreateLead(contact));

		exception.Code.ShouldBe("AdmissionCrm:InvalidContactNumber");
	}

	[Theory]
	[InlineData("  O'Connor  ", "O'Connor")]
	[InlineData("  Anne-Marie  ", "Anne-Marie")]
	[InlineData("  આરવ  ", "આરવ")]
	[InlineData("  आरव  ", "आरव")]
	public void Valid_international_names_preserve_characters_and_trim_outer_whitespace(
		string firstName,
		string expected)
	{
		var lead = CreateLead("9999900807", firstName);

		lead.FirstName.ShouldBe(expected);
	}

	private static AdmissionInquiryLead CreateLead(string contact, string firstName = "Aarav") => new(
		Guid.NewGuid(),
		null,
		Guid.NewGuid(),
		null,
		"Reception",
		AdmissionInquiryChannel.WalkInReception,
		null,
		null,
		null,
		firstName,
		null,
		"Patel",
		null,
		"Riya Patel",
		contact,
		null,
		"General admission",
		null,
		"Grade 5",
		null,
		DateTimeOffset.UtcNow);
}
