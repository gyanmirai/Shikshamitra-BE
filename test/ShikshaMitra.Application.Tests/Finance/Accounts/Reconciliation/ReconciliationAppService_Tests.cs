using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public class ReconciliationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IReconciliationAppService _service;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly ICurrentTenant _currentTenant;

	public ReconciliationAppService_Tests()
	{
		_service = GetRequiredService<IReconciliationAppService>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetListAsync_Should_Throw_When_CurrentUserId_Missing()
	{
		using (_currentPrincipalAccessor.Change(BuildPrincipalWithoutUserId()))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(async () =>
				await _service.GetListAsync(new GetReconciliationListInput
				{
					AcademicYearId = Guid.NewGuid(),
					Page = 1,
					PageSize = 10
				}));
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
