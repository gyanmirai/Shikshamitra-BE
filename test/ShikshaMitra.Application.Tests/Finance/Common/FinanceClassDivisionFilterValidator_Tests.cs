using System;
using Shouldly;
using ShikshaMitra.Finance.Common;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Common;

public class FinanceClassDivisionFilterValidator_Tests
{
	[Fact]
	public void Validate_Should_Normalize_Empty_Guids()
	{
		var filter = new FinanceClassDivisionFilterDto
		{
			StandardId = Guid.Empty,
			DivisionId = Guid.Empty,
		};

		FinanceClassDivisionFilterValidator.Validate(filter);

		filter.StandardId.ShouldBeNull();
		filter.DivisionId.ShouldBeNull();
	}

	[Fact]
	public void Validate_Should_Throw_When_Division_Provided_Without_Standard()
	{
		var filter = new FinanceClassDivisionFilterDto
		{
			DivisionId = Guid.NewGuid(),
		};

		Should.Throw<AbpValidationException>(() => FinanceClassDivisionFilterValidator.Validate(filter));
	}

	[Fact]
	public void Validate_Should_Pass_When_Standard_And_Division_Are_Set()
	{
		var filter = new FinanceClassDivisionFilterDto
		{
			StandardId = Guid.NewGuid(),
			DivisionId = Guid.NewGuid(),
		};

		FinanceClassDivisionFilterValidator.Validate(filter);
	}
}
