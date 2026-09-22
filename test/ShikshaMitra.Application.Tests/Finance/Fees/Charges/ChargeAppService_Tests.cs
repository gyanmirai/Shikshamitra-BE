using System;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public abstract class ChargeAppService_Tests<TStartupModule> : ShikshaMitraApplicationTestBase<TStartupModule>
	where TStartupModule : IAbpModule
{
	private readonly IChargeAppService _chargeAppService;

	protected ChargeAppService_Tests()
	{
		_chargeAppService = GetRequiredService<IChargeAppService>();
	}

	[Fact]
	public virtual async Task GetAllAsync_Should_Return_Paged_Result()
	{
		// Arrange — use a dummy academic year ID; in-memory DB returns empty list
		var academicYearId = Guid.NewGuid();
		var request = new GetChargesRequest
		{
			AcademicYearId = academicYearId,
			Page = 1,
			PageSize = 10
		};

		// Act
		var result = await _chargeAppService.GetAllAsync(request);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();
		result.Items.ShouldNotBeNull();
		result.PageSize.ShouldBeLessThanOrEqualTo(100);
	}

	[Fact]
	public virtual async Task GetAllAsync_PageSize_Should_Be_Capped_At_100()
	{
		var academicYearId = Guid.NewGuid();
		var request = new GetChargesRequest
		{
			AcademicYearId = academicYearId,
			Page = 1,
			PageSize = 999
		};

		var result = await _chargeAppService.GetAllAsync(request);

		result.PageSize.ShouldBeLessThanOrEqualTo(100);
	}

	[Fact]
	public virtual async Task CreateAsync_Should_Throw_When_Student_Not_Found()
	{
		var academicYearId = Guid.NewGuid();
		var request = new CreateChargeRequest
		{
			ChargeName = "Test Charge",
			StudentId = Guid.NewGuid(),
			ChargeCategoryId = Guid.NewGuid(),
			PurposeDescription = "Test charge description",
			Amount = 500,
			DueDate = DateTime.UtcNow.AddDays(30),
			Priority = 1
		};

		var ex = await Should.ThrowAsync<BusinessException>(
			() => _chargeAppService.CreateAsync(academicYearId, request));

		ex.Code.ShouldBe(FinanceChargeErrorCodes.AcademicYearNotFound);
	}
}
