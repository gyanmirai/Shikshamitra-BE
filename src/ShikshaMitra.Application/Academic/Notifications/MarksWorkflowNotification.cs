using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class MarksWorkflowNotification(IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries,
	INotificationIntentWriter writer, INotificationPersonalWriteLock locks, ICurrentTenant tenant, ICancellationTokenProvider cancellation)
	: ITransientDependency
{
	internal Task LockAsync(Guid examId, Guid subjectId, Guid divisionId) => tenant.Id.HasValue
		? locks.AcquireAsync([$"academic-marks-review:{examId:N}:{subjectId:N}:{divisionId:N}"], cancellation.Token)
		: Task.CompletedTask;

	internal async Task CaptureAsync(ExamMarksWorkflowState state)
	{
		if (tenant.Id is not Guid school || state.TenantId != school || state.LastModificationTime is not DateTime changedAt ||
			MarksNotificationSource.EventFor(state.Status) is not { } eventKey) return;
		var token = cancellation.Token;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.IsEnabled && !x.IsDeleted && x.EventKey == eventKey), token)) return;
		await writer.WriteAsync(new(eventKey, MarksNotificationSource.Type, state.Id, MarksNotificationSource.Revision(changedAt),
			LearningTaskNotificationRecordReader.AsUtc(changedAt), [], state.LastModifierId), token);
	}
}
