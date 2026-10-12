using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceScopeAccessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private readonly FinanceScopeAccessService _access;
	private readonly ICurrentTenant _tenant;
	private readonly ICurrentPrincipalAccessor _principal;
	private readonly IRepository<AcademicYear, Guid> _years;
	private readonly IRepository<IdentityUser, Guid> _users;
	private readonly IRepository<IdentityRole, Guid> _roles;

	public FinanceScopeAccessService_Tests()
	{
		_access = GetRequiredService<FinanceScopeAccessService>();
		_tenant = GetRequiredService<ICurrentTenant>();
		_principal = GetRequiredService<ICurrentPrincipalAccessor>();
		_years = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
	}

	[Fact]
	public async Task Database_administrator_can_configure_a_valid_year_before_its_first_student()
	{
		await RunAsync(true, async yearId =>
		{
			await _access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null);
			(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, null, null)).ShouldBeTrue();
			// The setup exception does not turn an unknown student into an accessible one.
			await Should.ThrowAsync<AbpAuthorizationException>(() => _access.EnsureStudentAccessibleAsync(Guid.NewGuid()));
		});
	}

	[Fact]
	public Task An_administrator_claim_without_database_membership_does_not_grant_setup_access() =>
		RunAsync(false, AssertYearDeniedAsync);

	[Fact]
	public Task Missing_and_foreign_tenant_years_remain_inaccessible() =>
		RunAsync(true, async _ =>
		{
			await AssertYearDeniedAsync(Guid.NewGuid());
			var foreignYear = Guid.NewGuid();
			using (_tenant.Change(Guid.NewGuid()))
			{
				await _years.InsertAsync(new AcademicYear(foreignYear, _tenant.Id, "Foreign QA year",
					new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			}
			await AssertYearDeniedAsync(foreignYear);
		});

	[Fact]
	public Task Class_context_still_requires_a_scoped_student() =>
		RunAsync(true, async yearId =>
		{
			var classId = Guid.NewGuid();
			(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, classId, null)).ShouldBeFalse();
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, classId, null));
		});

	[Fact]
	public Task Missing_identity_remains_denied() =>
		RunAsync(true, async yearId =>
		{
			using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				await Should.ThrowAsync<AbpAuthorizationException>(() =>
					_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null));
			}
		});

	private async Task AssertYearDeniedAsync(Guid yearId)
	{
		(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, null, null)).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() =>
			_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null));
	}

	private async Task RunAsync(bool databaseAdministrator, Func<Guid, Task> assertions)
	{
		using (_tenant.Change(TestTenantId))
		{
			var userId = Guid.NewGuid();
			using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity([
				new Claim(AbpClaimTypes.UserId, userId.ToString()),
				new Claim(AbpClaimTypes.UserName, $"finance-qa-{userId:N}"),
				new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())
			], "Test"))))
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var user = new IdentityUser(userId, $"finance-qa-{userId:N}", $"{userId:N}@example.test", TestTenantId);
					if (databaseAdministrator)
					{
						var role = (await _roles.GetListAsync(item => item.Name == "admin")).FirstOrDefault();
						if (role is null)
							role = await _roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId), true);
						user.AddRole(role.Id);
					}
					await _users.InsertAsync(user, true);
					var year = await _years.InsertAsync(new AcademicYear(Guid.NewGuid(), TestTenantId, $"QA-{userId:N}"[..20],
						new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
					await assertions(year.Id);
				});
			}
		}
	}
}
