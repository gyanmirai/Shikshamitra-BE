using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Library.Reservations;

public partial class LibraryReservationCoordinator
{
	public async Task<LibraryReservationPreviewDto> PreviewAsync(Guid bookId, Guid memberId, Guid yearId,
		DateOnly requestedDate, DateOnly? expiryDate, Guid? excludedRequestId = null, Guid? pickupSectionId = null,
		Guid? reviewedPickupSectionId = null)
	{
		var result = new LibraryReservationPreviewDto();
		var book = await books.GetAsync(bookId);
		var facts = (await eligibility.ReadAsync([memberId])).GetValueOrDefault(memberId);
		if (facts == null)
		{
			result.BlockedReasons.Add("The borrower could not be established.");
			return result;
		}
		result.CanonicalMemberId = facts.PersonId;
		result.AcademicYearId = facts.Role == 0 ? facts.AcademicYearId : yearId;
		if (facts.Eligibility != LibraryReservationEligibility.Eligible) result.BlockedReasons.Add(facts.Reason ?? "Library membership requires review.");
		if (facts.Role == 0 && facts.AcademicYearId != yearId) result.BlockedReasons.Add("Use the borrower's actual academic year.");
		if (requestedDate == default) result.BlockedReasons.Add("Enter an explicit school request date.");
		if (expiryDate.HasValue && expiryDate.Value < requestedDate) result.BlockedReasons.Add("Reservation expiry must be on or after the request date.");
		var pickup = await ReadPickupAsync(pickupSectionId);
		if (pickupSectionId.HasValue && pickup == null) result.BlockedReasons.Add("Choose an active Library pickup location in this school.");
		if (requestedDate != default)
		{
			try
			{
				result.Policy = await ResolvePolicyAsync(book, pickup?.SectionId ?? reviewedPickupSectionId, facts, yearId, requestedDate);
				result.RequiresExplicitExpiry = IsManual(result.Policy);
				if (result.RequiresExplicitExpiry && !expiryDate.HasValue) result.BlockedReasons.Add("Enter an explicit reservation expiry date for these manual borrowing terms.");
				if (!result.RequiresExplicitExpiry) await calendar.EnsureConfiguredAsync(yearId);
			}
			catch (BusinessException error) { result.BlockedReasons.Add(error.Message); }
		}
		var aliases = facts.MemberIds.ToArray();
		var current = await AsyncExecuter.ToListAsync((await requests.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(!excludedRequestId.HasValue || row.Id != excludedRequestId.Value) &&
			(row.CanonicalMemberId == facts.PersonId || !row.CanonicalMemberId.HasValue && Enumerable.Contains(aliases, row.MemberId)) &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview)));
		if (current.Any(row => row.BookId == bookId)) result.BlockedReasons.Add("An active reservation already exists for this borrower and title.");
		if (result.Policy != null && current.Count >= result.Policy.Terms.ReservationLimit) result.BlockedReasons.Add("The borrower has reached the active reservation limit.");
		result.CanCreate = result.BlockedReasons.Count == 0;
		return result;
	}
}
