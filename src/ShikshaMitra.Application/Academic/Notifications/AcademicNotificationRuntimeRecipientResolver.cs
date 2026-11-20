using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationRuntimeRecipientResolver))]
public sealed class AcademicNotificationRuntimeRecipientResolver(ICurrentTenant tenant,
	IAcademicNotificationRecipientProjection projection) : INotificationRuntimeRecipientResolver, ITransientDependency
{
	public bool Supports(string eventKey) => eventKey.StartsWith("Academic.", StringComparison.Ordinal);

	public async Task<IReadOnlyList<NotificationRuntimeRecipient>> ResolveAsync(NotificationCurrentSource source,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (source.TenantId == Guid.Empty || source.TenantId != tenant.Id || !Supports(source.Definition.EventKey))
			throw new AbpAuthorizationException();
		var config = source.Runtime.Configuration;
		var policy = new AcademicNotificationRecipientPolicy(config.GuardianMode, config.AdditionalGuardianRelationships, config.IncludeStudentAccount);
		var current = source.CurrentRecipients.ToHashSet();
		var selected = source.CapturedRecipients.Where(current.Contains).ToArray();
		var data = selected.Length == 0 ? new AcademicNotificationRecipientData([], []) :
			await projection.LoadAsync(source.TenantId, selected, cancellationToken);
		var children = data.Students.ToDictionary(x => x.Student.Id);
		var staff = data.Staff.ToDictionary(x => (x.UserId, x.EmployeeId));
		var results = new List<NotificationRuntimeRecipient>();
		foreach (var recipient in source.CapturedRecipients)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!current.Contains(recipient))
			{
				Exclude(recipient, source.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible);
				continue;
			}
			if (recipient.Kind == "Student" && recipient.StudentId is Guid studentId)
			{
				var row = children.GetValueOrDefault(studentId);
				if (row == null || row.Student.IsDeleted || row.Student.Status != StudentLifeCycleStatusEnum.Enrolled)
					Exclude(recipient, "StudentNotEnrolled");
				else if (row.Student.TenantId != source.TenantId || !InScope(row.Student, source.Runtime.SourceScope))
					Exclude(recipient, "StudentScopeChanged");
				else
				{
					var family = AcademicNotificationFamilyRecipients.ResolveRuntime(row, source.TenantId, policy, config.Channels,
						source.Definition.RecipientSelectors);
					var relationships = source.Runtime.SelectedFamilyRelationships;
					results.AddRange(relationships == null ? family : family.Where(item =>
						item.Recipient.FamilyRelationship is { } relationship && relationships.Contains(relationship)));
				}
				continue;
			}
			if (recipient.UserId is not Guid userId || !config.StaffAudienceSelectors.Contains(recipient.Kind))
				throw new BusinessException(NotificationErrorCodes.RuntimeSnapshotInvalid);
			var person = staff.GetValueOrDefault((userId, recipient.EmployeeId));
			if (person is not { IsActive: true })
			{
				Exclude(recipient, "StaffAccountInactive");
				continue;
			}
			var revision = AcademicNotificationRecipientRevision.ForStaff(person);
			foreach (var channel in config.Channels)
			{
				var reason = AcademicNotificationFamilyRecipients.ChannelExclusion(channel, person.Email, person.Phone, true);
				results.Add(new(recipient, recipient, channel, reason, revision,
					AcademicNotificationFamilyRecipients.Destination(channel, person.Email, person.Phone), person.DisplayName));
			}
		}
		return results;

		void Exclude(NotificationRecipientRefDto recipient, string reason)
		{
			foreach (var channel in config.Channels) results.Add(new(recipient, recipient, channel, reason));
		}
	}

	private static bool InScope(Student student, NotificationSourceScopeDto scope) =>
		(!scope.AcademicYearId.HasValue || student.AcademicYearId == scope.AcademicYearId) &&
		(!scope.BoardId.HasValue || student.AcademicBoardId == scope.BoardId) &&
		(!scope.MediumId.HasValue || student.AcademicMediumId == scope.MediumId) &&
		(!scope.ClassId.HasValue || student.StandardId == scope.ClassId) &&
		(!scope.DivisionId.HasValue || student.DivisionId == scope.DivisionId);
}
