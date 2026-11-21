using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Members.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Members;

public partial class LibraryMemberAppService
{
	public async Task<PaginatedResponse<MemberChargeItemDto>> GetMemberChargesAsync(Guid memberId, int page = 1, int pageSize = 10)
	{
		var input = new LibraryChargePositionReadInput(LibraryChargePositionReadPurpose.MemberCharges);
		var readableIds = await chargePositionReader.GetReadableSourceIdsAsync(input);
		var memberIds = (await libraryAccess.GetPersonMemberIdsAsync(memberId)).ToArray();
		var assessments = await libraryChargeRepository.GetQueryableAsync();
		var visible = from charge in assessments
			join readableId in readableIds on charge.Id equals readableId
			where charge.TenantId == CurrentTenant.Id && Enumerable.Contains(memberIds, charge.MemberId)
			select charge;

		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var total = await AsyncExecuter.CountAsync(visible);
		var result = new PaginatedResponse<MemberChargeItemDto>
		{
			TotalCount = total,
			TotalPages = (int)(((long)total + pageSize - 1) / pageSize),
			CurrentPage = page,
			PageSize = pageSize
		};
		var offset = ((long)page - 1) * pageSize;
		if (offset >= total) return result;

		var charges = await AsyncExecuter.ToListAsync(visible.OrderByDescending(charge => charge.ChargeDate)
			.ThenByDescending(charge => charge.CreationTime).ThenBy(charge => charge.Id).Skip((int)offset).Take(pageSize));
		if (charges.Count == 0) return result;

		var pageIds = charges.Select(charge => charge.Id).ToArray();
		var positions = (await chargePositionReader.ReadAsync(input with { AssessmentIds = pageIds }))
			.ToDictionary(position => position.SourceId);
		if (positions.Count != pageIds.Length || pageIds.Any(id => !positions.ContainsKey(id)))
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);

		var categoryIds = charges.Select(charge => charge.ChargeCategoryId).Distinct().ToArray();
		var categories = (await AsyncExecuter.ToListAsync((await libraryChargeCategoryRepository.GetQueryableAsync())
			.Where(category => category.TenantId == CurrentTenant.Id && Enumerable.Contains(categoryIds, category.Id))
			.Select(category => new { category.Id, category.ChargeType })))
			.ToDictionary(category => category.Id, category => category.ChargeType);
		var issues = await ReadMemberChargeIssuesAsync(charges);
		var books = await GetMemberIssueBooksAsync(issues.Values);
		result.Items = charges.Select(charge =>
		{
			var issue = charge.BookIssueId is Guid issueId ? issues.GetValueOrDefault(issueId) : null;
			var book = issue == null ? null : books.GetValueOrDefault(issue.Id);
			return new MemberChargeItemDto
			{
				Id = charge.Id,
				Amount = charge.Amount,
				AmountText = charge.Amount.ToString("G29", CultureInfo.InvariantCulture),
				RecordedCurrency = charge.Currency,
				AcademicYearId = charge.AcademicYearId,
				AcademicYearProvenance = charge.AcademicYearProvenance,
				Date = charge.ChargeDate,
				SchoolChargeDate = charge.SchoolChargeDate,
				SchoolDueDate = charge.SchoolDueDate,
				ChargeDateProvenance = (int)charge.ChargeDateProvenance,
				DueDateProvenance = (int)charge.DueDateProvenance,
				ChargeType = categories.GetValueOrDefault(charge.ChargeCategoryId, "Unknown"),
				Reason = charge.Remarks,
				PaymentStatus = (int)charge.PaymentStatus,
				AccessionNo = issue?.AccessionNumber,
				BookId = book?.BookId,
				CopyId = book?.CopyId,
				BookTitle = book?.Title,
				BookCategory = book?.Category,
				CopyLinkSource = book?.LinkSource ?? "Unavailable",
				RequiresCopyReconciliation = book?.RequiresReconciliation ?? charge.BookIssueId.HasValue,
				FinancePosition = MapMemberChargePosition(positions[charge.Id])
			};
		}).ToList();
		return result;
	}

	private async Task<Dictionary<Guid, BookIssue>> ReadMemberChargeIssuesAsync(IReadOnlyCollection<LibraryCharge> charges)
	{
		var ids = charges.Where(charge => charge.BookIssueId.HasValue).Select(charge => charge.BookIssueId!.Value).Distinct().ToArray();
		if (ids.Length == 0) return [];
		var context = await libraryAccess.GetContextAsync();
		var loans = await loanScope.GetReadableLoansAsync(context.UserScope);
		return (await AsyncExecuter.ToListAsync(loans.Where(loan => loan.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(ids, loan.Id)))).ToDictionary(loan => loan.Id);
	}

	private static LibraryMemberChargePositionDto MapMemberChargePosition(FinanceExternalSourcePosition position) => new()
	{
		Evidence = position.Evidence,
		ApprovalStatus = position.ApprovalStatus,
		Currency = position.Currency,
		ReviewIssues = position.ReviewIssues,
		Amounts = position.Evidence == FinanceExternalPositionEvidence.Verified && position.Amounts is { } amounts
			? new LibraryMemberChargeAmountsDto
			{
				OriginalAmountText = AmountText(amounts.Original),
				AcceptedAmountText = AmountText(amounts.Accepted),
				SettledAmountText = AmountText(amounts.Settled),
				PendingVerificationAmountText = AmountText(amounts.Pending),
				NetWaivedAmountText = AmountText(amounts.NetWaived),
				FinancialOutstandingAmountText = AmountText(amounts.FinancialOutstanding),
				CollectibleAmountText = AmountText(amounts.Collectible),
				RefundedAmountText = AmountText(amounts.Refunded)
			} : null
	};

	private static string AmountText(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);
}
