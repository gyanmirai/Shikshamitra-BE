using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationTemplateAccessPolicy_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _actorId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly ICurrentUser _current = Substitute.For<ICurrentUser>();
	private readonly IUserScopeAccessService _scopes = Substitute.For<IUserScopeAccessService>();
	private readonly IPermissionChecker _permissions = Substitute.For<IPermissionChecker>();
	private readonly AcademicNotificationTemplateAccessPolicy _policy;
	private readonly IdentityUser _actor;
	private readonly UserScopeContext _scope;
	private const string EventKey = "Academic.Homework.Published";

	public AcademicNotificationTemplateAccessPolicy_Tests()
	{
		_tenant.Id.Returns(_tenantId); _current.Id.Returns(_actorId); _current.TenantId.Returns(_tenantId); _current.IsAuthenticated.Returns(true);
		_actor = new(_actorId, "template-editor", "editor@example.invalid", _tenantId);
		var users = Substitute.For<IRepository<IdentityUser, Guid>>();
		users.FindAsync(_actorId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_actor);
		_scope = new() { UserId = _actorId, TenantId = _tenantId, IsAdmin = true };
		_scopes.GetCurrentUserScopeAsync().Returns(_scope);
		_policy = new(_tenant, _current, users, _scopes, _permissions, null!);
	}

	[Theory]
	[InlineData(NotificationTemplateAction.Read, "Academics.Notification.View")]
	[InlineData(NotificationTemplateAction.Write, "Academics.Notification.Add")]
	[InlineData(NotificationTemplateAction.Delete, "Academics.Notification.Delete")]
	public async Task Exact_action_is_checked_even_with_administrator_data_scope(NotificationTemplateAction action, string permission)
	{
		_permissions.IsGrantedAsync(permission).Returns(true);
		await _policy.EnsureAsync(EventKey, null, action, default);
		await _permissions.Received(1).IsGrantedAsync(permission);
		_permissions.IsGrantedAsync(permission).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => _policy.EnsureAsync(EventKey, null, action, default));
	}

	[Fact]
	public async Task Send_permission_does_not_grant_template_management()
	{
		_permissions.IsGrantedAsync("Academics.Notification.Send").Returns(true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => _policy.EnsureAsync(EventKey, null, NotificationTemplateAction.Write, default));
	}

	[Fact]
	public async Task Narrow_scope_can_read_school_templates_but_cannot_change_school_wide_defaults()
	{
		_scope.IsAdmin = false; _scope.AcademicYearIds.Add(Guid.NewGuid());
		_permissions.IsGrantedAsync("Academics.Notification.View").Returns(true);
		_permissions.IsGrantedAsync("Academics.Notification.Add").Returns(true);
		await _policy.EnsureAsync(EventKey, null, NotificationTemplateAction.Read, default);
		await Should.ThrowAsync<AbpAuthorizationException>(() => _policy.EnsureAsync(EventKey, null, NotificationTemplateAction.Write, default));
	}

	[Theory]
	[InlineData("inactive")]
	[InlineData("foreign-session")]
	[InlineData("foreign-scope")]
	[InlineData("no-scope")]
	public async Task Invalid_identity_and_scope_cannot_read_template_content(string failure)
	{
		_permissions.IsGrantedAsync("Academics.Notification.View").Returns(true);
		if (failure == "inactive") _actor.SetIsActive(false);
		if (failure == "foreign-session") _current.TenantId.Returns(Guid.NewGuid());
		if (failure == "foreign-scope") _scope.TenantId = Guid.NewGuid();
		if (failure == "no-scope") _scope.IsAdmin = false;
		await Should.ThrowAsync<AbpAuthorizationException>(() => _policy.EnsureAsync(EventKey, null, NotificationTemplateAction.Read, default));
	}

	[Fact]
	public void Module_matching_is_exact_and_does_not_claim_other_catalogues()
	{
		_policy.SupportsEvent(EventKey).ShouldBeTrue();
		_policy.SupportsEvent("academic.Homework.Published").ShouldBeFalse();
		_policy.SupportsEvent("HR.Leave.Approved").ShouldBeFalse();
	}
}
