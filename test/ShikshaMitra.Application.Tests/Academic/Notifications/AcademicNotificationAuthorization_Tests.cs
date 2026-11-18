using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationAuthorization_Tests
{
	private const string Send = "Academics.Notification.Send";
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _actorId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly ICurrentUser _current = Substitute.For<ICurrentUser>();
	private readonly IRepository<IdentityUser, Guid> _users = Substitute.For<IRepository<IdentityUser, Guid>>();
	private readonly IUserScopeAccessService _scopes = Substitute.For<IUserScopeAccessService>();
	private readonly IPermissionChecker _permissions = Substitute.For<IPermissionChecker>();
	private readonly INotificationSourceAdapter _source = Substitute.For<INotificationSourceAdapter>();
	private readonly IAcademicNotificationSourceAuthorizer _authorizer = Substitute.For<IAcademicNotificationSourceAuthorizer>();
	private readonly IdentityUser _actor;

	public AcademicNotificationAuthorization_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_current.Id.Returns(_actorId);
		_current.TenantId.Returns(_tenantId);
		_current.IsAuthenticated.Returns(true);
		_actor = new(_actorId, "notification-actor", "actor@example.invalid", _tenantId);
		_users.FindAsync(_actorId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_actor);
		_scopes.GetCurrentUserScopeAsync().Returns(new UserScopeContext { UserId = _actorId, TenantId = _tenantId, IsAdmin = true });
		_permissions.IsGrantedAsync(Send).Returns(true);
		_source.EventKeys.Returns(new[] { "Academic.Homework.Published" });
		_authorizer.EventKeys.Returns(new[] { "Academic.Homework.Published" });
		_source.LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>()).Returns(new NotificationSourceSnapshot(
			true, null, "revision-1", "/academic/homework", new Dictionary<string, NotificationVariableValueDto>(), []));
	}

	[Fact]
	public void Send_is_a_canonical_action_with_existing_notification_visibility_and_no_add_alias()
	{
		ShikshaMitraSubmodulePermissions.Academics.Notification.All.ShouldContain(Send);
		ShikshaMitraSubmodulePermissions.Academics.Notification.DisplayNames.Keys.ShouldContain(Send);
		NavigationVisibilityPrerequisites.GetPaths(Send).ShouldBe(new[] {
			new[] { "ShikshaMitra.Module.Academics", "ShikshaMitra.Module.Academics.Notification" }
		});
	}

	[Fact]
	public async Task Direct_application_boundary_requires_send_before_loading_source()
	{
		_permissions.IsGrantedAsync(Send).Returns(false);
		_permissions.IsGrantedAsync("Academics.Notification.Add").Returns(true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service().RequireSendAsync(Input(), default));
		await _source.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Sender_without_template_administration_can_preview_an_authorized_source()
	{
		var result = await Service().RequireSendAsync(Input(), default);
		result.TenantId.ShouldBe(_tenantId);
		result.UserId.ShouldBe(_actorId);
		await _authorizer.Received(1).EnsureCanSendAsync(Input().EventKey, Input().SourceId, default);
	}

	[Theory]
	[InlineData("host")]
	[InlineData("foreign-session")]
	[InlineData("inactive")]
	[InlineData("foreign-scope")]
	[InlineData("missing-scope")]
	public async Task Invalid_identity_or_scope_fails_closed(string failure)
	{
		if (failure == "host") _tenant.Id.Returns((Guid?)null);
		if (failure == "foreign-session") _current.TenantId.Returns(Guid.NewGuid());
		if (failure == "inactive") _actor.SetIsActive(false);
		if (failure == "foreign-scope") _scopes.GetCurrentUserScopeAsync().Returns(new UserScopeContext { UserId = _actorId, TenantId = Guid.NewGuid(), IsAdmin = true });
		if (failure == "missing-scope") _scopes.GetCurrentUserScopeAsync().Returns(new UserScopeContext { UserId = _actorId, TenantId = _tenantId });
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service().RequireSendAsync(Input(), default));
		await _source.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Source_requires_an_explicit_authorizer_even_when_an_adapter_is_registered()
	{
		await Should.ThrowAsync<BusinessException>(() => Service([]).RequireSendAsync(Input(), default));
		await _source.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Source_assignment_deny_stops_preview_before_recipient_data_is_loaded()
	{
		_authorizer.EnsureCanSendAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service().RequireSendAsync(Input(), default));
		await _source.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
	}

	private AcademicNotificationAccessService Service(IEnumerable<IAcademicNotificationSourceAuthorizer>? authorizers = null) =>
		new(_tenant, _current, _users, _scopes, _permissions, [_source], authorizers ?? [_authorizer], new([_source]));

	private static AcademicNotificationAudienceInput Input() => new("Academic.Homework.Published", "Academic.Homework",
		Guid.Parse("7e96e744-cbdd-4cff-8e98-e7916c869063"), new(NotificationGuardianMode.PrimaryOnly, []), [CommunicationChannelType.Email], []);
}
