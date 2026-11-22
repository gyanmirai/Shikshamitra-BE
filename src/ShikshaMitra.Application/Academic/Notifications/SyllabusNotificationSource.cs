using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(SyllabusNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class SyllabusNotificationSource(IRepository<AssignedSubjectChapter, Guid> chapters,
	SubjectAllocationNotificationSource subjects, IRepository<Division, Guid> divisions, TimetableNotificationAudience staff,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Syllabus";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { SyllabusPublished, SyllabusRevised });

	internal async Task<(AssignedSubjectChapter Chapter, SubjectNoticePlacement Subject)?> FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var chapter = await queries.SingleOrDefaultAsync((await chapters.WithDetailsAsync(x => x.Documents))
			.Where(x => x.Id == id && x.TenantId == school && !x.IsDeleted), token);
		if (chapter == null) return null;
		var subject = await subjects.FindAsync(school, chapter.AssignedSubjectId, includeDeleted: false, token);
		return subject == null ? null : (chapter, subject);
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();
		if (await FindAsync(school, context.SourceId, token) is not { } record) return Unavailable();

		var (chapter, placement) = record;
		var scope = placement.Scope;
		var requested = context.SourceScope;
		if (requested != null && (requested with { DivisionId = null }) != scope) return Unavailable();
		var state = SyllabusNotificationRevision.Read(placement.Assignment, chapter.Id);
		if (state == null || state.EventKey != context.EventKey ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != state.Revision) ||
			state.SubjectPublication != SubjectAllocationNotification.Read(placement.Assignment)?.Publication ||
			state.Content != SyllabusNotificationRevision.Content(placement.Assignment, chapter)) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var slices = new List<NotificationSourceAudienceSliceDto>();
		if (requested?.DivisionId == null)
		{
			var assigned = SubjectAllocationNotification.Teachers(placement.Assignment).Select(x => x.EmployeeId).Distinct().ToArray();
			var teachers = await staff.FindTeachersAsync(school, assigned, token);
			if (teachers.Count != 0) slices.Add(new(scope, teachers));
		}
		if (requested == null || requested.DivisionId.HasValue)
		{
			var requestedDivision = requested?.DivisionId;
			var divisionIds = (await queries.ToListAsync((await divisions.GetQueryableAsync()).Where(x =>
				x.TenantId == school && !x.IsDeleted && x.StandardId == scope.ClassId &&
				(!requestedDivision.HasValue || x.Id == requestedDivision)).Select(x => x.Id), token)).ToArray();
			if (divisionIds.Length != 0 && (placement.Assignment.SubjectType != SubjectType.Elective ||
				SubjectAllocationNotification.GroupId(placement.Assignment).HasValue))
			{
				var learners = await subjects.ReadLearnersAsync(placement, school, divisionIds, token);
				slices.AddRange(learners.GroupBy(x => x.DivisionId).Select(group =>
					new NotificationSourceAudienceSliceDto(scope with { DivisionId = group.Key },
						group.Select(x => new NotificationRecipientRefDto("Student", StudentId: x.StudentId)).ToArray())));
			}
		}

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, placement.ClassName),
			["subject_name"] = new(NotificationVariableType.Text, placement.SubjectName),
			["syllabus_title"] = new(NotificationVariableType.Text, chapter.ChapterName),
			["revision_summary"] = new(NotificationVariableType.Text,
				state.EventKey == SyllabusPublished ? "A syllabus chapter is available." : "A published syllabus chapter has been updated.")
		};
		var definition = AcademicNotificationCatalogue.GetDefinition(context.EventKey);
		var selectors = chapter.Visibility == "Parents"
			? definition.RecipientSelectors.Where(x => x != "StudentAccount").ToArray()
			: definition.RecipientSelectors;
		return new(true, null, state.Revision, Link, variables,
			slices.SelectMany(x => x.Recipients).Distinct().ToArray(), AudienceSlices: slices)
		{
			DeliveryLimits = new(selectors, definition.SupportedChannels)
		};
	}
}
