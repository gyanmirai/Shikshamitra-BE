using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public sealed class AcademicReportNotificationEvents_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Only_the_current_terminal_export_notifies_the_saved_requester_once(bool ready)
	{
		var schoolId = Guid.NewGuid();
		var requester = Guid.NewGuid();
		var jobId = Guid.NewGuid();
		var key = ready ? AcademicNotificationEventKeys.ReportExportReady : AcademicNotificationEventKeys.ReportExportFailed;
		const string privateError = "Synthetic file storage diagnostics";
		using var school = GetRequiredService<ICurrentTenant>().Change(schoolId, "Synthetic school");
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(
				new IdentityUser(requester, "report-owner", "report-owner@example.test", schoolId), autoSave: true);
			await GetRequiredService<IRepository<HrReportExportJob, Guid>>().InsertAsync(new HrReportExportJob(jobId, schoolId,
				requester, AcademicReportTypes.ChapterDescriptionSummary, AcademicReportFormat.Excel, "{}", "{}", DateTime.UtcNow.AddDays(7)),
				autoSave: true);
			var template = new CommunicationTemplate(Guid.NewGuid(), schoolId) { TemplateName = key };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), schoolId, template.Id, 1, key, "Requester",
				CommunicationChannelType.InApp, "en", "Academic export update", "Open the school inbox.", 1);
			revision.Publish(requester, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), schoolId, key, NotificationRuleScope.Create());
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["Requester"], [CommunicationChannelType.InApp], [template.Id],
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>()
				.Serialize(config, GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var units = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		var state = ActivatorUtilities.CreateInstance<AcademicReportExportStateService>(ServiceProvider, units);
		var run = (await state.TryStartAsync(jobId))!.Value;
		var artifact = $"academic-exports/{schoolId}/{requester}/{jobId}/{run:N}.xlsx";
		(await state.TryCompleteAsync(jobId, Guid.NewGuid(), artifact, 1, 10)).ShouldBeFalse();
		await state.TryFailAsync(jobId, Guid.NewGuid(), privateError);
		if (ready)
		{
			(await state.TryCompleteAsync(jobId, run, artifact, 1, 10)).ShouldBeTrue();
			(await state.TryCompleteAsync(jobId, run, artifact, 1, 10)).ShouldBeFalse();
		}
		else
		{
			await state.TryFailAsync(jobId, run, privateError);
			await state.TryFailAsync(jobId, run, privateError);
		}

		await WithUnitOfWorkAsync(async () =>
		{
			var terminal = await GetRequiredService<IRepository<HrReportExportJob, Guid>>().GetAsync(jobId);
			terminal.Status.ShouldBe(ready ? HrReportExportStatus.Completed : HrReportExportStatus.Failed);
			var notice = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == jobId && x.EventKey == key)).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldBe([new("Requester", UserId: requester)]);
			source.Snapshot!.Variables.Values.Select(x => x.Value).ShouldNotContain(artifact);
			source.Snapshot.Variables.Values.Select(x => x.Value).ShouldNotContain(privateError);
			source.Snapshot.RelativeLink.ShouldBe(ready ? $"/api/academic-reports/{jobId:D}/download" : "/dashboard?openNotifications=1");
			var resolver = GetRequiredService<IEnumerable<INotificationRuntimeRecipientResolver>>().Single(x => x.Supports(key));
			(await resolver.ResolveAsync(source, CancellationToken.None)).ShouldHaveSingleItem();
			var guards = GetRequiredService<IEnumerable<IAcademicNotificationSourceAuthorizer>>();
			var guard = guards.Single(x => x.EventKeys.Contains(key));
			using (AsUser(requester))
				await guard.EnsureCanViewAsync(key, jobId, CancellationToken.None);
			using (AsUser(Guid.NewGuid()))
				await Should.ThrowAsync<AbpAuthorizationException>(() => guard.EnsureCanViewAsync(key, jobId, CancellationToken.None));
			var expired = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(notice, new DateTimeOffset(DateTime.SpecifyKind(terminal.ExpiresAt, DateTimeKind.Utc)), CancellationToken.None);
			expired.CurrentRecipients.ShouldBeEmpty();
		});

		IDisposable AsUser(Guid user) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, user.ToString()), new Claim(AbpClaimTypes.TenantId, schoolId.ToString())], "Test")));
	}
}
