using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class ExamTimetablePublication(IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries,
	INotificationIntentWriter writer, ICurrentTenant tenant, ICancellationTokenProvider cancellation, IClock clock) : ITransientDependency
{
	internal static void MarkReleased(Exam exam) => exam.SetProperty(ExamNotificationSource.PublicationProperty, Guid.NewGuid().ToString("N"));

	internal async Task CaptureAsync(Exam exam, Guid? actor)
	{
		var revision = ExamNotificationSource.PublicationRevision(exam);
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || exam.TenantId != tenantId || revision == null) return;
		var token = cancellation.Token;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == ExamNotificationSource.Published), token)) return;
		await writer.WriteAsync(new(ExamNotificationSource.Published, ExamNotificationSource.Type, exam.Id, revision,
			new DateTimeOffset(clock.Now.ToUniversalTime()), [], actor), token);
	}
}
