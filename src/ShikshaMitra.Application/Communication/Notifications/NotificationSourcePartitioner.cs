using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationSourcePartition(NotificationRuleScope Scope, IReadOnlyList<NotificationRecipientRefDto> Recipients);

[RemoteService(false)]
public sealed class NotificationSourcePartitioner : ITransientDependency
{
	public IReadOnlyList<NotificationSourcePartition> Partition(NotificationIntentContext context,
		NotificationEventDefinition definition, NotificationSourceSnapshot source)
	{
		if (context.TenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		Require(context.EventKey == definition.EventKey && context.SourceType == definition.SourceType && context.SourceId != Guid.Empty);
		if (!source.IsEligible) return [];
		Require(!string.IsNullOrWhiteSpace(source.SourceRevision) && source.SourceRevision.Length <= 128 &&
			source.SourceRevision == context.SourceRevision && source.EligibleRecipients != null && source.AudienceSlices != null &&
			source.Variables != null && SafeLink(source.RelativeLink));
		var roster = source.EligibleRecipients!.ToHashSet();
		Require(roster.All(x => ValidReference(x, definition)) && context.AffectedRecipients != null && context.AffectedRecipients.All(roster.Contains));
		var selected = context.AffectedRecipients.Count == 0 ? roster : context.AffectedRecipients.ToHashSet();
		var covered = new HashSet<NotificationRecipientRefDto>();
		var scopesByStudent = new Dictionary<Guid, string>();
		var groups = new Dictionary<NotificationRuleScope, HashSet<NotificationRecipientRefDto>>();
		foreach (var slice in source.AudienceSlices!)
		{
			Require(slice != null && slice.Scope != null && slice.Recipients != null);
			NotificationRuleScope scope;
			try { scope = NotificationRuleScope.Create(slice!.Scope.AcademicYearId, slice.Scope.BoardId, slice.Scope.MediumId, slice.Scope.ClassId, slice.Scope.DivisionId); }
			catch (ArgumentException) { throw Invalid(); }
			if (!groups.TryGetValue(scope, out var references)) groups[scope] = references = [];
			foreach (var recipient in slice.Recipients)
			{
				Require(roster.Contains(recipient)); covered.Add(recipient);
				if (recipient.Kind == "Student" && recipient.StudentId is Guid studentId)
				{
					Require(!scopesByStudent.TryGetValue(studentId, out var previous) || previous == scope.Key);
					scopesByStudent[studentId] = scope.Key;
				}
				if (selected.Contains(recipient)) references.Add(recipient);
			}
		}
		Require(covered.SetEquals(roster));
		var overrides = source.RecipientContent ?? [];
		Require(overrides.All(x => x != null && roster.Contains(x.Recipient) && x.Variables != null && SafeLink(x.RelativeLink)) &&
			overrides.Select(x => x.Recipient).Distinct().Count() == overrides.Count);
		return groups.OrderBy(x => x.Key.Key, StringComparer.Ordinal).SelectMany(x => x.Value.OrderBy(r => r.Kind, StringComparer.Ordinal)
			.ThenBy(r => r.StudentId).ThenBy(r => r.UserId).ThenBy(r => r.EmployeeId).ThenBy(r => r.FamilyRelationship, StringComparer.Ordinal)
			.Chunk(100).Select(chunk => new NotificationSourcePartition(x.Key, chunk))).ToArray();
	}
	internal static bool ValidReference(NotificationRecipientRefDto? recipient, NotificationEventDefinition definition)
	{
		if (recipient == null || recipient.Kind == null || recipient.Kind.Length > 64 || recipient.FamilyRelationship != null ||
			recipient.StudentId == Guid.Empty || recipient.UserId == Guid.Empty || recipient.EmployeeId == Guid.Empty) return false;
		if (recipient.Kind == "Student") return recipient.StudentId.HasValue && !recipient.UserId.HasValue && !recipient.EmployeeId.HasValue &&
			definition.RecipientSelectors.Any(x => x is "PrimaryGuardian" or "AdditionalGuardians" or "StudentAccount");
		return recipient.UserId.HasValue && recipient.Kind is not ("PrimaryGuardian" or "AdditionalGuardians" or "StudentAccount") &&
			definition.RecipientSelectors.Contains(recipient.Kind);
	}
	private static bool SafeLink(string? link) => link is { Length: > 0 and <= 2048 } && link.StartsWith('/') &&
		!link.StartsWith("//", StringComparison.Ordinal) && !link.Contains('\\') && !link.Any(char.IsControl);
	private static void Require([DoesNotReturnIf(false)] bool valid) { if (!valid) throw Invalid(); }
	private static BusinessException Invalid() => new(NotificationErrorCodes.SourceUnavailable);
}
