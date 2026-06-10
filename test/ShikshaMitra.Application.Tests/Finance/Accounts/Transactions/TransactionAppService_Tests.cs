using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
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
