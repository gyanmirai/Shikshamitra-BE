using System;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeRequestValidator_Tests
{
	private readonly CreateChargeRequestValidator _validator = new();

	[Theory]
	[InlineData(1)]
	[InlineData(1.05)]
	[InlineData(9999999)]
	public void Supported_amounts_are_valid(decimal amount)
	{
		var result = _validator.Validate(CreateRequest(amount));

		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(1.005)]
	[InlineData(9999999.01)]
	public void Unsupported_amounts_are_rejected(decimal amount)
	{
		var result = _validator.Validate(CreateRequest(amount));

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateChargeRequest.Amount));
	}

	private static CreateChargeRequest CreateRequest(decimal amount) => new()
	{
		ChargeName = "Validated charge",
		ChargeCategoryId = Guid.NewGuid(),
		PurposeDescription = "Charge validator regression test",
		Amount = amount,
		DueDate = DateTimeOffset.UtcNow.AddDays(7),
		Priority = 1,
		StudentId = Guid.NewGuid()
	};
}
