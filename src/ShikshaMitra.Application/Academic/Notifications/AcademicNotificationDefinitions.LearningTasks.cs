using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> LearningTasks() =>
	[
		Event(HomeworkPublished, $"{Family},{Teacher}"),
		Event(HomeworkChanged, Family, Important, Change, "title", "instructions", "dueAt"),
		Event(HomeworkWithdrawn, Family, Important),
		Event(HomeworkDue, Family, Routine, Due),
		Event(HomeworkOverdue, $"{Family},{Teacher}", Routine, Due),
		Event(HomeworkSubmitted, Teacher, Digest),
		Event(HomeworkRevisionRequested, Family),
		Event(HomeworkFeedbackReleased, Family, Private),
		Event(AssignmentPublished, $"{Family},{Teacher}"),
		Event(AssignmentChanged, Family, Important, Change, "title", "instructions", "dueAt"),
		Event(AssignmentWithdrawn, Family, Important),
		Event(AssignmentDue, Family, Routine, Due),
		Event(AssignmentOverdue, $"{Family},{Teacher}", Routine, Due),
		Event(AssignmentSubmitted, Teacher, Digest),
		Event(AssignmentRevisionRequested, Family),
		Event(AssignmentFeedbackReleased, Family, Private),
		Event(EvaluationGradingDue, $"{Teacher},{Reviewer}", StaffOnly, Due)
	];
}
