using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Json;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationEndToEndApi_Tests
{
	private static async Task VerifyReportExportDeliveryAsync(DeliveryApiFactory fixture, HttpClient owner, SchoolSample sample)
	{
		const string route = "/api/app/academic-report/exports";
		const string eventKey = AcademicNotificationEventKeys.ReportExportReady;
		var template = await PublishInboxTemplateAsync(owner, eventKey, "Requester");
		await SaveRuleAsync(owner, new()
		{
			EventKey = eventKey, Scope = new(), Enabled = true, StaffAudienceSelectors = ["Requester"],
			TemplateIds = [template.TemplateId], QuietHours = new(false)
		});

		var export = await PostAsync<AcademicReportExportCreateResponseDto>(owner, route, new AcademicReportRequestDto
		{
			ReportType = AcademicReportTypes.ChapterDescriptionSummary,
			Filters = new() { AcademicYearId = fixture.SeededAcademicYearId, ClassIds = [fixture.SeededStandardId] }
		});
		export.Status.ShouldBe(HrReportExportStatus.Queued);
		var jobRoute = $"{route}/{export.JobId:D}";
		var link = $"/api/academic-reports/{export.JobId:D}/download";
		var historyRoute = $"{Root}/history?eventKey={eventKey}&sourceType=Academic.Report&sourceId={export.JobId:D}&maxResultCount=20";
		(await InboxAsync(owner)).Items.ShouldNotContain(x => x.RelativeLink == link);

		await RunSavedExportAsync(fixture, export.JobId);
		(await owner.GetFromJsonAsync<AcademicReportExportJobDto>(jobRoute))!.Status.ShouldBe(HrReportExportStatus.Completed);
		(await InboxAsync(owner)).Items.ShouldNotContain(x => x.RelativeLink == link);
		await RunQueueAsync(fixture);
		var first = (await InboxAsync(owner)).Items.Where(x => x.RelativeLink == link).ShouldHaveSingleItem();
		first.Body.ShouldContain("Your academic report export is ready.");
		await CheckDownloadAsync();
		var delivery = (await owner.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(historyRoute))!
			.Items.ShouldHaveSingleItem();
		delivery.RecipientUserId.ShouldBe(Actor);
		delivery.Status.ShouldBe(NotificationDispatchState.Delivered);

		using var other = PersonalClient(fixture, sample.Primary, "Guardian");
		other.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Notification.View,
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add));
		(await InboxAsync(other)).Items.ShouldNotContain(x => x.RelativeLink == link);
		using var otherHistory = await other.GetAsync(historyRoute);
		otherHistory.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherRecovery = await other.GetAsync(jobRoute + "/recovery");
		otherRecovery.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherDownload = await other.GetAsync(jobRoute + "/download");
		otherDownload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		await InSchoolAsync(fixture, async services =>
		{
			var saved = await services.GetRequiredService<IRepository<HrReportExportJob, Guid>>().GetAsync(export.JobId);
			(await services.GetRequiredService<IBlobContainer>().DeleteAsync(saved.BlobName!)).ShouldBeTrue();
		});
		var recovery = (await owner.GetFromJsonAsync<AcademicReportRecoveryDto>(jobRoute + "/recovery"))!;
		recovery.Status.ShouldBe("Unavailable");
		recovery.CanRegenerate.ShouldBeTrue();
		var replacement = await PostAsync<AcademicReportExportCreateResponseDto>(owner, jobRoute + "/regenerate", new { });
		replacement.JobId.ShouldBe(export.JobId);
		replacement.Status.ShouldBe(HrReportExportStatus.Queued);
		(await PostAsync<AcademicReportExportCreateResponseDto>(owner, jobRoute + "/regenerate", new { }))
			.JobId.ShouldBe(export.JobId);
		await RunSavedExportAsync(fixture, export.JobId);
		await RunQueueAsync(fixture);
		await CheckDownloadAsync();
		var deliveries = (await owner.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(historyRoute))!.Items;
		deliveries.Count.ShouldBe(2);
		deliveries.ShouldAllBe(x => x.Status == NotificationDispatchState.Delivered && x.RecipientUserId == Actor);
		(await InboxAsync(other)).Items.ShouldNotContain(x => x.RelativeLink == link);

		async Task CheckDownloadAsync()
		{
			using var response = await owner.GetAsync(jobRoute + "/download");
			response.StatusCode.ShouldBe(HttpStatusCode.OK, response.ReasonPhrase);
			response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
			using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
			using var workbook = new ZipArchive(stream, ZipArchiveMode.Read);
			workbook.GetEntry("xl/workbook.xml").ShouldNotBeNull();
		}
	}

	private static async Task RunSavedExportAsync(DeliveryApiFactory fixture, Guid exportId)
	{
		using var scope = fixture.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(School);
		var store = services.GetRequiredService<IBackgroundJobStore>();
		var job = (await store.GetWaitingJobsAsync(1000)).Single(x => x.JobArgs.Contains(exportId.ToString("D"), StringComparison.OrdinalIgnoreCase));
		var args = services.GetRequiredService<IJsonSerializer>().Deserialize<AcademicReportExportJobArgs>(job.JobArgs);
		args.JobId.ShouldBe(exportId);
		await services.GetRequiredService<AcademicReportExportBackgroundJob>().ExecuteAsync(args);
		await store.DeleteAsync(job.Id);
	}
}
