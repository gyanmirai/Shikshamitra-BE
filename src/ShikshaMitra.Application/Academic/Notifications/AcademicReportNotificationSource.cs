using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicReportNotificationSource(IRepository<HrReportExportJob, Guid> jobs,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants, ICurrentUser current)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Type = "Academic.Report";
	internal const string Ready = AcademicNotificationEventKeys.ReportExportReady;
	internal const string Failed = AcademicNotificationEventKeys.ReportExportFailed;
	private static readonly NotificationSourceScopeDto SchoolScope = new();
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { Ready, Failed });

	internal static string? TerminalEvent(HrReportExportJob job) => job.Status switch
	{
		HrReportExportStatus.Completed when !string.IsNullOrWhiteSpace(job.BlobName) => Ready,
		HrReportExportStatus.Failed => Failed,
		_ => null
	};

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != Type || !EventKeys.Contains(context.EventKey) ||
			(context.SourceScope != null && context.SourceScope != SchoolScope) ||
			await FindAsync(school, context.SourceId, token) is not { } job || TerminalEvent(job) != context.EventKey ||
			AcademicReportExportStateService.ReadRunId(job) is not { } run || run.ToString("N") != context.SourceRevision)
			return Unavailable();

		var expiresAt = Utc(job.ExpiresAt);
		if (expiresAt <= context.OccurredAt) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName) || job.RequestedByUserId == Guid.Empty) return Unavailable();

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["report_name"] = new(NotificationVariableType.Text, job.ReportType),
			["export_format"] = new(NotificationVariableType.Text, job.Format)
		};
		NotificationRecipientRefDto[] recipients = [new("Requester", UserId: job.RequestedByUserId)];
		var link = context.EventKey == Ready ? $"/api/academic-reports/{job.Id:D}/download" : Link;
		return new(true, null, context.SourceRevision, link, variables, recipients, expiresAt,
			AudienceSlices: [new(SchoolScope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || current.Id is not Guid actor ||
			current.TenantId != school || await FindAsync(school, sourceId, token) is not { } job ||
			job.RequestedByUserId != actor || AcademicReportExportStateService.ReadRunId(job) == null)
			throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => throw new AbpAuthorizationException();

	internal static DateTimeOffset Utc(DateTime timestamp) => new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

	private async Task<HrReportExportJob?> FindAsync(Guid school, Guid id, CancellationToken token) =>
		await queries.SingleOrDefaultAsync((await jobs.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.Id == id && !x.IsDeleted), token);
}
