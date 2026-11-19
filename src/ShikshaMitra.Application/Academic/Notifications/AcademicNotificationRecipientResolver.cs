using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationRecipientResolver(IAcademicNotificationAccessService access,
	IAcademicNotificationRecipientProjection projection, AcademicNotificationCatalogue catalogue)
	: IAcademicNotificationRecipientResolver, ITransientDependency
{
	public async Task<NotificationAudiencePreviewDto> ResolveAsync(AcademicNotificationAudienceInput input,
		CancellationToken cancellationToken)
	{
		var context = await access.RequireSendAsync(input, cancellationToken);
		var definition = catalogue.Get(input.EventKey);
		Validate(input, definition);
		if (!context.Source.IsEligible)
			return new(0, 0, [], [context.Source.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible]);
		definition = NotificationSourceDeliveryPolicy.Restrict(definition, context.Source);
		var policy = input.Policy;
		if (context.Source.DeliveryLimits != null)
		{
			if (!definition.RecipientSelectors.Contains("AdditionalGuardians"))
				policy = policy with { GuardianMode = NotificationGuardianMode.PrimaryOnly, AdditionalGuardianRelationships = [] };
			policy = policy with { IncludeStudentAccount = policy.IncludeStudentAccount && definition.RecipientSelectors.Contains("StudentAccount") };
		}
		var sourceRecipients = context.Source.EligibleRecipients.Distinct().ToArray();
		if (input.AffectedRecipients.Any(x => !sourceRecipients.Contains(x))) throw new AbpAuthorizationException();
		if (input.Channels.Any(x => !definition.SupportedChannels.Contains(x)))
			return new(0, 0, [], ["ChannelsExcludedBySource"]);
		var selected = input.AffectedRecipients.Count == 0 ? sourceRecipients : input.AffectedRecipients.Distinct().ToArray();
		var data = await projection.LoadAsync(context.TenantId, selected, cancellationToken);
		var results = new List<NotificationRecipientEligibilityDto>();
		var issues = new List<string>();
		foreach (var recipient in selected)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (recipient.Kind == "Student" && recipient.StudentId is Guid studentId)
			{
				if (!definition.RecipientSelectors.Any(x => x is "PrimaryGuardian" or "StudentAccount"))
					throw new AbpAuthorizationException();
				var row = data.Students.SingleOrDefault(x => x.Student.Id == studentId);
				if (row != null && (row.Student.TenantId != context.TenantId ||
					context.Scope.TenantId != context.TenantId || !StudentRecordScope.HasAccess(row.Student, context.Scope)))
					throw new AbpAuthorizationException();
				if (row == null || row.Student.Status != StudentLifeCycleStatusEnum.Enrolled)
				{
					AddExcluded(recipient, "StudentNotEnrolled");
					continue;
				}
				results.AddRange(AcademicNotificationFamilyRecipients.Resolve(row, context.TenantId,
					policy, input.Channels, definition.RecipientSelectors));
			}
			else if (definition.RecipientSelectors.Contains(recipient.Kind) && recipient.UserId.HasValue)
			{
				var staff = data.Staff.SingleOrDefault(x => x.UserId == recipient.UserId && x.EmployeeId == recipient.EmployeeId);
				foreach (var channel in input.Channels.Distinct())
				{
					var reason = staff is not { IsActive: true } ? "StaffAccountInactive" :
						AcademicNotificationFamilyRecipients.ChannelExclusion(channel, staff.Email, staff.Phone, true);
					results.Add(new(recipient, channel, reason == null, reason,
						staff == null ? null : AcademicNotificationRecipientRevision.ForStaff(staff)));
				}
			}
			else throw new AbpAuthorizationException();
		}
		if (definition.RecipientSelectors.Contains("ConfiguredReviewer") && !selected.Any(x => x.Kind == "ConfiguredReviewer"))
			issues.Add("ReviewerNotConfigured");
		var rows = results.DistinctBy(x => (x.Channel, x.Recipient.StudentId, x.Recipient.UserId,
			x.Recipient.UserId.HasValue ? null : x.Recipient.FamilyRelationship, x.Recipient.Kind)).ToArray();
		var eligible = rows.Where(x => x.IsEligible).Select(x => x.Recipient).ToHashSet();
		return new(eligible.Count, rows.Where(x => !x.IsEligible && !eligible.Contains(x.Recipient))
			.Select(x => x.Recipient).Distinct().Count(), rows, issues);

		void AddExcluded(NotificationRecipientRefDto recipient, string reason)
		{
			foreach (var channel in input.Channels.Distinct()) results.Add(new(recipient, channel, false, reason));
		}
	}

	private static void Validate(AcademicNotificationAudienceInput input, NotificationEventDefinition definition)
	{
		if (input.SourceId == Guid.Empty || input.SourceType != definition.SourceType || input.Channels.Count == 0 ||
			input.Channels.Any(x => !definition.SupportedChannels.Contains(x)) || input.AffectedRecipients.Count > 10000 ||
			!Enum.IsDefined(input.Policy.GuardianMode) || input.Policy.AdditionalGuardianRelationships.Count > 3 ||
			input.Policy.AdditionalGuardianRelationships.Any(x => x is not ("father" or "mother" or "guardian")) ||
			input.Policy.GuardianMode == NotificationGuardianMode.PrimaryOnly && input.Policy.AdditionalGuardianRelationships.Count != 0)
			throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
	}
}
