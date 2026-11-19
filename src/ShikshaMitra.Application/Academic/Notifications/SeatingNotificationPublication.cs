using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
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

internal sealed record SeatingNoticeOptions(bool NotifyParents, bool NotifyStudents, bool NotifyTeachers, bool SendEmail,
	DateTimeOffset? ScheduleAt);

[RemoteService(false), ExposeServices(typeof(ISeatingPlanNotifier), typeof(SeatingNotificationPublication))]
public sealed class SeatingNotificationPublication(IRepository<ExamSeatingPublication, Guid> publications,
	IRepository<NotificationRule, Guid> rules, IRepository<Exam, Guid> exams, IAsyncQueryableExecuter queries,
	INotificationIntentWriter writer, INotificationPersonalWriteLock locks, ICurrentTenant tenant,
	ICancellationTokenProvider cancellation, NotificationSchoolTimeZone zone, IClock clock)
	: ISeatingPlanNotifier, ITransientDependency
{
	private const string OptionsProperty = "AcademicSeatingNotificationOptions";

	public Task LockPublicationAsync(Guid examId) => tenant.Id.HasValue
		? locks.AcquireAsync([$"academic-seating-publication:{examId:N}"], cancellation.Token)
		: Task.CompletedTask;

	public async Task NotifyAsync(Guid examId, PublishSeatingPlanInputDto input)
	{
		if (!input.NotifyParents && !input.NotifyStudents && !input.NotifyTeachers) return;
		var publication = await FindAsync(examId);
		if (publication == null) return;
		var schedule = input.ScheduleAt;
		var options = new SeatingNoticeOptions(input.NotifyParents, input.NotifyStudents, input.NotifyTeachers, input.SendEmail,
			schedule is not DateTime time ? null : time.Kind == DateTimeKind.Unspecified
				? NotificationDeliveryTimingPolicy.Boundary(time, await zone.GetAsync(cancellation.Token), later: true)
				: new DateTimeOffset(time.ToUniversalTime()));
		publication.SetProperty(OptionsProperty, NotificationRuntimeJson.Serialize(options, NotificationErrorCodes.SourceUnavailable));
		await publications.UpdateAsync(publication, autoSave: true, cancellationToken: cancellation.Token);
		await CaptureAsync(publication, options, publication.PublishedByUserId);
	}

	internal async Task CaptureAsync(Guid examId, Guid? actor)
	{
		var publication = await FindAsync(examId);
		if (publication != null && ReadOptions(publication) is { } options)
			await CaptureAsync(publication, options, actor);
	}

	internal static SeatingNoticeOptions? ReadOptions(ExamSeatingPublication publication) =>
		publication.GetProperty<string>(OptionsProperty) is { } json
			? NotificationRuntimeJson.Deserialize<SeatingNoticeOptions>(json, NotificationErrorCodes.SourceUnavailable)
			: null;

	private async Task<ExamSeatingPublication?> FindAsync(Guid examId) => tenant.Id is not Guid school ? null
		: await publications.FindAsync(x => x.TenantId == school && x.ExamId == examId, cancellationToken: cancellation.Token);

	private async Task CaptureAsync(ExamSeatingPublication publication, SeatingNoticeOptions options, Guid? actor)
	{
		var token = cancellation.Token;
		var school = publication.TenantId;
		var examId = publication.ExamId;
		if (!await queries.AnyAsync((await exams.GetQueryableAsync()).Where(x => x.Id == examId && x.TenantId == school &&
				x.Status == ExamStatus.Published), token) ||
			!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == school && x.IsEnabled &&
				x.EventKey == SeatingNotificationSource.Published), token)) return;
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var due = options.ScheduleAt > now ? options.ScheduleAt : null;
		await writer.WriteAsync(new(SeatingNotificationSource.Published, SeatingNotificationSource.Type, examId,
			publication.Id.ToString("N"), now, [], actor, due), token);
	}
}
