using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Emailing.Smtp;
using Volo.Abp.Identity;
using Volo.Abp.MailKit;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicNotificationEndToEndApi_Tests
{
	private const string Root = "/api/academic/notifications";
	private const string Self = "/api/communication/notifications/me";
	private const string Event = AcademicNotificationEventKeys.HomeworkPublished;
	private const string Source = "Academic.Homework";
	private const string TaskTitle = "Practice sheet on fractions";
	private const string PrimaryEmail = "primary-guardian@example.invalid";
	private const string OtherEmail = "other-guardian@example.invalid";
	private const string LearnerEmail = "learner@example.invalid";
	private const string PrimaryPhone = "+15555550101";
	private const string PushEndpoint = "https://push.example.test/fixture/subscription";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);
	private static readonly CommunicationChannelType[] Channels = [CommunicationChannelType.InApp,
		CommunicationChannelType.Email, CommunicationChannelType.WhatsApp, CommunicationChannelType.WebPush];

	[AcademicPerformancePostgresFact]
	public async Task Reviewed_homework_delivers_only_to_selected_accounts_and_records_each_channels_actual_outcome()
	{
		await using var mailbox = new LoopbackSmtpMailbox();
		await using var fixture = new DeliveryApiFactory(mailbox.Configuration);
		using var staff = fixture.CreateClient();
		using (var services = fixture.Services.CreateScope())
			services.ServiceProvider.GetRequiredService<IEmailSender>().ShouldBeAssignableTo<MailKitSmtpEmailSender>();
		staff.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Notification.View, ShikshaMitraSubmodulePermissions.Academics.Notification.Add,
			ShikshaMitraSubmodulePermissions.Academics.Notification.Send,
			ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View));
		var sample = new SchoolSample();
		await InSchoolAsync(fixture, services => SeedHomeworkAsync(fixture, services, sample));
		using var primary = PersonalClient(fixture, sample.Primary, "Guardian");
		using var learner = PersonalClient(fixture, sample.LearnerAccount, "Student");
		using var unrelated = PersonalClient(fixture, sample.UnrelatedGuardian, "Guardian");

		var browserKeys = NotificationSelfPushApiFactory.CreateParameters();
		var subscription = await PostAsync<WebPushSubscriptionDto>(primary, $"{Self}/push/subscriptions",
			new RegisterWebPushSubscriptionDto(Guid.NewGuid(), PushEndpoint,
				new(NotificationSelfPushApiFactory.PublicKey(browserKeys),
					NotificationSelfPushApiFactory.Base64(RandomNumberGenerator.GetBytes(16))), DeliveryApiFactory.PushVersion));
		subscription.IsEnabled.ShouldBeTrue();

		await PostAsync<int>(staff, $"{Root}/templates/initialize-defaults", new { eventKey = Event });
		var templates = await staff.GetFromJsonAsync<PagedResultDto<NotificationTemplateDto>>($"{Root}/templates?eventKey={Event}&maxResultCount=100");
		var published = new List<NotificationTemplateDto>();
		foreach (var item in templates!.Items.Where(x =>
			(x.RecipientKind == "Guardian" && Channels.Contains(x.Channel)) ||
			(x.RecipientKind == "StudentAccount" && x.Channel is CommunicationChannelType.InApp or CommunicationChannelType.Email)))
		{
			var draft = item.Channel is CommunicationChannelType.InApp or CommunicationChannelType.Email
				? await SaveTemplateBodyAsync(staff, item)
				: item;
			published.Add(await PostAsync<NotificationTemplateDto>(staff, $"{Root}/templates/{draft.TemplateId}/publish",
				new NotificationTemplateRevisionCommandDto(draft.RevisionId, draft.ConcurrencyStamp)));
		}
		published.Count.ShouldBe(6);
		var guardianTemplates = published.Where(x => x.RecipientKind == "Guardian").Select(x => x.TemplateId).ToArray();
		await InSchoolAsync(fixture, services => LinkProviderTemplateAsync(services,
			published.Single(x => x.Channel == CommunicationChannelType.WhatsApp).RevisionId));
		var scope = new AcademicNotificationScopeDto(fixture.SeededAcademicYearId, fixture.SeededBoardId,
			fixture.SeededMediumId, fixture.SeededStandardId, sample.Division);
		var firstRule = await SaveRuleAsync(staff, new()
		{
			EventKey = Event, Scope = scope, Enabled = true, Channels = Channels, TemplateIds = guardianTemplates,
			QuietHours = new(false)
		});
		firstRule.Configuration!.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
		firstRule.Configuration.IncludeStudentAccount.ShouldBeFalse();

		var request = new PreviewAcademicNotificationManualDto(Event, Source, sample.Homework, [new("Student", sample.Learner)], Channels);
		var beforeConsent = await PostAsync<AcademicNotificationManualPreviewDto>(staff, $"{Root}/manual/preview", request);
		beforeConsent.Channels.Single(x => x.Channel == CommunicationChannelType.WhatsApp).Exclusions
			.ShouldContain(x => x.Code == "ConsentRequired" && x.Count == 1);
		await InSchoolAsync(fixture, async services =>
		{
			await InsertAsync(services, new ConsentRecord(Guid.NewGuid(), School)
			{
				RecipientType = "ParentGuardian", RecipientId = sample.Learner, Channel = CommunicationChannelType.WhatsApp,
				Destination = PrimaryPhone, Status = CommunicationConsentStatus.OptedIn, ConsentCapturedAt = DateTimeOffset.UtcNow
			});
		});
		var review = await PostAsync<AcademicNotificationManualPreviewDto>(staff, $"{Root}/manual/preview", request);
		review.EligibleRecipientCount.ShouldBe(1);
		review.Channels.Count.ShouldBe(Channels.Length);
		review.Channels.ShouldAllBe(x => x.ReadyDeliveryCount == 1);
		review.Samples.ShouldNotBeEmpty();
		review.Samples.ShouldAllBe(x => x.IsSynthetic && x.Rendered.Body != null && !x.Rendered.Body.Contains(TaskTitle, StringComparison.Ordinal));
		(await InboxAsync(primary)).Items.ShouldBeEmpty();
		var confirmation = new ConfirmAcademicNotificationManualDto(review.ReviewToken!.Value, "acceptance-primary");
		var operation = await PostAsync<AcademicNotificationOperationDto>(staff, $"{Root}/manual/send", confirmation, HttpStatusCode.Accepted);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Queued);
		var replay = await PostAsync<AcademicNotificationOperationDto>(staff, $"{Root}/manual/send", confirmation, HttpStatusCode.Accepted);
		replay.OperationId.ShouldBe(operation.OperationId);
		fixture.WhatsApp.Requests.ShouldBeEmpty();
		fixture.Push.Requests.ShouldBeEmpty();
		mailbox.Accepted.ShouldBeEmpty();
		(await InboxAsync(primary)).Items.ShouldBeEmpty();

		await RunQueueAsync(fixture);
		var history = await HistoryAsync(staff, sample.Homework, operation.OperationId);
		history.Count.ShouldBe(Channels.Length);
		history.ShouldAllBe(x => x.RecipientUserId == sample.Primary && x.RecipientKind == "Guardian");
		foreach (var channel in Channels)
		{
			var item = history.Single(x => x.Channel == channel);
			item.AttemptNumber.ShouldBe(1);
			item.Status.ShouldBe(channel == CommunicationChannelType.InApp
				? NotificationDispatchState.Delivered : NotificationDispatchState.ProviderAccepted);
		}
		var firstMail = mailbox.Accepted.ShouldHaveSingleItem();
		firstMail.Recipient.ShouldBe(PrimaryEmail);
		firstMail.Message.From.Mailboxes.Single().Address.ShouldBe(LoopbackSmtpMailbox.FromAddress);
		firstMail.Message.To.Mailboxes.Single().Address.ShouldBe(PrimaryEmail);
		firstMail.Message.Subject.ShouldNotBeNullOrEmpty();
		firstMail.Message.HtmlBody.ShouldContain(TaskTitle);
		firstMail.Message.HtmlBody.ShouldContain("https://school.example.invalid/");
		var whatsapp = fixture.WhatsApp.Requests.ShouldHaveSingleItem();
		using (var payload = JsonDocument.Parse(whatsapp.Body))
		{
			payload.RootElement.GetProperty("to").GetString().ShouldBe(PrimaryPhone.TrimStart('+'));
			payload.RootElement.GetProperty("type").GetString().ShouldBe("template");
			payload.RootElement.GetProperty("template").GetProperty("name").GetString().ShouldBe(DeliveryApiFactory.TemplateName);
			payload.RootElement.GetProperty("template").GetProperty("components").GetArrayLength().ShouldBeGreaterThan(0);
		}
		var push = fixture.Push.Requests.ShouldHaveSingleItem();
		push.Endpoint.ShouldBe(new Uri(PushEndpoint));
		push.ContentEncoding.ShouldBe("aes128gcm");
		push.Body.Length.ShouldBeGreaterThan(0);
		var firstInbox = (await InboxAsync(primary)).Items.ShouldHaveSingleItem();
		firstInbox.Body.ShouldContain(TaskTitle);
		(await InboxAsync(learner)).Items.ShouldBeEmpty();
		(await InboxAsync(unrelated)).Items.ShouldBeEmpty();

		var deliveredAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		await SendWhatsAppReceiptAsync(fixture, deliveredAt);
		await SendWhatsAppReceiptAsync(fixture, deliveredAt);
		await RunQueueAsync(fixture);
		var afterReceipt = await HistoryAsync(staff, sample.Homework, operation.OperationId);
		afterReceipt.Count.ShouldBe(Channels.Length);
		afterReceipt.Single(x => x.Channel == CommunicationChannelType.WhatsApp).Status.ShouldBe(NotificationDispatchState.Delivered);
		afterReceipt.Single(x => x.Channel == CommunicationChannelType.Email).Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
		afterReceipt.Single(x => x.Channel == CommunicationChannelType.WebPush).Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
		fixture.WhatsApp.Requests.Count.ShouldBe(1);
		fixture.Push.Requests.Count.ShouldBe(1);

		CommunicationChannelType[] familyChannels = [CommunicationChannelType.InApp, CommunicationChannelType.Email];
		await SaveRuleAsync(staff, new()
		{
			EventKey = Event, Scope = scope, Enabled = true, ConcurrencyStamp = firstRule.ConcurrencyStamp,
			GuardianMode = NotificationGuardianMode.PrimaryAndSelected, AdditionalGuardianRelationships = ["father"],
			IncludeStudentAccount = true, Channels = familyChannels,
			TemplateIds = published.Where(x => familyChannels.Contains(x.Channel)).Select(x => x.TemplateId).ToArray(),
			QuietHours = new(false)
		});
		var wider = await PostAsync<AcademicNotificationManualPreviewDto>(staff, $"{Root}/manual/preview",
			request with { Channels = familyChannels });
		wider.EligibleRecipientCount.ShouldBe(3);
		var inboxChannel = wider.Channels.Single(x => x.Channel == CommunicationChannelType.InApp);
		inboxChannel.ReadyDeliveryCount.ShouldBe(2);
		inboxChannel.Exclusions.ShouldContain(x => x.Code == "VerifiedAccountRequired" && x.Count == 1);
		var secondOperation = await PostAsync<AcademicNotificationOperationDto>(staff, $"{Root}/manual/send",
			new ConfirmAcademicNotificationManualDto(wider.ReviewToken!.Value, "acceptance-family"), HttpStatusCode.Accepted);
		await RunQueueAsync(fixture);
		var widerHistory = await HistoryAsync(staff, sample.Homework, secondOperation.OperationId);
		widerHistory.Where(x => x.Channel == CommunicationChannelType.InApp && x.Status == NotificationDispatchState.Delivered)
			.Select(x => x.RecipientUserId).ShouldBe([(Guid?)sample.Primary, sample.LearnerAccount], ignoreOrder: true);
		widerHistory.Count(x => x.Channel == CommunicationChannelType.Email && x.Status == NotificationDispatchState.ProviderAccepted).ShouldBe(3);
		mailbox.Accepted.Skip(1).Select(x => x.Recipient).ShouldBe([PrimaryEmail, OtherEmail, LearnerEmail], ignoreOrder: true);
		mailbox.Accepted.ShouldAllBe(x => x.Message.HtmlBody!.Contains(TaskTitle, StringComparison.Ordinal));
		(await InboxAsync(learner)).Items.ShouldHaveSingleItem().Body.ShouldContain(TaskTitle);
		(await InboxAsync(unrelated)).Items.ShouldBeEmpty();

		mailbox.RecipientRejections[OtherEmail] = "550 Mailbox unavailable";
		var rejectionReview = await PostAsync<AcademicNotificationManualPreviewDto>(staff, $"{Root}/manual/preview",
			request with { Channels = familyChannels });
		var rejectedOperation = await PostAsync<AcademicNotificationOperationDto>(staff, $"{Root}/manual/send",
			new ConfirmAcademicNotificationManualDto(rejectionReview.ReviewToken!.Value, "acceptance-smtp-rejection"), HttpStatusCode.Accepted);
		await RunQueueAsync(fixture);
		var rejectionHistory = await HistoryAsync(staff, sample.Homework, rejectedOperation.OperationId);
		rejectionHistory.Count(x => x.Channel == CommunicationChannelType.InApp && x.Status == NotificationDispatchState.Delivered)
			.ShouldBe(2);
		var emailHistory = rejectionHistory.Where(x => x.Channel == CommunicationChannelType.Email).ToArray();
		emailHistory.Count(x => x.Status == NotificationDispatchState.ProviderAccepted).ShouldBe(2);
		var rejected = emailHistory.Single(x => x.Status == NotificationDispatchState.Failed);
		rejected.ReasonCode.ShouldBe("SmtpPermanentRejection");
		rejected.CanRequestRetry.ShouldBeFalse();
		mailbox.Accepted.Skip(4).Select(x => x.Recipient).ShouldBe([PrimaryEmail, LearnerEmail], ignoreOrder: true);
	}

	private static async Task SeedHomeworkAsync(DeliveryApiFactory fixture, IServiceProvider services, SchoolSample sample)
	{
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(fixture.SeededStandardId, "Delivery test division", 30);
		sample.Division = (await InsertAsync(services, division)).Id;
		var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Fractions", "#224466", fixture.SeededMediumId));
		await InsertAsync(services, new AssignedSubject(Guid.NewGuid(), School, fixture.SeededStandardId, subject.Id, "TEST", SubjectStatus.Published));
		var type = await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(fixture.SeededMediumId, "Delivery test homework");
		var homework = new HomeworkAssignmentEntity(sample.Homework, fixture.SeededStandardId, subject.Id, TaskTitle, "",
			type.Id, SubmissionType.OnlineText, School, educationMediumId: fixture.SeededMediumId);
		homework.AddDivisionSchedule(new(Guid.NewGuid(), homework.Id, division.Id, null, DateTime.UtcNow.AddDays(3), School));
		homework.PublishAll();
		await InsertAsync(services, homework);
		var learner = await CreateLearnerAsync(sample.Learner, "River", PrimaryEmail, PrimaryPhone);
		await CreateAccountAsync(learner.Id, sample.Primary, "guardian", "mother", PrimaryEmail);
		await CreateAccountAsync(learner.Id, sample.LearnerAccount, "student", "self", LearnerEmail);
		var sibling = await CreateLearnerAsync(Guid.NewGuid(), "Unselected", "unselected@example.invalid", null);
		await CreateAccountAsync(sibling.Id, sample.UnrelatedGuardian, "guardian", "mother", "unselected@example.invalid");
		await InsertAsync(services, new ChannelProviderSetting(Guid.NewGuid(), School)
		{
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, IsDefault = true,
			ConsentRequired = false, Status = CommunicationProviderStatus.Enabled
		});
		await InsertAsync(services, new ChannelProviderSetting(DeliveryApiFactory.ProviderId, School)
		{
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, IsDefault = true, ConsentRequired = true, Status = CommunicationProviderStatus.Enabled,
			SecretReference = "fixture", ConfigurationJson = NotificationRuntimeJson.Serialize(
				new WhatsAppProviderAccount(1, DeliveryApiFactory.PhoneId, DeliveryApiFactory.BusinessId), NotificationErrorCodes.ProviderUnavailable)
		});

		async Task<Student> CreateLearnerAsync(Guid id, string lastName, string primaryEmail, string? primaryPhone)
		{
			var student = new Student(id, School, "Sample", null, lastName, fixture.SeededAcademicYearId, fixture.SeededBoardId,
				fixture.SeededMediumId, fixture.SeededMediumStageId, fixture.SeededStandardId, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			student.UpdateFamilyInfo(new("Other guardian", null, null, OtherEmail, null, null, null, null, null,
				null, null, null, null), new("Primary guardian", null, null, primaryEmail, primaryPhone, null, null, null, null,
				null, null, null, null), null, []);
			StudentFamilyContactSelection.Select(student, "mother");
			await InsertAsync(services, student);
			await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, id, fixture.SeededAcademicYearId,
				fixture.SeededStandardId, division.Id));
			return student;
		}

		async Task CreateAccountAsync(Guid studentId, Guid userId, string type, string relationship, string email)
		{
			var user = new IdentityUser(userId, $"delivery-{userId:N}", email, School);
			user.SetEmailConfirmed(true);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(user, autoSave: true);
			await InsertAsync(services, new StudentPortalAccountLink(Guid.NewGuid(), School, studentId, userId, type, relationship, Actor));
		}
	}

	private static async Task LinkProviderTemplateAsync(IServiceProvider services, Guid revisionId)
	{
		var revision = await services.GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().GetAsync(revisionId);
		var compiler = services.GetRequiredService<WhatsAppTemplateCompiler>();
		var compiled = compiler.Compile(revision);
		var connection = new WhatsAppProviderConnection(DeliveryApiFactory.GraphVersion, DeliveryApiFactory.PhoneId,
			DeliveryApiFactory.BusinessId, DeliveryApiFactory.AccessToken);
		var binding = new WhatsAppTemplateBinding(Guid.NewGuid(), School, revisionId, DeliveryApiFactory.ProviderId,
			DeliveryApiFactory.TemplateName, compiled.ProviderLanguage, WhatsAppApprovedTemplates.Encode(compiled, compiler, connection));
		binding.RecordProviderStatus("3001", NotificationProviderTemplateState.Approved, DateTimeOffset.UtcNow);
		await InsertAsync(services, binding);
	}

	private static async Task SendWhatsAppReceiptAsync(DeliveryApiFactory fixture, long deliveredAt)
	{
		using var client = fixture.CreateClient();
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		var body = JsonSerializer.SerializeToUtf8Bytes(new
		{
			@object = "whatsapp_business_account",
			entry = new[] { new
			{
				id = DeliveryApiFactory.BusinessId,
				changes = new[] { new
				{
					field = "messages",
					value = new
					{
						messaging_product = "whatsapp", metadata = new { phone_number_id = DeliveryApiFactory.PhoneId },
						statuses = new[] { new { id = DeliveryApiFactory.MessageId, status = "delivered",
							timestamp = deliveredAt.ToString(CultureInfo.InvariantCulture) } }
					}
				} }
			} }
		});
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/communication/notifications/providers/whatsapp/webhook")
		{
			Content = new ByteArrayContent(body)
		};
		request.Headers.Add("X-Hub-Signature-256", "sha256=" +
			Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(DeliveryApiFactory.WebhookSecret), body)));
		using var response = await client.SendAsync(request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	private static async Task RunQueueAsync(DeliveryApiFactory fixture)
	{
		using var scope = fixture.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School);
		await services.GetRequiredService<NotificationTenantQueueRunner>().RunAsync(20, CancellationToken.None);
	}

	private static async Task InSchoolAsync(DeliveryApiFactory fixture, Func<IServiceProvider, Task> action)
	{
		using var scope = fixture.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}

	private static Task<T> InsertAsync<T>(IServiceProvider services, T entity) where T : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);

	private static HttpClient PersonalClient(DeliveryApiFactory fixture, Guid id, string role)
	{
		var client = fixture.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, role);
		return client;
	}

	private static async Task<T> PostAsync<T>(HttpClient client, string route, object body, HttpStatusCode expected = HttpStatusCode.OK)
	{
		using var response = await client.PostAsJsonAsync(route, body);
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	private static async Task<AcademicNotificationRuleDto> SaveRuleAsync(HttpClient staff, SaveAcademicNotificationRuleDto request)
	{
		using var response = await staff.PutAsJsonAsync($"{Root}/rules/{Event}", request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AcademicNotificationRuleDto>())!;
	}

	private static async Task<NotificationTemplateDto> SaveTemplateBodyAsync(HttpClient staff, NotificationTemplateDto template)
	{
		var body = template.Body + (template.Channel == CommunicationChannelType.Email
			? "<p>Homework: {{task_title}}.</p>" : "\nHomework: {{task_title}}.");
		using var response = await staff.PutAsJsonAsync($"{Root}/templates/{template.TemplateId}/draft",
			new UpdateNotificationTemplateDraftDto(template.RevisionId, template.Subject, body, template.ConcurrencyStamp));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<NotificationTemplateDto>())!;
	}

	private static async Task<IReadOnlyList<AcademicNotificationHistoryItemDto>> HistoryAsync(HttpClient staff, Guid sourceId, Guid operationId) =>
		(await staff.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(
			$"{Root}/history?eventKey={Event}&sourceType={Source}&sourceId={sourceId}&operationId={operationId}&maxResultCount=20"))!.Items;

	private static async Task<PagedResultDto<NotificationInboxItemDto>> InboxAsync(HttpClient personal) =>
		(await personal.GetFromJsonAsync<PagedResultDto<NotificationInboxItemDto>>($"{Self}/inbox"))!;

	private sealed class SchoolSample
	{
		public Guid Division { get; set; }
		public Guid Homework { get; } = Guid.NewGuid();
		public Guid Learner { get; } = Guid.NewGuid();
		public Guid LearnerAccount { get; } = Guid.NewGuid();
		public Guid Primary { get; } = Guid.NewGuid();
		public Guid UnrelatedGuardian { get; } = Guid.NewGuid();
	}

	private sealed class DeliveryApiFactory(ISmtpEmailSenderConfiguration smtpConfiguration) : AcademicPerformancePostgresTestFactory
	{
		public static readonly Guid ProviderId = Guid.NewGuid();
		public const string BusinessId = "1001";
		public const string PhoneId = "2001";
		public const string TemplateName = "fixture_academic_homework";
		public const string GraphVersion = "v26.0";
		public const string AccessToken = "fixture-access-token";
		public const string WebhookSecret = "fixture-webhook-secret";
		public const string MessageId = "wamid.fixture-accepted";
		public const string PushVersion = "fixture-v1";
		public CaptureTransport WhatsApp { get; } = new(() => new(HttpStatusCode.OK)
		{
			Content = JsonContent.Create(new { messages = new[] { new { id = MessageId } } })
		});
		public CaptureTransport Push { get; } = new(() => new(HttpStatusCode.Created));

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			var pair = NotificationSelfPushApiFactory.CreateParameters();
			builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["App:ClientUrl"] = "https://school.example.invalid",
				["Notifications:WhatsApp:GraphApiVersion"] = GraphVersion,
				[$"Notifications:WhatsApp:Tenants:{School:N}:AccessTokens:fixture"] = AccessToken,
				["Notifications:WhatsApp:Webhook:AppSecret"] = WebhookSecret,
				[$"Notifications:WhatsApp:Webhook:Accounts:{BusinessId}:{PhoneId}:TenantId"] = School.ToString(),
				[$"Notifications:WhatsApp:Webhook:Accounts:{BusinessId}:{PhoneId}:ProviderSettingId"] = ProviderId.ToString(),
				["Notifications:WebPush:PublicKey"] = NotificationSelfPushApiFactory.PublicKey(pair),
				["Notifications:WebPush:PrivateKey"] = NotificationSelfPushApiFactory.Base64(pair.D!),
				["Notifications:WebPush:KeyVersion"] = PushVersion,
				["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test"
			}));
			builder.ConfigureTestServices(services =>
			{
				services.Replace(ServiceDescriptor.Singleton(smtpConfiguration));
				services.PostConfigure<AbpMailKitOptions>(options => options.SecureSocketOption = SecureSocketOptions.None);
				services.AddHttpClient(WhatsAppProviderClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => WhatsApp);
				services.RemoveAll<IWebPushEndpointClientFactory>();
				services.AddSingleton<IWebPushEndpointClientFactory>(new PushCaptureFactory(Push));
				services.RemoveAll<IWebPushEndpointDnsResolver>();
				services.AddSingleton<IWebPushEndpointDnsResolver, NotificationTestPushDns>();
			});
		}
	}

	private sealed class PushCaptureFactory(CaptureTransport transport) : IWebPushEndpointClientFactory
	{
		public HttpClient Create(ValidatedWebPushEndpoint endpoint) => new(transport, disposeHandler: false);
	}

	private sealed record CapturedRequest(Uri Endpoint, byte[] Body, string ContentEncoding);

	private sealed class CaptureTransport(Func<HttpResponseMessage> reply) : HttpMessageHandler
	{
		public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
			Requests.Enqueue(new(request.RequestUri!, body, string.Join(",", request.Content.Headers.ContentEncoding)));
			return reply();
		}
	}
}
