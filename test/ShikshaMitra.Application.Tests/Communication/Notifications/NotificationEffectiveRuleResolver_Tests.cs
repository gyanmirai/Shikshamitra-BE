using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationEffectiveRuleResolver_Tests
{
	private const string EventKey = "Academic.Homework.Published";
	private readonly Guid _tenant = Guid.NewGuid();
	private readonly Guid _year = Guid.NewGuid();
	private readonly Guid _board = Guid.NewGuid();
	private readonly Guid _medium = Guid.NewGuid();
	private readonly Guid _class = Guid.NewGuid();
	private readonly Guid _division = Guid.NewGuid();
	private readonly List<NotificationRule> _rows = [];
	private readonly ICurrentTenant _current = Substitute.For<ICurrentTenant>();
	private readonly NotificationEffectiveRuleResolver _resolver;
	public NotificationEffectiveRuleResolver_Tests()
	{
		_current.Id.Returns(_tenant);
		var repository = Substitute.For<IRepository<NotificationRule, Guid>>(); repository.GetQueryableAsync().Returns(_rows.AsQueryable());
		_resolver = new(_current, repository, new AsyncQueryableExecuter([]));
	}

	[Fact]
	public async Task The_deepest_matching_ancestor_wins_without_combining_another_branch()
	{
		Add(NotificationRuleScope.Create()); Add(NotificationRuleScope.Create(_year));
		var expected = Add(NotificationRuleScope.Create(_year, _board, _medium, _class));
		Add(NotificationRuleScope.Create(_year, _board, Guid.NewGuid(), _class, _division));
		var result = await _resolver.ResolveAsync(EventKey, Scope()); result.ShouldNotBeNull(); result.Id.ShouldBe(expected.Id);
	}

	[Fact]
	public async Task A_disabled_override_suppresses_an_enabled_parent_instead_of_falling_back()
	{
		var parent = Add(NotificationRuleScope.Create()); typeof(NotificationRule).GetProperty(nameof(NotificationRule.IsEnabled))!.SetValue(parent, true);
		var disabled = Add(NotificationRuleScope.Create(_year, _board, _medium));
		var result = await _resolver.ResolveAsync(EventKey, Scope());
		result.ShouldNotBeNull(); result.Id.ShouldBe(disabled.Id); result.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task Deleted_wrong_event_and_foreign_tenant_rows_are_not_candidates()
	{
		var expected = Add(NotificationRuleScope.Create());
		Add(Scope()).IsDeleted = true;
		_rows.Add(new(Guid.NewGuid(), Guid.NewGuid(), EventKey, Scope()));
		_rows.Add(new(Guid.NewGuid(), _tenant, "Academic.Assignment.Published", Scope()));
		var result = await _resolver.ResolveAsync(EventKey, Scope()); result.ShouldNotBeNull(); result.Id.ShouldBe(expected.Id);
	}

	[Fact]
	public async Task A_school_source_does_not_accidentally_inherit_a_year_specific_rule()
	{
		var expected = Add(NotificationRuleScope.Create()); Add(NotificationRuleScope.Create(_year));
		var result = await _resolver.ResolveAsync(EventKey, NotificationRuleScope.Create()); result.ShouldNotBeNull(); result.Id.ShouldBe(expected.Id);
	}

	[Fact]
	public async Task No_matching_rule_is_explicitly_absent_but_a_host_context_is_rejected()
	{
		(await _resolver.ResolveAsync(EventKey, Scope())).ShouldBeNull();
		_current.Id.Returns((Guid?)null);
		var error = await Should.ThrowAsync<BusinessException>(() => _resolver.ResolveAsync(EventKey, Scope()));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
	}

	[Fact]
	public async Task An_ambiguous_persisted_scope_is_rejected_instead_of_selecting_an_arbitrary_row()
	{
		Add(Scope()); Add(Scope());
		var error = await Should.ThrowAsync<BusinessException>(() => _resolver.ResolveAsync(EventKey, Scope()));
		error.Code.ShouldBe(NotificationErrorCodes.StateConflict);
	}
	private NotificationRuleScope Scope() => NotificationRuleScope.Create(_year, _board, _medium, _class, _division);
	private NotificationRule Add(NotificationRuleScope scope)
	{
		var row = new NotificationRule(Guid.NewGuid(), _tenant, EventKey, scope); _rows.Add(row); return row;
	}
}
