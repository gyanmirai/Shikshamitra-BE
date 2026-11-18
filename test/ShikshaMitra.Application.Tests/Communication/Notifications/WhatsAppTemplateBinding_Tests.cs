using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppTemplateBinding_Tests
{
	private const string View = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.View;
	private const string Add = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add;
	private const string Missing = """{"data":[]}""";
	private const string Approved = """
		{"data":[{"id":"3001","name":"academic_homework","language":"en_US","category":"UTILITY","status":"APPROVED",
		"components":[{"type":"BODY","text":"Homework for {{1}}. View {{2}}."}]}]}
		""";
	private readonly IPermissionChecker _permissions = Substitute.For<IPermissionChecker>();
	private readonly INotificationTemplateAccessPolicy _policy = Substitute.For<INotificationTemplateAccessPolicy>();
	private readonly IClock _clock = Substitute.For<IClock>();
	private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
	private readonly List<WhatsAppTemplateBinding> _bindings = [];
	private readonly ChannelProviderSetting _provider;
	private readonly CommunicationTemplate _header;
	private readonly CommunicationTemplateRevision _revision;
	private readonly WhatsAppTemplateBindingAppService _service;
	private readonly HashSet<IUnitOfWork> _activeUnits = [];
	private readonly ProviderHandler _handler;

	public WhatsAppTemplateBinding_Tests()
	{
		var tenantId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		_user.Id.Returns(userId);
		_user.TenantId.Returns(tenantId);
		_user.IsAuthenticated.Returns(true);
		_permissions.IsGrantedAsync(View).Returns(true);
		_permissions.IsGrantedAsync(Add).Returns(true);
		_clock.Now.Returns(new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc));
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example",
			["Notifications:WhatsApp:GraphApiVersion"] = "v26.0",
			[$"Notifications:WhatsApp:Tenants:{tenantId:N}:AccessTokens:main"] = "test-secret"
		}).Build();
		_provider = new(Guid.NewGuid(), tenantId) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, Status = CommunicationProviderStatus.Enabled, SecretReference = "main",
			ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1001","businessAccountId":"2001"}"""
		};
		_header = new(Guid.NewGuid(), tenantId) { AcademicMediumId = Guid.NewGuid() };
		_revision = new(Guid.NewGuid(), tenantId, _header.Id, 1, AcademicNotificationEventKeys.HomeworkPublished, "Guardian",
			CommunicationChannelType.WhatsApp, "en-US", "", "Homework for {{student_name}}. View {{source_link}}.", 1);
		_revision.Publish(userId, _clock.Now);
		_policy.SupportsEvent(_revision.EventKey).Returns(true);
		var validator = new NotificationTemplateVariableValidator([new AcademicNotificationCatalogue([])], config);
		var queries = new AsyncQueryableExecuter([]);
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns(_ => _activeUnits.SingleOrDefault());
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => {
			var unit = Substitute.For<IUnitOfWork>();
			_activeUnits.Add(unit);
			unit.When(x => x.Dispose()).Do(_ => _activeUnits.Remove(unit));
			return unit;
		});
		var filters = Substitute.For<IDataFilter>();
		var bindingRepo = Repository(_bindings);
		bindingRepo.InsertAsync(Arg.Any<WhatsAppTemplateBinding>(), true, Arg.Any<CancellationToken>()).Returns(call => {
			var binding = call.ArgAt<WhatsAppTemplateBinding>(0);
			_bindings.Add(binding);
			return binding;
		});
		var revisions = Repository(new[] { _revision });
		var templates = new NotificationTemplateRevisionService(tenant, _user, Repository(new[] { _header }), revisions, queries,
			SimpleGuidGenerator.Instance, filters, [_policy], validator, new());
		_handler = new(() => _activeUnits.ShouldBeEmpty());
		var clients = Substitute.For<IHttpClientFactory>();
		clients.CreateClient(WhatsAppProviderClient.HttpClientName).Returns(_ => new HttpClient(_handler, disposeHandler: false));
		_service = new(tenant, _user, _permissions, units, new AmbientUnitOfWork(), Substitute.For<INotificationPersonalWriteLock>(), bindingRepo, revisions,
			queries, filters, SimpleGuidGenerator.Instance, _clock, templates, new(tenant, Repository(new[] { _provider }), queries, config),
			new(validator), new(clients), validator, config);
	}

	[Fact]
	public async Task Creation_is_local_and_idempotent_and_requires_both_scopes()
	{
		(await _service.GetAsync(_header.Id, _revision.Id, _provider.Id)).ShouldBeNull();
		var created = await CreateAsync();
		(await CreateAsync()).ShouldBe(created);
		created.ProviderStatus.ShouldBe(NotificationProviderTemplateState.Draft);
		(await _service.GetAsync(_header.Id, _revision.Id, _provider.Id)).ShouldBe(created);
		(await Should.ThrowAsync<BusinessException>(() => CreateAsync("different_name"))).Code.ShouldBe(NotificationErrorCodes.StateConflict);
		_bindings.Count.ShouldBe(1);
		_handler.Requests.ShouldBeEmpty();
		await _policy.Received(3).EnsureAsync(_revision.EventKey, _header.AcademicMediumId, NotificationTemplateAction.Write, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Provider_permission_and_academic_scope_precede_any_graph_call()
	{
		var binding = await CreateAsync();
		_permissions.IsGrantedAsync(View).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.GetAsync(_header.Id, _revision.Id, _provider.Id));
		_permissions.IsGrantedAsync(Add).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.SubmitAsync(binding.Id));
		_permissions.IsGrantedAsync(Add).Returns(true);
		_policy.EnsureAsync(_revision.EventKey, _header.AcademicMediumId, NotificationTemplateAction.Write, Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromException(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.SyncAsync(binding.Id));
		_handler.Requests.ShouldBeEmpty();
	}

	[Fact]
	public async Task Submit_reuses_existing_template_and_requires_lookup_to_confirm_approval()
	{
		var binding = await CreateAsync();
		_handler.Responses.Enqueue(Missing);
		_handler.Responses.Enqueue("""{"id":"3001","status":"APPROVED"}""");
		_handler.Responses.Enqueue(Missing);
		(await _service.SubmitAsync(binding.Id)).ProviderStatus.ShouldBe(NotificationProviderTemplateState.PendingApproval);
		_clock.Now.Returns(_clock.Now.AddSeconds(1));
		_handler.Responses.Enqueue(Approved);
		(await _service.SubmitAsync(binding.Id)).ProviderStatus.ShouldBe(NotificationProviderTemplateState.Approved);
		_handler.Requests.ShouldBe([HttpMethod.Get, HttpMethod.Post, HttpMethod.Get, HttpMethod.Get]);
		var body = _handler.PostBody.ShouldNotBeNull();
		body.ShouldContain("Example student");
		body.ShouldContain("https://school.example/");
	}

	[Fact]
	public async Task An_older_lookup_cannot_overwrite_a_newer_provider_observation()
	{
		var binding = await CreateAsync();
		_handler.Responses.Enqueue(Approved);
		_handler.OnRequest = () => _bindings.Single().RecordProviderStatus("3001", NotificationProviderTemplateState.Paused, _clock.Now.AddSeconds(1));

		(await _service.SyncAsync(binding.Id)).ProviderStatus.ShouldBe(NotificationProviderTemplateState.Paused);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Sync_disables_an_approved_binding_when_meta_removes_or_changes_it(bool changed)
	{
		var binding = await CreateAsync();
		_handler.Responses.Enqueue(Approved);
		(await _service.SyncAsync(binding.Id)).ProviderStatus.ShouldBe(NotificationProviderTemplateState.Approved);
		_clock.Now.Returns(_clock.Now.AddSeconds(1));
		_handler.Responses.Enqueue(changed ? Approved.Replace("Homework", "Classwork", StringComparison.Ordinal) : Missing);
		var synced = await _service.SyncAsync(binding.Id);
		synced.ProviderStatus.ShouldBe(NotificationProviderTemplateState.Disabled);
		synced.ProviderTemplateId.ShouldBe("3001");
		_handler.Requests.ShouldBe([HttpMethod.Get, HttpMethod.Get]);
	}

	[Fact]
	public async Task Changed_sender_account_cannot_sync_an_existing_binding()
	{
		var binding = await CreateAsync();
		_provider.ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1002","businessAccountId":"2001"}""";
		(await Should.ThrowAsync<BusinessException>(() => _service.SyncAsync(binding.Id))).Code.ShouldBe(NotificationErrorCodes.ProviderUnavailable);
		_handler.Requests.ShouldBeEmpty();
	}

	private Task<WhatsAppTemplateBindingDto> CreateAsync(string name = "academic_homework") =>
		_service.CreateAsync(new(_header.Id, _revision.Id, _provider.Id, name));

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}

	private sealed class ProviderHandler(Action checkTransaction) : HttpMessageHandler
	{
		public Queue<string> Responses { get; } = new();
		public List<HttpMethod> Requests { get; } = [];
		public string? PostBody { get; private set; }
		public Action? OnRequest { get; set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			checkTransaction();
			OnRequest?.Invoke();
			Requests.Add(request.Method);
			request.RequestUri!.AbsolutePath.ShouldBe("/v26.0/2001/message_templates");
			if (request.Content != null) PostBody = await request.Content.ReadAsStringAsync(cancellationToken);
			return new(HttpStatusCode.OK) { Content = new StringContent(Responses.Dequeue()) };
		}
	}
}
