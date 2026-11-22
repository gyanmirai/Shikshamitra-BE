using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppNotificationChannel_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
	private readonly Guid _tenant = Guid.NewGuid();
	private readonly ChannelProviderSetting _provider;
	private readonly CommunicationTemplateRevision _revision;
	private readonly List<WhatsAppTemplateBinding> _bindings = [];
	private readonly WhatsAppTemplateCompiler _compiler;
	private readonly WhatsAppNotificationChannelSender _sender;
	private readonly ProviderHandler _handler;

	public WhatsAppNotificationChannel_Tests()
	{
		var currentTenant = Substitute.For<ICurrentTenant>();
		currentTenant.Id.Returns(_tenant);
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example",
			["Notifications:WhatsApp:GraphApiVersion"] = "v26.0",
			[$"Notifications:WhatsApp:Tenants:{_tenant:N}:AccessTokens:main"] = "test-secret"
		}).Build();
		_provider = new(Guid.NewGuid(), _tenant) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, Status = CommunicationProviderStatus.Enabled, SecretReference = "main",
			ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1001","businessAccountId":"2001"}"""
		};
		_revision = new(Guid.NewGuid(), _tenant, Guid.NewGuid(), 1, AcademicNotificationEventKeys.HomeworkPublished,
			"Guardian", CommunicationChannelType.WhatsApp, "en-US", "Homework for {{student_name}}",
			"Hello {{recipient_name}}. View {{source_link}}.", 1);
		_revision.Publish(Guid.NewGuid(), Now);
		_compiler = new(new([new AcademicNotificationCatalogue([])], config));
		var queries = new AsyncQueryableExecuter([]);
		var units = Substitute.For<IUnitOfWorkManager>();
		var unit = Substitute.For<IUnitOfWork>();
		var unitActive = false;
		units.Current.Returns(_ => unitActive ? unit : null);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => { unitActive = true; return unit; });
		unit.When(x => x.Dispose()).Do(_ => unitActive = false);
		_handler = new(() => unitActive.ShouldBeFalse());
		var clients = Substitute.For<IHttpClientFactory>();
		clients.CreateClient(WhatsAppProviderClient.HttpClientName).Returns(_ => new HttpClient(_handler, disposeHandler: false));
		_sender = new(units, new(currentTenant, Repository(new[] { _provider }), queries, config),
			new(currentTenant, Repository(_bindings), queries), Repository(new[] { _revision }), queries, _compiler, new(clients));
	}

	[Fact]
	public async Task Sends_only_the_approved_template_for_the_original_provider_account_and_revision()
	{
		var binding = AddBinding(NotificationProviderTemplateState.Approved);

		var result = await _sender.SendAsync(Request(binding.Id), default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		result.ProviderMessageId.ShouldBe("wamid.example");
		_handler.Requests.ShouldBe(1);
		_handler.Address.ShouldBe("https://graph.facebook.com/v26.0/1001/messages");
		using var body = JsonDocument.Parse(_handler.Body!);
		var template = body.RootElement.GetProperty("template");
		template.GetProperty("name").GetString().ShouldBe("academic_homework");
		template.GetProperty("language").GetProperty("code").GetString().ShouldBe("en_US");
		var components = template.GetProperty("components");
		components[0].GetProperty("parameters")[0].GetProperty("text").GetString().ShouldBe("Aarav");
		components[1].GetProperty("parameters")[1].GetProperty("text").GetString().ShouldBe("https://school.example/homework");

		_provider.ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1002","businessAccountId":"2001"}""";
		(await _sender.SendAsync(Request(binding.Id), default)).Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		_handler.Requests.ShouldBe(1);
	}

	[Theory]
	[InlineData(NotificationProviderTemplateState.Draft)]
	[InlineData(NotificationProviderTemplateState.PendingApproval)]
	[InlineData(NotificationProviderTemplateState.Rejected)]
	[InlineData(NotificationProviderTemplateState.Paused)]
	public async Task Blocks_a_binding_without_current_approval(NotificationProviderTemplateState status)
	{
		var binding = AddBinding(status);
		var result = await _sender.SendAsync(Request(binding.Id), default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		_handler.Requests.ShouldBe(0);
	}

	[Fact]
	public async Task Blocks_missing_bindings_and_cross_school_bindings()
	{
		(await _sender.SendAsync(Request(Guid.NewGuid()), default)).Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		var foreign = AddBinding(NotificationProviderTemplateState.Approved, Guid.NewGuid());
		(await _sender.SendAsync(Request(foreign.Id), default)).Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		_handler.Requests.ShouldBe(0);
	}

	private WhatsAppTemplateBinding AddBinding(NotificationProviderTemplateState state, Guid? tenant = null)
	{
		var compiled = _compiler.Compile(_revision);
		var binding = new WhatsAppTemplateBinding(Guid.NewGuid(), tenant ?? _tenant, _revision.Id, _provider.Id,
			"academic_homework", compiled.ProviderLanguage, WhatsAppApprovedTemplates.Encode(compiled, _compiler,
				new("v26.0", "1001", "2001", "test-secret")));
		if (state != NotificationProviderTemplateState.Draft) binding.RecordProviderStatus("3001", state, Now);
		_bindings.Add(binding);
		return binding;
	}

	private NotificationChannelSendRequest Request(Guid bindingId) => new(Guid.NewGuid(), "attempt", _provider.Id,
		CommunicationChannelType.WhatsApp, "+919000000012", null, "Rendered locally",
		"https://school.example/homework", _revision.LanguageCode, 1, bindingId, _revision.Id,
		new("Guardian", Guid.NewGuid()), new Dictionary<string, NotificationVariableValueDto> {
			["student_name"] = new(NotificationVariableType.Text, "Aarav"),
			["recipient_name"] = new(NotificationVariableType.Text, "Parent"),
			["school_name"] = new(NotificationVariableType.Text, "School"),
			["source_link"] = new(NotificationVariableType.Link, "https://school.example/homework")
		});

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}

	private sealed class ProviderHandler(Action checkTransaction) : HttpMessageHandler
	{
		public int Requests { get; private set; }
		public string? Address { get; private set; }
		public string? Body { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			checkTransaction();
			Requests++;
			Address = request.RequestUri?.AbsoluteUri;
			Body = await request.Content!.ReadAsStringAsync(cancellationToken);
			return new(HttpStatusCode.OK) { Content = new StringContent("""{"messages":[{"id":"wamid.example"}]}""") };
		}
	}
}
