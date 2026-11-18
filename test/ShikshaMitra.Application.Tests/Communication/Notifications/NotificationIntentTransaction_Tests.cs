using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentTransaction_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IUnitOfWorkManager _units = Substitute.For<IUnitOfWorkManager>();
	private readonly IUnitOfWork _unit = Substitute.For<IUnitOfWork>();
	private readonly INotificationEventCaptureLock _writeLock = Substitute.For<INotificationEventCaptureLock>();
	private readonly ISettingManager _settings = Substitute.For<ISettingManager>();
	private readonly IRepository<NotificationIntent, Guid> _intentsRepository = Substitute.For<IRepository<NotificationIntent, Guid>>();
	private readonly List<NotificationRule> _rules = [];
	private readonly List<CommunicationTemplateRevision> _versions = [];
	private readonly List<CommunicationCampaign> _operations = [];
	private readonly List<NotificationIntent> _intents = [];
	private readonly List<string> _trace = [];
	private readonly List<INotificationSourceAdapter> _sources = [];
	private readonly NotificationSourceScopeDto _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	private readonly Source _source;
	private readonly NotificationRuleConfigurationCodec _configuration = new();
	private readonly AcademicNotificationCatalogue _catalogue;
	private readonly NotificationIntentWriter _writer;
	private readonly NotificationEventInput _input;
	private NotificationRule _rule = null!;
	public NotificationIntentTransaction_Tests()
	{
		_tenant.Id.Returns(_tenantId); _units.Current.Returns(_unit); _unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = true });
		_unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => { _trace.Add("flush"); return Task.CompletedTask; });
		_writeLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { _trace.Add("lock"); return Task.CompletedTask; });
		_settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns((string?)null);
		var student = new NotificationRecipientRefDto("Student", StudentId: Guid.NewGuid());
		_source = new(_trace, new(true, null, "version-1", "/academic/tasks", new Dictionary<string, NotificationVariableValueDto>(), [student],
			Now.AddDays(3), [new(_scope, [student])])); _sources.Add(_source); _catalogue = new(_sources);
		_input = new("Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "version-1", Now, []);
		AddRule(_input.EventKey);
		var rules = Repository(_rules); var versions = Repository(_versions); var operations = Repository(_operations);
		_intentsRepository.GetQueryableAsync().Returns(_ => _intents.AsQueryable());
		_intentsRepository.InsertAsync(Arg.Any<NotificationIntent>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.ArgAt<NotificationIntent>(0); _intents.Add(row); return row;
		});
		var executer = new AsyncQueryableExecuter([]);
		var planner = new NotificationIntentCapturePlanner(_tenant, [_catalogue], _sources, new(), new(_tenant, rules, executer),
			_configuration, new(_configuration), new(_tenant, versions, executer, _configuration), new(_tenant, _settings), new());
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		_writer = new(_tenant, _units, _writeLock, planner, operations, _intentsRepository, executer, guids, Substitute.For<IDataFilter>());
	}
	[Fact]
	public async Task Capture_flushes_source_state_and_creates_one_operation_with_a_frozen_intent()
	{
		var operation = await _writer.WriteAsync(_input, default); operation.ShouldNotBeNull();
		_operations.Count.ShouldBe(1); _operations[0].Id.ShouldBe(operation.Value); _operations[0].CampaignType.ShouldBe(NotificationCampaignTypes.Academic);
		_operations[0].SourceModule.ShouldBe(CommunicationSourceModule.Academic); _operations[0].Status.ShouldNotBe(CommunicationCampaignStatus.Sent);
		_intents.Count.ShouldBe(1); var row = _intents[0]; row.CampaignId.ShouldBe(operation.Value); row.TenantId.ShouldBe(_tenantId);
		row.Origin.ShouldBe(NotificationIntentOrigin.Automatic); row.RuleRevision.ShouldBe(_rule.Revision); row.RuleId.ShouldBe(_rule.Id);
		JsonSerializer.Deserialize<Guid[]>(row.TemplateRevisionIdsJson, Json).ShouldBe(new[] { _versions[0].Id });
		var snapshot = new NotificationIntentSnapshotCodec(_configuration).Deserialize(row.RuleConfigurationJson, _catalogue.Get(_input.EventKey));
		snapshot.SourceScope.ShouldBe(_scope); snapshot.Configuration.IncludeStudentAccount.ShouldBeFalse();
		_trace.IndexOf("flush").ShouldBeLessThan(_trace.IndexOf("source")); _trace.IndexOf("lock").ShouldBeLessThan(_trace.IndexOf("source"));
	}
	[Fact]
	public async Task All_scopes_and_more_than_ten_thousand_recipients_share_one_operation_without_truncation()
	{
		var references = Enumerable.Range(0, 10001).Select(_ => new NotificationRecipientRefDto("Student", StudentId: Guid.NewGuid())).ToArray();
		_source.Snapshot = _source.Snapshot with { EligibleRecipients = references,
			AudienceSlices = [new(_scope, references[..5000]), new(_scope with { ClassId = Guid.NewGuid() }, references[5000..])] };
		var operation = await _writer.WriteAsync(_input, default); operation.ShouldNotBeNull(); _operations.Count.ShouldBe(1);
		_intents.Count.ShouldBe(101); _intents.ShouldAllBe(x => x.CampaignId == operation.Value);
		_intents.SelectMany(x => JsonSerializer.Deserialize<NotificationRecipientRefDto[]>(x.AffectedRecipientsJson, Json)!).Distinct().Count().ShouldBe(10001);
		_intents.ShouldAllBe(x => x.AffectedRecipientsJson.Length < 65536);
	}
	[Fact]
	public async Task Repeating_a_captured_event_returns_its_operation_before_reloading_or_expanding_the_source()
	{
		var first = await _writer.WriteAsync(_input, default); first.ShouldNotBeNull();
		_rule.Configure(false, _rule.ConfigurationJson, _rule.ConcurrencyStamp);
		_source.Snapshot = _source.Snapshot with { SourceRevision = "another-version" };
		(await _writer.WriteAsync(_input with { OccurredAt = Now.AddMinutes(1) }, default)).ShouldBe(first);
		_operations.Count.ShouldBe(1); _intents.Count.ShouldBe(1); _source.Calls.ShouldBe(1);
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task No_effective_rule_or_a_disabled_narrower_override_creates_no_notification_work(bool disabledOverride)
	{
		if (disabledOverride) _rules.Add(new(Guid.NewGuid(), _tenantId, _input.EventKey,
			NotificationRuleScope.Create(_scope.AcademicYearId, _scope.BoardId, _scope.MediumId, _scope.ClassId)));
		else _rules.Clear();
		(await _writer.WriteAsync(_input, default)).ShouldBeNull(); _operations.ShouldBeEmpty(); _intents.ShouldBeEmpty(); _source.Calls.ShouldBe(1);
	}
	[Theory]
	[InlineData("template")]
	[InlineData("configuration")]
	[InlineData("timezone")]
	public async Task Configuration_problems_persist_a_non_sendable_review_issue_instead_of_failing_publication(string issue)
	{
		if (issue == "template") _versions.Clear();
		if (issue == "configuration") typeof(NotificationRule).GetProperty(nameof(NotificationRule.ConfigurationJson))!.SetValue(_rule, "{}");
		if (issue == "timezone") _settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("Invalid/Zone");
		(await _writer.WriteAsync(_input, default)).ShouldNotBeNull(); _intents.Count.ShouldBe(1);
		_intents[0].Status.ShouldBe(NotificationIntentState.NeedsReview); _intents[0].ErrorCode.ShouldNotBeNullOrWhiteSpace();
	}
	[Theory]
	[InlineData("tenant")]
	[InlineData("unit")]
	[InlineData("transaction")]
	[InlineData("source")]
	[InlineData("ambiguous")]
	[InlineData("revision")]
	[InlineData("identifier")]
	[InlineData("manual")]
	public async Task Invalid_capture_boundaries_fail_before_any_notification_is_persisted(string boundary)
	{
		var input = _input;
		if (boundary == "tenant") _tenant.Id.Returns((Guid?)null);
		if (boundary == "unit") _units.Current.Returns((IUnitOfWork?)null);
		if (boundary == "transaction") _unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = false });
		if (boundary == "source") _sources.Clear();
		if (boundary == "ambiguous") _sources.Add(_source);
		if (boundary == "revision") _source.Snapshot = _source.Snapshot with { SourceRevision = "changed" };
		if (boundary == "identifier") input = input with { SourceId = Guid.Empty };
		if (boundary == "manual") input = input with { EventKey = "Academic.Student360.GuardianMessage", SourceType = "Academic.Student360" };
		await Should.ThrowAsync<BusinessException>(() => _writer.WriteAsync(input, default)); _operations.ShouldBeEmpty(); _intents.ShouldBeEmpty();
	}
	[Fact]
	public async Task Due_occurrences_are_stable_across_scans_but_distinct_windows_can_create_new_work()
	{
		_rules.Clear(); _versions.Clear(); AddRule("Academic.Homework.Due");
		var input = _input with { EventKey = "Academic.Homework.Due", DueAt = Now.AddDays(1) };
		var first = await _writer.WriteAsync(input, default); first.ShouldNotBeNull();
		(await _writer.WriteAsync(input with { OccurredAt = Now.AddMinutes(1) }, default)).ShouldBe(first);
		var second = await _writer.WriteAsync(input with { DueAt = Now.AddDays(2) }, default); second.ShouldNotBeNull(); second.ShouldNotBe(first);
		_operations.Count.ShouldBe(2);
	}
	[Fact]
	public async Task A_quiet_hour_deferral_that_reaches_expiry_is_recorded_as_expired()
	{
		_source.Snapshot = _source.Snapshot with { ExpiresAt = new(2026, 11, 18, 23, 0, 0, TimeSpan.Zero) };
		(await _writer.WriteAsync(_input with { OccurredAt = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero) }, default)).ShouldNotBeNull();
		_intents.Count.ShouldBe(1); _intents[0].Status.ShouldBe(NotificationIntentState.Expired);
	}
	[Fact]
	public async Task Cancellation_does_not_flush_or_start_capture()
	{
		await Should.ThrowAsync<OperationCanceledException>(() => _writer.WriteAsync(_input, new CancellationToken(true)));
		_trace.ShouldBeEmpty(); _operations.ShouldBeEmpty(); _intents.ShouldBeEmpty();
	}
	private void AddRule(string eventKey)
	{
		var version = new CommunicationTemplateRevision(Guid.NewGuid(), _tenantId, Guid.NewGuid(), 1, eventKey, "Guardian", CommunicationChannelType.InApp,
			"en", "Safe title", "Safe body", 1); version.Publish(Guid.NewGuid(), Now); _versions.Add(version);
		_rule = new(Guid.NewGuid(), _tenantId, eventKey, NotificationRuleScope.Create());
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [], [CommunicationChannelType.InApp],
			[version.TemplateId], NotificationRuleTimingConfiguration.Default, "en");
		_rule.Configure(true, _configuration.Serialize(config, _catalogue.Get(eventKey)), _rule.ConcurrencyStamp); _rules.Add(_rule);
	}
	private static IRepository<T, Guid> Repository<T>(List<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>(); repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.ArgAt<T>(0); rows.Add(row); return row;
		});
		return repository;
	}
	private sealed class Source(List<string> trace, NotificationSourceSnapshot snapshot) : INotificationSourceAdapter
	{
		public NotificationSourceSnapshot Snapshot { get; set; } = snapshot;
		public int Calls { get; private set; }
		public IReadOnlyCollection<string> EventKeys => ["Academic.Homework.Published", "Academic.Homework.Due"];
		public Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
		{
			Calls++; trace.Add("source"); return Task.FromResult(Snapshot);
		}
	}
}
