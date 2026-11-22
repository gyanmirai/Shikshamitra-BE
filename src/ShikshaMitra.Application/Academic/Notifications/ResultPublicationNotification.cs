using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class ResultPublicationNotification(IRepository<ExamResultAuditLog, Guid> audits,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries, INotificationIntentWriter writer,
	ICurrentTenant tenant, ICancellationTokenProvider cancellation) : ITransientDependency
{
	private const string EventKey = AcademicNotificationEventKeys.ResultPublished;

	internal async Task<string?> RecordAsync(ExamResultHeader header)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || header.TenantId != tenantId) return null;
		var token = cancellation.Token;
		if (await queries.AnyAsync((await audits.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.ExamId == header.ExamId && x.DivisionId == header.DivisionId && x.Action == "Published"), token))
		{
			header.RemoveProperty(ResultNotificationSource.PublicationRevisionProperty);
			return null;
		}
		var revision = Guid.NewGuid().ToString("N");
		header.SetProperty(ResultNotificationSource.PublicationRevisionProperty, revision);
		return revision;
	}

	internal async Task CaptureAsync(ExamResultHeader header, string? revision, Guid actor)
	{
		if (revision == null || header.PublishedOn is not DateTime publishedAt || tenant.Id is not Guid tenantId ||
			header.TenantId != tenantId) return;
		var token = cancellation.Token;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == EventKey), token)) return;
		await writer.WriteAsync(new(EventKey, ResultNotificationSource.Type, header.Id, revision,
			LearningTaskNotificationRecordReader.AsUtc(publishedAt), [], actor), token);
	}
}
