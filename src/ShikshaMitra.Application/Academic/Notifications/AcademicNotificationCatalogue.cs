using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;

namespace ShikshaMitra.Academic.Notifications;

[ExposeServices(typeof(AcademicNotificationCatalogue), typeof(INotificationEventDefinitionProvider))]
public sealed class AcademicNotificationCatalogue : INotificationEventDefinitionProvider, ITransientDependency
{
	private static readonly IReadOnlyList<NotificationEventDefinition> Definitions = AcademicNotificationDefinitions.Create();
	private static readonly IReadOnlyDictionary<string, NotificationEventDefinition> ByKey =
		Definitions.ToDictionary(x => x.EventKey, StringComparer.Ordinal);
	private readonly HashSet<string> _supportedEvents;
	private readonly HashSet<(string SourceType, string EventKey)> _scheduledEvents;

	public AcademicNotificationCatalogue(IEnumerable<INotificationSourceAdapter> adapters,
		IEnumerable<INotificationDueSource>? dueSources = null)
	{
		_supportedEvents = adapters.SelectMany(x => x.EventKeys).ToHashSet(StringComparer.Ordinal);
		_scheduledEvents = (dueSources ?? []).SelectMany(source =>
		{
			var keys = source.ScheduledEventKeys ?? [];
			return Definitions.Where(x => x.SourceType == source.SourceType && x.TriggerType == NotificationTriggerType.Due &&
				(keys.Count == 0 || keys.Contains(x.EventKey))).Select(x => (x.SourceType, x.EventKey));
		}).ToHashSet();
	}

	public IReadOnlyList<NotificationEventDefinition> GetAll() => Definitions;

	public NotificationEventDefinition Get(string eventKey) => GetDefinition(eventKey);

	internal static NotificationEventDefinition GetDefinition(string eventKey) => eventKey != null && ByKey.TryGetValue(eventKey, out var definition)
		? definition
		: throw new BusinessException(NotificationErrorCodes.EventUnknown);

	public NotificationSourceAvailability GetAvailability(string eventKey)
	{
		var definition = Get(eventKey);
		return _supportedEvents.Contains(eventKey) &&
			(definition.TriggerType != NotificationTriggerType.Due || _scheduledEvents.Contains((definition.SourceType, eventKey)))
			? new(true, null)
			: new(false, NotificationErrorCodes.SourceUnavailable);
	}

	internal static string? GetSourceDependency(string eventKey) => eventKey switch
	{
		ElectivePreferenceOpened or ElectivePreferenceDue =>
			"This notice requires an elective preference window with saved opening and due dates.",
		LessonPlanSubmissionDue => "This reminder requires a saved lesson plan submission deadline.",
		HomeworkChanged or AssignmentChanged => "This notice requires a published task edit that identifies the affected families.",
		HomeworkWithdrawn or AssignmentWithdrawn => "This notice requires a withdrawal that identifies who could see the published task.",
		EvaluationGradingDue => "This reminder requires a saved grading deadline.",
		ExamApprovalRequested or ExamApproved or ExamChangesRequested =>
			"This notice requires an exam review workflow with saved decisions.",
		ExamRescheduled => "This notice requires a saved exam reschedule and a list of the people affected.",
		ExamCancelled => "This notice requires a saved exam cancellation and a list of the people affected.",
		SeatingChanged => "This notice requires the saved seating list from the previous release.",
		ExamDutyAssigned or ExamDutyChanged => "This notice requires saved invigilation assignments and changes.",
		ExamDutyReminder => "This reminder requires a saved invigilation assignment and reminder schedule.",
		MarksEntryOpened => "This notice requires a saved marks-entry opening date.",
		MarksEntryDue => "This reminder requires a saved marks-entry deadline.",
		ResultReadyForReview => "This notice requires a saved result review step.",
		ResultCorrected => "Published corrections use the Result published notice.",
		Student360FollowUpDue => "This reminder requires a saved follow-up and an assigned staff member.",
		_ => null
	};

	public void EnsureAvailable(string eventKey)
	{
		if (!GetAvailability(eventKey).IsAvailable)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
	}

	public NotificationSourceAvailability GetManualAvailability(string eventKey)
	{
		Get(eventKey);
		return eventKey is not (AcademicNotificationEventKeys.TimetableDayReminder or AcademicNotificationEventKeys.ExamReminder or
			AcademicNotificationEventKeys.CalendarEventReminder or AcademicNotificationEventKeys.ProgressBehindSchedule or
			AcademicNotificationEventKeys.ProgressMilestoneReached or AcademicNotificationEventKeys.ReportExportReady or
			AcademicNotificationEventKeys.ReportExportFailed or AcademicNotificationEventKeys.SummaryWeekly or
			AcademicNotificationEventKeys.SettingsOperationalImpact) &&
			_supportedEvents.Contains(eventKey)
			? new(true, null)
			: new(false, NotificationErrorCodes.SourceUnavailable);
	}

	public void EnsureSourceAvailable(string eventKey)
	{
		if (!GetManualAvailability(eventKey).IsAvailable)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
	}
}
