using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public sealed record LibraryFineReminderEvidence(Guid AssessmentId, Guid SourceLinkId, FinanceExternalPayerKind PayerKind,
	Guid PayerId, DateOnly DueDate, decimal Collectible, string Currency);

public interface ILibraryFineReminderEvidenceReader
{
	Task<IReadOnlyDictionary<Guid, LibraryFineReminderEvidence>> ReadAsync(Guid[] assessmentIds, DateOnly schoolDate);
}

// Notification intents use this bounded read. Public requests go through the owning notification action.
public class LibraryFineReminderEvidenceReader(IRepository<LibraryCharge, Guid> charges,
	IRepository<LibraryMember, Guid> memberships, LibraryAssessmentSourceScopeQuery assessmentScope, IUserScopeService scopes,
	ILibraryChargePositionSourceQuery sources, IFinanceExternalPositionReader finance, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant) : ILibraryFineReminderEvidenceReader, ITransientDependency
{
	internal const int BatchSize = 100;

	internal async Task<IQueryable<LibraryCharge>> GetCandidatesAsync(DateOnly schoolDate, LibraryNotificationOrigin origin)
	{
		var candidates = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id && tenant.Id.HasValue &&
			row.FinanceSourceLinkId.HasValue && row.SchoolDueDate.HasValue && row.SchoolDueDate.Value <= schoolDate);
		if (origin.IsSystem && !origin.ActorUserId.HasValue && string.IsNullOrWhiteSpace(origin.Action)) return candidates;
		if (!origin.ActorUserId.HasValue || origin.IsSystem) return candidates.Where(_ => false);
		var readable = await assessmentScope.GetQueryAsync(await scopes.GetUserScopeAsync(origin.ActorUserId.Value));
		return candidates.Where(row => readable.Any(source => source.SourceId == row.Id));
	}

	public async Task<IReadOnlyDictionary<Guid, LibraryFineReminderEvidence>> ReadAsync(Guid[] assessmentIds, DateOnly schoolDate)
	{
		var result = new Dictionary<Guid, LibraryFineReminderEvidence>();
		if (!tenant.Id.HasValue || assessmentIds.Length == 0) return result;
		var raw = (await sources.GetQueryAsync()).Where(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(assessmentIds, row.SourceId) && row.ExpectedSourceLinkId.HasValue && row.SchoolDatesConfirmed &&
			row.SchoolDueDate.HasValue && row.SchoolDueDate.Value <= schoolDate);
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var localPayers = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(assessmentIds, row.Id) && (row.MemberRole == 0 || row.MemberRole == 1 || row.MemberRole == 2))
			.Select(row => new FinanceExternalReadableSource
			{
				SourceId = row.Id,
				StudentId = row.MemberRole == 0 ? aliases.Where(alias => alias.Id == row.MemberId)
					.Select(alias => (Guid?)alias.StudentId).FirstOrDefault() ?? row.MemberId : null,
				EmployeeId = row.MemberRole == 0 ? null : row.MemberId
			});
		var facts = await queries.ToListAsync(LibraryChargePositionReader.WithCanonicalPayers(raw, localPayers));
		if (facts.Count == 0) return result;
		var positions = (await finance.ReadAsync(facts)).ToDictionary(row => row.SourceId);
		foreach (var source in facts)
		{
			var payer = source.PayerKind == FinanceExternalPayerKind.Student ? source.StudentId : source.EmployeeId;
			if (payer is not Guid payerId || payerId == Guid.Empty || source.SchoolDueDate is not DateOnly dueDate ||
				!positions.TryGetValue(source.SourceId, out var position) || position.Evidence != FinanceExternalPositionEvidence.Verified ||
				position.SourceLinkId is not Guid linkId || linkId != source.ExpectedSourceLinkId ||
				position.Amounts is not { Collectible: > 0 } amounts || string.IsNullOrWhiteSpace(position.Currency)) continue;
			result.Add(source.SourceId, new(source.SourceId, linkId, source.PayerKind, payerId, dueDate, amounts.Collectible, position.Currency));
		}
		return result;
	}
}
