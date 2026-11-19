using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeResolution_Tests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IUserScopeResolutionRepository _repository = Substitute.For<IUserScopeResolutionRepository>();
    private readonly IUnitOfWorkManager _manager = Substitute.For<IUnitOfWorkManager>();
    private readonly UserScopeService _service;

    public UserScopeResolution_Tests()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(_tenantId);
        var lazy = Substitute.For<IAbpLazyServiceProvider>();
        lazy.LazyGetRequiredService<ICurrentTenant>().Returns(tenant);
        _service = new UserScopeService(_repository, _manager, NullLogger<UserScopeService>.Instance,
            Options.Create(new UserScopeOptions { CacheEnabled = true, CacheDurationMinutes = 15 })) { LazyServiceProvider = lazy };
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string>());
        _repository.GetReportingHierarchyAsync(Arg.Any<CancellationToken>()).Returns(new List<ScopeReportingEmployee>());
        _repository.GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserScopeAssignment> { new(Guid.NewGuid(), _tenantId, userId: _user) });
        NewRequest();
    }

    private void NewRequest()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.Items.Returns(new Dictionary<string, object>());
        uow.Outer.Returns((IUnitOfWork?)null);
        _manager.Current.Returns(uow);
    }

    [Fact]
    public async Task Reuse_is_local_and_scope_reduction_is_visible_on_the_next_request()
    {
        (await _service.GetUserScopeAsync(_user)).HasTenantWideScope().ShouldBeTrue();
        (await _service.GetUserScopeAsync(_user)).HasTenantWideScope().ShouldBeTrue();
        await _repository.Received(1).GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        _repository.GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserScopeAssignment>());
        NewRequest();
        var reduced = await _service.GetUserScopeAsync(_user);
        reduced.HasAnyAccess.ShouldBeFalse();
        reduced.FromCache.ShouldBeFalse();
    }

    [Fact]
    public async Task Local_invalidation_clears_reporting_ancestors_and_current_role_authority()
    {
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string> { "admin" });
        (await _service.GetUserScopeAsync(_user)).IsAdmin.ShouldBeTrue();
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string>());
        await _service.InvalidateUserScopeCacheAsync(Guid.NewGuid());
        (await _service.GetUserScopeAsync(_user)).IsAdmin.ShouldBeFalse();
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string> { "admin" });
        await _service.InvalidateRoleScopeCacheAsync("admin");
        (await _service.GetUserScopeAsync(_user)).IsAdmin.ShouldBeTrue();
        NewRequest();
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string>());
        (await _service.GetUserScopeAsync(_user)).IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task Missing_identity_and_failures_never_become_unrestricted_scope()
    {
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns((List<string>?)null);
        (await _service.GetUserScopeAsync(_user)).HasAnyAccess.ShouldBeFalse();
        await _repository.DidNotReceive().GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        NewRequest();
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(Task.FromException<List<string>?>(new InvalidOperationException("Synthetic DB failure")));
        await Should.ThrowAsync<InvalidOperationException>(() => _service.GetUserScopeAsync(_user));
        _repository.GetRoleNamesAsync(_user, Arg.Any<CancellationToken>()).Returns(new List<string>());
        (await _service.GetUserScopeAsync(_user)).IsAdmin.ShouldBeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => _service.GetUserScopeAsync(_user, cancellation.Token));
    }

    [Fact]
    public async Task Batch_resolution_preserves_admin_direct_role_and_reporting_access_with_constant_reads()
    {
        var candidates = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid()).ToArray();
        var admin = candidates[0];
        var direct = candidates[1];
        var roleUser = candidates[2];
        var manager = candidates[3];
        var reportee = Guid.NewGuid();
        var managerEmployee = Guid.NewGuid();
        var bridgeEmployee = Guid.NewGuid();
        var reporteeEmployee = Guid.NewGuid();
        _repository.GetReportingHierarchyAsync(Arg.Any<CancellationToken>()).Returns([
            new(managerEmployee, manager, null),
            new(bridgeEmployee, null, managerEmployee),
            new(reporteeEmployee, reportee, bridgeEmployee)
        ]);
        _repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ids = call.ArgAt<IReadOnlyCollection<Guid>>(0);
                return ids.ToDictionary(id => id, id =>
                    id == admin ? new List<string> { "admin" } :
                    id == roleUser ? new List<string> { "Counsellor" } : new List<string>());
            });
        _repository.GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserScopeAssignment> {
                new(Guid.NewGuid(), _tenantId, userId: direct),
                new(Guid.NewGuid(), _tenantId, roleName: "Counsellor"),
                new(Guid.NewGuid(), _tenantId, userId: reportee)
            });

        var scopes = await _service.GetUserScopesAsync(candidates);

        scopes.Count.ShouldBe(120);
        scopes[admin].IsAdmin.ShouldBeTrue();
        scopes[direct].HasAnyAccess.ShouldBeTrue();
        scopes[roleUser].HasAnyAccess.ShouldBeTrue();
        scopes[manager].HasAnyAccess.ShouldBeTrue();
        scopes[candidates[4]].HasAnyAccess.ShouldBeFalse();
        await _repository.Received(1).GetReportingHierarchyAsync(Arg.Any<CancellationToken>());
        await _repository.Received(1).GetRoleNamesByUserIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).GetAssignmentsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetRoleNamesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // The same request reuses authority, but the next request sees a revocation.
        (await _service.GetUserScopeAsync(direct)).HasAnyAccess.ShouldBeTrue();
        _repository.GetAssignmentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserScopeAssignment>());
        NewRequest();
        _repository.GetRoleNamesAsync(direct, Arg.Any<CancellationToken>()).Returns(new List<string>());
        (await _service.GetUserScopeAsync(direct)).HasAnyAccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Batch_resolution_fails_closed_for_missing_identity_and_does_not_cache_errors()
    {
        _repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, List<string>>());
        (await _service.GetUserScopesAsync([_user]))[_user].HasAnyAccess.ShouldBeFalse();

        NewRequest();
        _repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<Guid, List<string>>>(new InvalidOperationException("Synthetic DB failure")));
        await Should.ThrowAsync<InvalidOperationException>(() => _service.GetUserScopesAsync([_user]));

        _repository.GetRoleNamesByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, List<string>> { [_user] = [] });
        (await _service.GetUserScopesAsync([_user]))[_user].HasAnyAccess.ShouldBeTrue();
    }

    [Fact]
    public void Reporting_traversal_includes_unlinked_intermediates_and_terminates_cycles()
    {
        var manager = Guid.NewGuid();
        var bridge = Guid.NewGuid();
        var reportee = Guid.NewGuid();
        var reporteeUser = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        var population = UserScopeService.ResolveReportingUsers(_user, [
            new(manager, _user, reportee),
            new(bridge, null, manager),
            new(reportee, reporteeUser, bridge),
            new(Guid.NewGuid(), unrelated, null)
        ]);
        population.SetEquals(new[] { _user, reporteeUser }).ShouldBeTrue();
    }
}
