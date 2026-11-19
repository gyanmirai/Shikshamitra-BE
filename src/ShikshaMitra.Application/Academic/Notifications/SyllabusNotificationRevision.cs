using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Data;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record SyllabusNoticeRevision(string EventKey, string Revision, string Content, string? SubjectPublication);

internal static class SyllabusNotificationRevision
{
	internal static SyllabusNoticeRevision? Read(AssignedSubject subject, Guid chapterId) =>
		subject.GetProperty<string>(Property(chapterId)) is { } json
			? NotificationRuntimeJson.Deserialize<SyllabusNoticeRevision>(json, NotificationErrorCodes.SourceUnavailable)
			: null;

	internal static void Clear(AssignedSubject subject, Guid chapterId) => subject.RemoveProperty(Property(chapterId));

	internal static CurriculumNoticeCapture? Record(AssignedSubject subject, AssignedSubjectChapter chapter, string? before,
		string? after, bool allowed)
	{
		if (!allowed || after == null)
		{
			Clear(subject, chapter.Id);
			return null;
		}

		var state = new SyllabusNoticeRevision(before == null ? SyllabusPublished : SyllabusRevised, Guid.NewGuid().ToString("N"),
			after, SubjectAllocationNotification.Read(subject)?.Publication);
		subject.SetProperty(Property(chapter.Id), NotificationRuntimeJson.Serialize(state, NotificationErrorCodes.SourceUnavailable));
		return new(chapter.Id, state.EventKey, state.Revision);
	}

	internal static string? Content(AssignedSubject subject, AssignedSubjectChapter chapter)
	{
		if (subject.Status != SubjectStatus.Published || subject.IsDeleted || chapter.IsDeleted ||
			chapter.PublishStatus is not ("Published" or "published") || chapter.Visibility is not ("Students" or "Parents" or "All"))
			return null;

		// Version the published material separately from planning and progress metadata.
		var content = JsonSerializer.SerializeToUtf8Bytes(new
		{
			chapter.ChapterNumber, chapter.ChapterName, chapter.Description,
			Start = chapter.ChapterStartTime.UtcDateTime, End = chapter.ChapterEndTime.UtcDateTime,
			chapter.UnitName, chapter.TermName, chapter.Topic, chapter.LearningOutcomes,
			chapter.EstimatedTeachingDays, chapter.EstimatedPeriods, chapter.DifficultyLevel, chapter.BloomLevel,
			chapter.Tags, chapter.ClassActivities, chapter.CompletionCriteria, chapter.AssessmentMethod,
			chapter.QuestionTypes, chapter.MarksWeightage, chapter.SkillsCovered, chapter.Prerequisites,
			chapter.StudentSummary, chapter.ParentSummary, chapter.Visibility,
			Documents = chapter.Documents.Where(x => !x.IsDeleted).Select(x => x.BlobName).Distinct().Order(StringComparer.Ordinal)
		});
		return Convert.ToHexString(SHA256.HashData(content));
	}

	private static string Property(Guid chapterId) => $"AcademicSyllabusNotice:{chapterId:N}";
}
