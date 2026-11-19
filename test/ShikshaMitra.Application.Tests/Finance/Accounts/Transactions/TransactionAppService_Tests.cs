using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Transactions;

public class TransactionAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ITransactionAppService _service;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly ICurrentTenant _currentTenant;

	public TransactionAppService_Tests()
	{
		_service = GetRequiredService<ITransactionAppService>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_CurrentUserId_Missing()
	{
		using (_currentPrincipalAccessor.Change(BuildPrincipalWithoutUserId()))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(async () =>
				await _service.GetListAsync(
					Guid.NewGuid(),
					new GetTransactionsRequest { Page = 1, PageSize = 10 },
					TransactionType.Income));
		}
	}

	[Theory]
	[InlineData(0, 10)]
	[InlineData(1, 0)]
	[InlineData(1, 101)]
	public async Task GetListAsync_Should_Reject_Invalid_Paging(int page, int pageSize)
	{
		var error = await Should.ThrowAsync<BusinessException>(() =>
			_service.GetListAsync(Guid.NewGuid(), new GetTransactionsRequest { Page = page, PageSize = pageSize }, TransactionType.Income));
		error.Code.ShouldBe(TransactionErrorCodes.InvalidPaging);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("0.001")]
	[InlineData("1000000000000")]
	public async Task CreateAsync_Should_Reject_Out_Of_Range_Money_Before_Persistence(string amount)
	{
		var input = new TransactionCreateRequest
		{
			CategoryId = Guid.NewGuid(), Name = "Donation", CategoryTags = [],
			Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
			PaymentMethod = 0, PaymentDate = DateTime.UtcNow
		};
		await Should.ThrowAsync<AbpValidationException>(() =>
			_service.CreateAsync(Guid.NewGuid(), input, TransactionType.Income));
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Fractional_Paise()
	{
		var input = new TransactionCreateRequest
		{
			CategoryId = Guid.NewGuid(), Name = "Donation", CategoryTags = [],
			Amount = 12.345m, PaymentMethod = 0, PaymentDate = DateTime.UtcNow
		};
		var error = await Should.ThrowAsync<BusinessException>(() =>
			_service.CreateAsync(Guid.NewGuid(), input, TransactionType.Income));
		error.Code.ShouldBe(TransactionErrorCodes.InvalidAmount);
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Unknown_Payment_Method()
	{
		var input = new TransactionCreateRequest
		{
			CategoryId = Guid.NewGuid(), Name = "Donation", CategoryTags = [],
			Amount = 100m, PaymentMethod = 7, PaymentDate = DateTime.UtcNow
		};
		await Should.ThrowAsync<AbpValidationException>(() =>
			_service.CreateAsync(Guid.NewGuid(), input, TransactionType.Income));
	}

	private ClaimsPrincipal BuildPrincipalWithoutUserId()
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserName, "admin-no-userid"),
			new(AbpClaimTypes.Role, "admin"),
			new(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
