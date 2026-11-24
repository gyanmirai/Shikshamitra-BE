using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationEndToEndApi_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task The_staging_pilot_delivers_one_reviewed_in_app_notice_and_rolls_back_a_recipient_mismatch()
	{
		await using var mailbox = new LoopbackSmtpMailbox();
		await using var fixture = new DeliveryApiFactory(mailbox.Configuration);
		using var staff = fixture.CreateClient();
		staff.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Notification.View, ShikshaMitraSubmodulePermissions.Academics.Notification.Add,
			ShikshaMitraSubmodulePermissions.Academics.Notification.Send,
			ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View));
		var sample = new SchoolSample();
		await InSchoolAsync(fixture, services => SeedHomeworkAsync(fixture, services, sample, useDemoAccounts: true));
		using var primary = PersonalClient(fixture, sample.Primary, "Guardian");
		using var learner = PersonalClient(fixture, sample.LearnerAccount, "Student");

		await PostAsync<int>(staff, $"{Root}/templates/initialize-defaults", new { eventKey = Event });
		var templates = (await staff.GetFromJsonAsync<PagedResultDto<NotificationTemplateDto>>(
			$"{Root}/templates?eventKey={Event}&maxResultCount=100")).ShouldNotBeNull();
		var draft = await SaveTemplateBodyAsync(staff, templates.Items.Single(x => x.RecipientKind == "Guardian" &&
			x.Channel == CommunicationChannelType.InApp));
		await PostAsync<NotificationTemplateDto>(staff, $"{Root}/templates/{draft.TemplateId}/publish",
			new NotificationTemplateRevisionCommandDto(draft.RevisionId, draft.ConcurrencyStamp));
		CommunicationChannelType[] channels = [CommunicationChannelType.InApp];
		var scope = new AcademicNotificationScopeDto(fixture.SeededAcademicYearId, fixture.SeededBoardId,
			fixture.SeededMediumId, fixture.SeededStandardId, sample.Division);
		await SaveRuleAsync(staff, new()
		{
			EventKey = Event, Scope = scope, Enabled = true, Channels = channels, TemplateIds = [draft.TemplateId],
			QuietHours = new(false)
		});

		var configuration = fixture.Services.GetRequiredService<IConfiguration>();
		configuration["Notifications:QueueWorker:Enabled"] = "false";
		var operation = await QueueAsync("staging-selected");
		var unrelated = await QueueAsync("staging-unselected");
		var pilot = new StagingInAppNotificationPilot(School, operation.OperationId, sample.Primary, sample.Homework);

		fixture.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Staging;
		await RunPilotAsync(pilot with { RecipientUserId = sample.LearnerAccount });
		(await InboxAsync(primary)).Items.ShouldBeEmpty();
		(await InboxAsync(learner)).Items.ShouldBeEmpty();
		(await HistoryAsync(staff, sample.Homework, operation.OperationId)).ShouldBeEmpty();
		await InSchoolAsync(fixture, async services =>
		{
			var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.CampaignId == operation.OperationId);
			intents.ShouldHaveSingleItem().Status.ShouldBe(NotificationIntentState.Ready);
		});

		await RunPilotAsync(pilot);
		var projected = await staff.GetFromJsonAsync<AcademicNotificationOperationStatusDto>($"{Root}/operations/{operation.OperationId}");
		projected.ShouldNotBeNull().Status.ShouldBe(CommunicationCampaignStatus.Sent);
		await RunPilotAsync(pilot);
		var delivered = (await HistoryAsync(staff, sample.Homework, operation.OperationId)).ShouldHaveSingleItem();
		delivered.Status.ShouldBe(NotificationDispatchState.Delivered);
		delivered.Channel.ShouldBe(CommunicationChannelType.InApp);
		delivered.RecipientUserId.ShouldBe(sample.Primary);
		delivered.AttemptNumber.ShouldBe(1);
		(await InboxAsync(primary)).Items.ShouldHaveSingleItem().Body.ShouldContain(TaskTitle);
		(await InboxAsync(learner)).Items.ShouldBeEmpty();
		(await HistoryAsync(staff, sample.Homework, unrelated.OperationId)).ShouldBeEmpty();
		mailbox.Accepted.ShouldBeEmpty();
		fixture.WhatsApp.Requests.ShouldBeEmpty();
		fixture.Push.Requests.ShouldBeEmpty();

		async Task<AcademicNotificationOperationDto> QueueAsync(string idempotencyKey)
		{
			var request = new PreviewAcademicNotificationManualDto(Event, Source, sample.Homework, [new("Student", sample.Learner)], channels);
			var preview = await PostAsync<AcademicNotificationManualPreviewDto>(staff, $"{Root}/manual/preview", request);
			preview.IsQueueWorkerEnabled.ShouldBeFalse();
			preview.EligibleRecipientCount.ShouldBe(1);
			var queued = await PostAsync<AcademicNotificationOperationDto>(staff, $"{Root}/manual/send",
				new ConfirmAcademicNotificationManualDto(preview.ReviewToken.ShouldNotBeNull(), idempotencyKey), HttpStatusCode.Accepted);
			queued.Status.ShouldBe(CommunicationCampaignStatus.Queued);
			return queued;
		}

		async Task RunPilotAsync(StagingInAppNotificationPilot selected)
		{
			const string prefix = "Notifications:QueueWorker:StagingInAppPilot:";
			configuration[prefix + "TenantId"] = selected.TenantId.ToString();
			configuration[prefix + "OperationId"] = selected.OperationId.ToString();
			configuration[prefix + "SourceId"] = selected.SourceId.ToString();
			configuration[prefix + "RecipientUserId"] = selected.RecipientUserId.ToString();
			using var worker = fixture.Services.CreateScope();
			using var tenant = worker.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(School);
			await worker.ServiceProvider.GetRequiredService<StagingInAppNotificationPilotRunner>().RunAsync(selected, CancellationToken.None);
		}
	}
}
