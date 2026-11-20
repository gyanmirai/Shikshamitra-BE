using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class Student360NotificationAudience(IAcademicNotificationRecipientProjection projection,
	AcademicNotificationCatalogue catalogue) : ITransientDependency
{
	internal static bool Supports(string eventKey) =>
		eventKey is Student360NotificationSourceAdapter.GuardianMessage or Student360NotificationSourceAdapter.MeetingRequested;

	internal static bool ValidSelection(IReadOnlyList<string> relationships) =>
		relationships.Count is > 0 and <= 4 && relationships.Distinct(StringComparer.Ordinal).Count() == relationships.Count &&
		relationships.All(x => x is "father" or "mother" or "guardian" or "self");

	internal async Task<IReadOnlyList<Student360NotificationFamilyChoiceDto>> ReadFamilyChoicesAsync(string eventKey,
		AcademicNotificationAccessContext context, CancellationToken token)
	{
		var student = context.Source.EligibleRecipients.SingleOrDefault(x => x.Kind == "Student");
		if (!context.Source.IsEligible || student == null) return [];
		var data = await projection.LoadAsync(context.TenantId, [student], token);
		var child = data.Students.SingleOrDefault(x => x.Student.Id == student.StudentId);
		if (child == null || child.Student.Status != StudentLifeCycleStatusEnum.Enrolled ||
			!StudentRecordScope.HasAccess(child.Student, context.Scope)) return [];
		var primary = StudentFamilyContactSelection.Resolve(child.Student, child.Family);
		var definition = catalogue.Get(eventKey);
		var allFamily = new AcademicNotificationRecipientPolicy(NotificationGuardianMode.PrimaryAndSelected,
			["father", "mother", "guardian"], IncludeStudentAccount: true);
		return AcademicNotificationFamilyRecipients.ResolveRuntime(child, context.TenantId, allFamily, definition.SupportedChannels,
				definition.RecipientSelectors)
			.Where(x => x.ExclusionCode == null && x.Recipient.FamilyRelationship != null)
			.Select(x => x.Recipient.FamilyRelationship!).Distinct(StringComparer.Ordinal)
			.Select(relationship => new Student360NotificationFamilyChoiceDto(relationship, relationship == primary))
			.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Relationship, StringComparer.Ordinal).ToArray();
	}
}
