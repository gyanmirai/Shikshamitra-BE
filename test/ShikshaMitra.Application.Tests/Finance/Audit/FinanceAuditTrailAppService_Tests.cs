using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Shouldly;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditTrailAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IFinanceAuditTrailAppService _service;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly ICurrentTenant _currentTenant;

	public FinanceAuditTrailAppService_Tests()
	{
		_service = GetRequiredService<IFinanceAuditTrailAppService>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetByCorrelationAsync_Should_Throw_When_CorrelationId_Empty()
	{
		await Should.ThrowAsync<BusinessException>(async () =>
			await _service.GetByCorrelationAsync(" ", new PagedAndSortedResultRequestDto()));
	}

	[Fact]
	public async Task LookupStudentsForAuditAsync_Should_Throw_When_CurrentUserId_Missing()
	{
		using (_currentPrincipalAccessor.Change(BuildPrincipalWithoutUserId()))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(async () =>
				await _service.LookupStudentsForAuditAsync(new FinanceAuditStudentLookupRequestDto
				{
					AcademicYearId = Guid.NewGuid(),
					Search = "ab",
					MaxResultCount = 10
				}));
		}
	}

	private ClaimsPrincipal BuildPrincipalWithoutUserId()
	{
		var claims = new List<Claim>
		{
			new Claim(AbpClaimTypes.UserName, "admin-no-userid"),
			new Claim(AbpClaimTypes.Role, "admin"),
			new Claim(AbpClaimTypes.TenantId, (_currentTenant.Id ?? Guid.Empty).ToString())
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
