using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>In-app delivery projects durable financial events. Fixed messages deliberately omit audit
/// summaries, reasons, changed fields, document URLs, and beneficiary details.</summary>
[Authorize, RemoteService(false)]
public class MobileFinanceActivityService(MobileFinanceAccessService access, IRepository<FinanceAuditEvent, Guid> events) : ApplicationService, IMobileFinanceActivityService
{
	private const string ReadMarker = "family_notification_read";
	private async Task<IQueryable<FinanceAuditEvent>> VisibleAsync(Guid child, Guid year)
	{
		var actor = CurrentUser.Id;
		return (await events.GetQueryableAsync()).Where(x => x.StudentId == child && x.AcademicYearId == year &&
			(x.EntityType == FinanceAuditEntityTypes.FeePayment || x.EntityType == FinanceAuditEntityTypes.FeeReceipt ||
			 x.EntityType == FinanceAuditEntityTypes.FeeRefund || x.EntityType == FinanceAuditEntityTypes.ConcessionRequest ||
			 x.EntityType == FinanceAuditEntityTypes.FeeAdjustment || x.EntityType == FinanceAuditEntityTypes.FeeCharge) &&
			(x.ActorUserId == actor || x.Action == "submit" || x.Action == "approve" || x.Action == "reject" ||
			 x.Action == "cancel" || x.Action == "reverse" || x.Action == "verify" || x.Action == "process" ||
			 x.EntityType == FinanceAuditEntityTypes.FeeRefund && x.Action == "create" ||
			 x.EntityType == FinanceAuditEntityTypes.FeeAdjustment && x.Action == "adjustment" ||
			 x.EntityType == FinanceAuditEntityTypes.FeePayment || x.EntityType == FinanceAuditEntityTypes.FeeReceipt));
	}

	public virtual async Task<MobileFinancePage<MobileFinanceActivityItem>> GetAsync(Guid child, Guid year, int page = 1,
		int pageSize = 20, bool unreadOnly = false, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(child, year, cancellationToken: cancellationToken);
		page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
		var visible = await VisibleAsync(child, year);
		var read = (await events.GetQueryableAsync()).Where(x => x.EntityType == ReadMarker && x.ActorUserId == CurrentUser.Id)
			.Select(x => x.EntityId);
		if (unreadOnly) visible = visible.Where(x => !read.Contains(x.Id));
		var total = await AsyncExecuter.CountAsync(visible, cancellationToken);
		var rows = await AsyncExecuter.ToListAsync(visible.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
			.Skip((page - 1) * pageSize).Take(pageSize), cancellationToken);
		var ids = rows.Select(x => x.Id).ToArray();
		var readIds = (await AsyncExecuter.ToListAsync(read.Where(x => Enumerable.Contains(ids, x)), cancellationToken)).ToHashSet();
		return new(rows.Select(x => new MobileFinanceActivityItem(x.Id, Kind(x.EntityType), Title(x.EntityType, x.Action),
			x.Action, x.OccurredAt, readIds.Contains(x.Id))).ToList(), total, page, pageSize);
	}

	[UnitOfWork]
	public virtual async Task MarkReadAsync(Guid child, Guid year, Guid eventId, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(child, year, cancellationToken: cancellationToken);
		if (!await AsyncExecuter.AnyAsync((await VisibleAsync(child, year)).Where(x => x.Id == eventId), cancellationToken))
			throw new AbpAuthorizationException();
		var actor = CurrentUser.Id ?? throw new AbpAuthorizationException();
		var id = MobileFinanceCommandRules.ReadMarkerId(actor, eventId);
		if (await events.FindAsync(id, cancellationToken: cancellationToken) != null) return;
		await events.InsertAsync(new FinanceAuditEvent(id, CurrentTenant.Id, year, "family", ReadMarker, eventId,
			string.Empty, "read", child, null, null, null, actor, null, null, Clock.Now, "In-app item read."),
			autoSave: true, cancellationToken: cancellationToken);
	}

	private static string Kind(string entityType) => entityType switch
	{
		FinanceAuditEntityTypes.ConcessionRequest => "concession",
		FinanceAuditEntityTypes.FeeRefund => "refund",
		FinanceAuditEntityTypes.FeeAdjustment => "adjustment",
		FinanceAuditEntityTypes.FeeReceipt => "receipt",
		FinanceAuditEntityTypes.FeeCharge => "charge",
		_ => "payment"
	};
	private static string Title(string type, string action) => $"{Kind(type)} {action.Replace('_', ' ')}";
}
