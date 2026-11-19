using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentRecordsSettings_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IStudentRecordsSettingsAppService _service;
	private readonly StudentRecordsPolicyResolver _resolver;
	private readonly ICurrentTenant _tenant;
	private readonly ICurrentPrincipalAccessor _principals;
	public StudentRecordsSettings_Tests()
	{
		_service = GetRequiredService<IStudentRecordsSettingsAppService>();
		_resolver = GetRequiredService<StudentRecordsPolicyResolver>();
		_tenant = GetRequiredService<ICurrentTenant>();
		_principals = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task Draft_is_not_effective_until_published_and_stale_updates_are_rejected()
	{
		using var tenant = _tenant.Change(Guid.NewGuid());
		using var actor = _principals.Change(ActorPrincipal(await CreateActorAsync()));
		await EnsureRoleAsync();
		var initial = await _service.GetAsync(null);
		initial.IsStarter.ShouldBeTrue();
		var policy = StudentRecordsPolicy.Starter(); policy.Provisioning.Mode = "Full";
		var draft = await _service.SaveDraftAsync(new() { Policy = policy });
		draft.DraftPolicy.Provisioning.Mode.ShouldBe("Full");
		draft.EffectivePolicy.Provisioning.Mode.ShouldBe("Summary");
		(await Should.ThrowAsync<BusinessException>(() => _service.SaveDraftAsync(new() { Policy = policy, ConcurrencyStamp = "stale" })))
			.Code.ShouldBe("Students:Records:ConfigurationChanged");
		var published = await _service.PublishAsync(new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
		published.IsStarter.ShouldBeFalse(); published.EffectivePolicy.Provisioning.Mode.ShouldBe("Full");
		var firstVersionId = published.PublishedVersionId;
		policy.Provisioning.Mode = "OnboardingOnly";
		var revised = await _service.SaveDraftAsync(new() { Policy = policy, ConcurrencyStamp = published.ConcurrencyStamp });
		revised.EffectivePolicy.Provisioning.Mode.ShouldBe("Full");
		await _service.PublishAsync(new() { ConcurrencyStamp = revised.ConcurrencyStamp! });
		var retained = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().GetAsync(firstVersionId!.Value));
		StudentRecordsPolicy.FromJson(retained.PolicyJson).Provisioning.Mode.ShouldBe("Full");
	}

	[Fact]
	public async Task Published_year_override_wins_and_unpublished_year_draft_inherits_school()
	{
		using var tenant = _tenant.Change(Guid.NewGuid());
		using var actor = _principals.Change(ActorPrincipal(await CreateActorAsync()));
		await EnsureRoleAsync();
		var yearId = Guid.NewGuid();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
			new AcademicYear(yearId, _tenant.Id, "2026-2027", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), autoSave: true));
		var school = StudentRecordsPolicy.Starter(); school.Provisioning.Mode = "Full";
		var savedSchool = await _service.SaveDraftAsync(new() { Policy = school });
		await _service.PublishAsync(new() { ConcurrencyStamp = savedSchool.ConcurrencyStamp! });
		var yearPolicy = StudentRecordsPolicy.Starter(); yearPolicy.Provisioning.Mode = "OnboardingOnly";
		var savedYear = await _service.SaveDraftAsync(new() { AcademicYearId = yearId, Policy = yearPolicy });
		savedYear.EffectivePolicy.Provisioning.Mode.ShouldBe("Full");
		await _service.PublishAsync(new() { AcademicYearId = yearId, ConcurrencyStamp = savedYear.ConcurrencyStamp! });
		(await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(yearId))).Policy.Provisioning.Mode.ShouldBe("OnboardingOnly");
		(await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(null))).Policy.Provisioning.Mode.ShouldBe("Full");
	}

	[Fact]
	public async Task Foreign_tenant_configuration_roles_and_year_are_never_accepted()
	{
		var tenantId = Guid.NewGuid(); var yearId = Guid.NewGuid();
		using (_tenant.Change(tenantId))
		{
			using var actor = _principals.Change(ActorPrincipal(await CreateActorAsync()));
			await EnsureRoleAsync();
			(await _service.GetRoleNamesAsync()).Items.Select(x => x.Name).ShouldBe(["admin"]);
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new AcademicYear(yearId, tenantId, "2026-2027", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), autoSave: true));
			var policy = StudentRecordsPolicy.Starter(); policy.Provisioning.Mode = "Full";
			var draft = await _service.SaveDraftAsync(new() { Policy = policy });
			await _service.PublishAsync(new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
		}
		using (_tenant.Change(Guid.NewGuid()))
		{
			using var actor = _principals.Change(ActorPrincipal(await CreateActorAsync()));
			(await _service.GetRoleNamesAsync()).Items.ShouldBeEmpty();
			(await _service.GetAsync(null)).IsStarter.ShouldBeTrue();
			(await Should.ThrowAsync<BusinessException>(() => _service.GetAsync(yearId))).Code.ShouldBe("Students:Records:InvalidAcademicYear");
			(await Should.ThrowAsync<BusinessException>(() => _service.SaveDraftAsync(new() { Policy = StudentRecordsPolicy.Starter() })))
				.Code.ShouldBe("Students:Records:InvalidApproverRole");
		}
		using (_tenant.Change(null))
			(await Should.ThrowAsync<BusinessException>(() => _service.GetAsync(null))).Code.ShouldBe("Students:Records:TenantRequired");
	}

	[Fact]
	public async Task Invalid_stage_role_is_rejected_before_any_draft_write()
	{
		using var tenant = _tenant.Change(Guid.NewGuid());
		using var actor = _principals.Change(ActorPrincipal(await CreateActorAsync()));
		await EnsureRoleAsync();
		var policy = StudentRecordsPolicy.Starter();
		policy.Changes["Identity"].Stages = [new() { RoleNames = ["Unknown"] }];
		(await Should.ThrowAsync<BusinessException>(() => _service.SaveDraftAsync(new() { Policy = policy }))).Code.ShouldBe("Students:Records:InvalidApproverRole");
		(await _service.GetAsync(null)).Id.ShouldBeNull();
	}

	[Fact]
	public async Task Inactive_database_actor_is_denied_even_with_authenticated_claims()
	{
		using var tenant = _tenant.Change(Guid.NewGuid());
		var actorId = await CreateActorAsync();
		using var actor = _principals.Change(ActorPrincipal(actorId));
		await EnsureRoleAsync();
		var draft = await _service.SaveDraftAsync(new() { Policy = StudentRecordsPolicy.Starter() });
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var user = await users.GetAsync(actorId);
			user.SetIsActive(false);
			await users.UpdateAsync(user, autoSave: true);
		});
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.GetAsync(null));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.GetRoleNamesAsync());
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.PreviewAsync(new() { Policy = StudentRecordsPolicy.Starter() }));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.SaveDraftAsync(new() { Policy = StudentRecordsPolicy.Starter(), ConcurrencyStamp = draft.ConcurrencyStamp }));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.PublishAsync(new() { ConcurrencyStamp = draft.ConcurrencyStamp! }));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().GetCountAsync())).ShouldBe(0);
	}

	private Task<Guid> CreateActorAsync() => WithUnitOfWorkAsync(async () =>
	{
		var id = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
			new IdentityUser(id, $"records-settings-{id:N}", $"{id:N}@example.test", _tenant.Id), autoSave: true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), _tenant.Id, userId: id, description: "Settings test tenant scope"), autoSave: true);
		return id;
	});

	private ClaimsPrincipal ActorPrincipal(Guid id) => new(new ClaimsIdentity([
		new Claim(AbpClaimTypes.UserId, id.ToString()), new Claim(AbpClaimTypes.UserName, "records-settings-test"),
		new Claim(AbpClaimTypes.TenantId, _tenant.Id!.Value.ToString())
	], "Test"));

	private Task EnsureRoleAsync() => WithUnitOfWorkAsync(async () =>
	{
		var roles = GetRequiredService<IIdentityRoleRepository>();
		if (!(await roles.GetListAsync()).Any(x => x.TenantId == _tenant.Id && x.Name == "admin"))
			await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", _tenant.Id), autoSave: true);
	});
}
