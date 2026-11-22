using System;
using System.Linq;
using ShikshaMitra.Academic.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationIntentSnapshotCodec(NotificationRuleConfigurationCodec rules) : ITransientDependency
{
	public string Serialize(NotificationIntentRuntimeSnapshot snapshot, NotificationEventDefinition definition)
	{
		Validate(snapshot, definition); return NotificationRuntimeJson.Serialize(snapshot, NotificationErrorCodes.RuntimeSnapshotInvalid);
	}
	public NotificationIntentRuntimeSnapshot Deserialize(string json, NotificationEventDefinition definition)
	{
		var snapshot = NotificationRuntimeJson.Deserialize<NotificationIntentRuntimeSnapshot>(json, NotificationErrorCodes.RuntimeSnapshotInvalid);
		Validate(snapshot, definition); return snapshot;
	}
	private void Validate(NotificationIntentRuntimeSnapshot? snapshot, NotificationEventDefinition definition)
	{
		if (snapshot?.SchemaVersion != 1 || snapshot.SourceScope == null || snapshot.Configuration == null) throw Invalid();
		if (snapshot.ReviewedDeliveryDigest != null && (snapshot.ReviewedDeliveryDigest.Length != 64 ||
			!snapshot.ReviewedDeliveryDigest.All(Uri.IsHexDigit) || snapshot.DueOccurrenceAt.HasValue)) throw Invalid();
		if (snapshot.SelectedFamilyRelationships is { } family && (snapshot.ReviewedDeliveryDigest == null ||
			!Student360NotificationAudience.Supports(definition.EventKey) || !Student360NotificationAudience.ValidSelection(family))) throw Invalid();
		if (snapshot.DueOccurrenceAt.HasValue && (definition.TriggerType != NotificationTriggerType.Due ||
			snapshot.DueOccurrenceAt.Value == default)) throw Invalid();
		if (snapshot.DuePhase.HasValue && (snapshot.DuePhase != NotificationDuePhase.ReviewerEscalation ||
			definition.EventKey != AcademicNotificationEventKeys.SubstitutionAcknowledgementDue || !snapshot.DueOccurrenceAt.HasValue ||
			snapshot.Configuration.Timing?.ReviewerEscalationMinutes == null)) throw Invalid();
		try
		{
			var scope = snapshot.SourceScope;
			_ = NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId);
			_ = rules.Serialize(snapshot.Configuration, definition);
		}
		catch (ArgumentException) { throw Invalid(); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { throw Invalid(); }
	}
	private static BusinessException Invalid() => new(NotificationErrorCodes.RuntimeSnapshotInvalid);
}
