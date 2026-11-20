using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> Curriculum() =>
	[
		Event(ClassTeacherAssigned, $"{Teacher},{Reviewer}", StaffOnly),
		Event(ClassTeacherReplaced, $"PreviousTeacher,{Teacher},{Reviewer}", StaffOnly),
		Event(ClassTeacherRemoved, $"PreviousTeacher,{Reviewer}", StaffOnly),
		Event(SubjectAllocated, $"{Teacher},{ClassTeacher}", StaffOnly),
		Event(SubjectTeacherChanged, $"PreviousTeacher,{Teacher},{ClassTeacher}", StaffOnly),
		Event(SubjectWithdrawn, $"{Teacher},{ClassTeacher},{Family}", Important),
		Event(SyllabusPublished, $"{Teacher},{Family}"),
		Event(SyllabusRevised, $"{Teacher},{Family}", Routine, Change, "title", "content", "visibility"),
		Event(ElectivePreferenceOpened, Family),
		Event(ElectivePreferenceDue, Family, Important, Due),
		Event(ElectiveSelectionConfirmed, $"{Family},{Teacher}"),
		Event(ElectiveLockedSelectionCorrected, $"{Family},PreviousTeacher,{Teacher},{ClassTeacher}", Important),
		Event(LessonPlanSubmitted, Reviewer, StaffOnly),
		Event(LessonPlanApproved, Teacher, StaffOnly),
		Event(LessonPlanChangesRequested, Teacher, StaffOnly),
		Event(LessonPlanRejected, Teacher, StaffOnly),
		Event(LessonPlanSubmissionDue, Teacher, StaffOnly, Due),
		Event(DiaryFollowUpRequested, $"{Teacher},{Reviewer}", NotificationDeliveryProfile.Manual, NotificationTriggerType.Manual),
		Event(ProgressBehindSchedule, $"{Teacher},{ClassTeacher},{Reviewer}", Digest, Due),
		Event(ProgressMilestoneReached, $"{Teacher},{ClassTeacher}", Digest, Due)
	];
}
