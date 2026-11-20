using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private async Task<List<Dictionary<string, string?>>> ReadFineRowsAsync(
		LibraryReportExecutionScope scope, LibraryReportFilterDto filters)
	{
		var input = new LibraryChargePositionReadInput(LibraryChargePositionReadPurpose.Reports, filters.AcademicYearId);
		var sourceIds = await chargePositionReader.GetReadableSourceIdsAsync(input);
		var charges = from charge in await chargeRepository.GetQueryableAsync()
			join id in sourceIds on charge.Id equals id
			where charge.TenantId == tenant.Id
			select charge;
		charges = await FilterFineAssessmentsAsync(charges, filters);
		var selected = await asyncExecuter.ToListAsync(charges.OrderByDescending(row => row.SchoolChargeDate)
			.ThenByDescending(row => row.ChargeDate).ThenBy(row => row.Id));
		var rows = new List<Dictionary<string, string?>>(selected.Count);
		foreach (var batch in selected.Chunk(100))
		{
			var ids = batch.Select(charge => charge.Id).ToArray();
			var positions = (await chargePositionReader.ReadAsync(input with { AssessmentIds = ids }))
				.ToDictionary(position => position.SourceId);
			if (positions.Count != ids.Length || ids.Any(id => !positions.ContainsKey(id)))
				throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);

			var context = await LoadFineEvidenceAsync(scope, batch);
			rows.AddRange(batch.Select(charge => BuildFineRow(context, charge, positions[charge.Id])));
		}
		return rows;
	}

	private async Task<IQueryable<LibraryCharge>> FilterFineAssessmentsAsync(
		IQueryable<LibraryCharge> charges, LibraryReportFilterDto filters)
	{
		if (filters.MemberIds is { Count: > 0 })
		{
			var ids = filters.MemberIds.ToArray();
			var aliases = (await membershipRepository.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
			var aliasedPeople = aliases.Where(row => Enumerable.Contains(ids, row.Id)).Select(row => row.StudentId);
			var matchingAliases = aliases.Where(row => Enumerable.Contains(ids, row.StudentId) ||
				aliasedPeople.Contains(row.StudentId)).Select(row => row.Id);
			charges = charges.Where(row => Enumerable.Contains(ids, row.MemberId) || row.MemberRole == 0 &&
				(aliasedPeople.Contains(row.MemberId) || matchingAliases.Contains(row.MemberId)));
		}
		if (filters.MemberRole.HasValue)
			charges = filters.MemberRole.Value == 0 ? charges.Where(row => row.MemberRole == 0) :
				charges.Where(row => row.MemberRole == 1 || row.MemberRole == 2);
		if (filters.ChargeStatuses is { Count: > 0 })
		{
			var statuses = filters.ChargeStatuses.ToArray();
			charges = charges.Where(row => Enumerable.Contains(statuses, (int)row.PaymentStatus));
		}
		if (filters.FromDate.HasValue || filters.ToDate.HasValue)
		{
			charges = charges.Where(row => row.SchoolChargeDate.HasValue &&
				(row.ChargeDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
				 row.ChargeDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy));
			if (filters.FromDate is DateTime fromDate)
			{
				var from = DateOnly.FromDateTime(fromDate);
				charges = charges.Where(row => row.SchoolChargeDate >= from);
			}
			if (filters.ToDate is DateTime toDate)
			{
				var to = DateOnly.FromDateTime(toDate);
				charges = charges.Where(row => row.SchoolChargeDate <= to);
			}
		}
		return charges;
	}

	private async Task<LibraryReportContext> LoadFineEvidenceAsync(LibraryReportExecutionScope scope, LibraryCharge[] charges)
	{
		var loanIds = charges.Where(row => row.BookIssueId.HasValue).Select(row => row.BookIssueId!.Value).Distinct().ToArray();
		var loans = loanIds.Length == 0 ? [] : await asyncExecuter.ToListAsync((await loanScope.GetReadableLoansAsync(scope.UserScope))
			.Where(row => row.TenantId == tenant.Id && Enumerable.Contains(loanIds, row.Id)));
		var copyIds = loans.Where(row => row.BookCopyId != Guid.Empty).Select(row => row.BookCopyId).Distinct().ToArray();
		var accessions = loans.Where(row => row.BookCopyId == Guid.Empty).Select(row => row.AccessionNumber.Trim().ToUpperInvariant())
			.Distinct(StringComparer.Ordinal).ToArray();
		var copies = loans.Count == 0 ? [] : await asyncExecuter.ToListAsync((await copyRepository.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id &&
				(Enumerable.Contains(copyIds, row.Id) || Enumerable.Contains(accessions, row.NormalizedAccessionNumber))));
		var bookIds = copies.Select(row => row.BookId).Distinct().ToArray();
		var books = bookIds.Length == 0 ? [] : await asyncExecuter.ToListAsync((await bookRepository.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && Enumerable.Contains(bookIds, row.Id)));
		var legacyCopies = copies.GroupBy(row => row.NormalizedAccessionNumber, StringComparer.Ordinal)
			.Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().BookId, StringComparer.Ordinal);
		var categoryIds = charges.Select(row => row.ChargeCategoryId).Distinct().ToArray();
		var categories = await asyncExecuter.ToListAsync((await chargeCategoryRepository.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && Enumerable.Contains(categoryIds, row.Id))
			.Select(row => new { row.Id, row.ChargeType }));
		var (members, aliases) = await LoadCurrentMembersAsync(scope, loans, [], charges, [], includeRosterContacts: false);
		return new(books, copies, loans, [], [], [], categories.ToDictionary(row => row.Id, row => row.ChargeType),
			[], [], members, legacyCopies, aliases);
	}

	private static Dictionary<string, string?> BuildFineRow(
		LibraryReportContext context, LibraryCharge charge, FinanceExternalSourcePosition position)
	{
		var personId = charge.MemberRole == 0 ? context.MemberAliases.GetValueOrDefault(charge.MemberId, charge.MemberId) : charge.MemberId;
		var member = context.Members.GetValueOrDefault(personId);
		var loan = charge.BookIssueId is Guid loanId ? context.IssueById.GetValueOrDefault(loanId) : null;
		var saved = loan == null ? null : LibraryLoanBorrowerSnapshot.Read(loan, personId);
		var chargeDate = LibraryLoanDates.IsConfirmed(charge.ChargeDateProvenance) ? charge.SchoolChargeDate : null;
		var dueDate = LibraryLoanDates.IsConfirmed(charge.DueDateProvenance) ? charge.SchoolDueDate : null;
		var amounts = position.Amounts;
		return Row(
			("chargeDate", Date(charge.ChargeDate)), ("dueDate", Date(charge.DueDate)), ("paidDate", Date(charge.PaidDate)),
			("schoolChargeDate", Date(chargeDate)), ("schoolDueDate", Date(dueDate)),
			("dateEvidence", chargeDate.HasValue && dueDate.HasValue ? "Confirmed school dates" :
				chargeDate.HasValue || dueDate.HasValue ? "Partly confirmed" : "Unconfirmed"),
			("memberName", saved?.MemberName ?? member?.Name), ("memberIdentifier", saved?.MemberIdentifier ?? member?.Identifier),
			("memberDisplaySource", saved != null ? "LoanSnapshot" : member != null ? "CurrentAuthorizedRecord" : "Unavailable"),
			("memberRole", charge.MemberRole == 0 ? "Student" : "Staff"), ("recordedMemberRole", charge.MemberRole.ToString(CultureInfo.InvariantCulture)),
			("chargeType", context.ChargeCategories.GetValueOrDefault(charge.ChargeCategoryId)), ("remarks", charge.Remarks),
			("bookTitle", loan == null ? null : context.ResolveBook(loan)?.Title), ("accessionNumber", loan?.AccessionNumber),
			("academicYearId", charge.AcademicYearId?.ToString()), ("amount", charge.Amount.ToString("G29", CultureInfo.InvariantCulture)),
			("paymentStatus", charge.PaymentStatus.ToString()), ("financeEvidence", position.Evidence.ToString()), ("currency", position.Currency),
			("originalAmountText", FinanceText(amounts?.Original)), ("acceptedAmountText", FinanceText(amounts?.Accepted)),
			("settledAmountText", FinanceText(amounts?.Settled)), ("pendingVerificationAmountText", FinanceText(amounts?.Pending)),
			("netWaivedAmountText", FinanceText(amounts?.NetWaived)), ("financialOutstandingAmountText", FinanceText(amounts?.FinancialOutstanding)),
			("collectibleAmountText", FinanceText(amounts?.Collectible)), ("refundedAmountText", FinanceText(amounts?.Refunded)));
	}

	private static string? FinanceText(decimal? amount) => amount?.ToString("F2", CultureInfo.InvariantCulture);
}
