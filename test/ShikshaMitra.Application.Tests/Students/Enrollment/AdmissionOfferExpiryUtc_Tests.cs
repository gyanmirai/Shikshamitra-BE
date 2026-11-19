using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferExpiryUtc_Tests
{
	[Fact]
	public void Decision_request_keeps_explicit_utc_instant()
	{
		var request = JsonSerializer.Deserialize<AdmissionApplicationDecisionDto>(
			"{\"OfferExpiresAt\":\"2026-09-30T11:30:00Z\"}");

		request.ShouldNotBeNull();
		request.OfferExpiresAt.ShouldNotBeNull();
		request.OfferExpiresAt.Value.UtcDateTime.ShouldBe(
			new DateTime(2026, 9, 30, 11, 30, 0, DateTimeKind.Utc));
	}

	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Utc_offer_expiry_does_not_shift_when_kind_is_missing(DateTimeKind kind)
	{
		var input = new DateTime(2026, 9, 30, 17, 0, 0, kind);

		var actual = AdmissionOfferUtcTime.Normalize(input);

		actual.ShouldBe(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc));
		actual.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void Local_offer_expiry_is_converted_to_utc_once()
	{
		var input = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Local);

		AdmissionOfferUtcTime.Normalize(input).ShouldBe(input.ToUniversalTime());
	}
}
