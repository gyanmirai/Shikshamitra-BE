using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library.Portal;

public partial class LibraryPortalAppService
{
	[Authorize(Actions.Reserve)]
	public async Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync(Guid studentId) =>
		await pickup.GetOptionsAsync((await authority.ResolveAsync(studentId, Actions.Reserve)).Tenant);

	[Authorize(Actions.Reserve)]
	public async Task<LibraryPortalReservePreview> PreviewReserveAsync(Guid studentId, LibraryPortalReserveInput input)
	{
		var owner = await authority.ResolveAsync(studentId, Actions.Reserve);
		return (await ReviewReservationAsync(owner, input)).Preview;
	}

	[Authorize(Actions.Reserve)]
	[UnitOfWork(true)]
	public async Task<LibraryPortalReservation> ReserveAsync(Guid studentId, LibraryPortalReserveInput input)
	{
		var tenant = await authority.RequireSchoolAsync(Actions.Reserve);
		await inventoryLock.AcquireAsync(tenant);
		var owner = await authority.ResolveAsync(studentId, Actions.Reserve);
		var plan = await ReviewReservationAsync(owner, input);
		if (!plan.Preview.CanRequest) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest,
			string.Join(" ", plan.Preview.Reasons));
		var request = await reservations.CreateAsync(input.BookId, owner.StudentId, owner.Facts.AcademicYearId!.Value, plan.Today!.Value,
			null, LibraryOperationActor.User(owner.Actor), pickupSectionId: input.PickupSectionId);
		await notifications.SendReservationConfirmedAsync(request, await books.GetAsync(request.BookId));
		return (await MapReservationsAsync(tenant, [request], canCancel: true)).Single();
	}

	[Authorize(Actions.Reserve)]
	[UnitOfWork(true)]
	public async Task<LibraryPortalReservation> CancelAsync(Guid studentId, Guid id, LibraryPortalCancelInput input)
	{
		var tenant = await authority.RequireSchoolAsync(Actions.Reserve);
		await inventoryLock.AcquireAsync(tenant);
		var owner = await authority.ResolveAsync(studentId, Actions.Reserve);
		var request = await AsyncExecuter.FirstOrDefaultAsync((await OwnedRequestsAsync(owner)).Where(row => row.Id == id))
			?? throw new AbpAuthorizationException();
		await reservations.CancelAsync(request, "Cancelled by the linked student or guardian.", input.Revision,
			LibraryOperationActor.User(owner.Actor));
		return (await MapReservationsAsync(tenant, [request], canCancel: true)).Single();
	}

	private async Task<(LibraryPortalReservePreview Preview, DateOnly? Today)> ReviewReservationAsync(
		LibraryPortalOwner owner, LibraryPortalReserveInput input)
	{
		if (owner.Facts.Eligibility != LibraryReservationEligibility.Eligible || !owner.Facts.AcademicYearId.HasValue)
			return (new(false, [owner.Facts.Reason ?? "Library membership requires review."]), null);
		var date = await ConfirmedSchoolDateAsync();
		if (!date.HasValue) return (new(false, ["Ask the library to record this request while school dates are set manually."]), null);
		var plan = await reservations.PreviewAsync(input.BookId, owner.StudentId, owner.Facts.AcademicYearId.Value,
			date.Value, null, pickupSectionId: input.PickupSectionId);
		return plan.RequiresExplicitExpiry
			? (new(false, ["Ask the library to record the expiry date for this request."]), date)
			: (new(plan.CanCreate, plan.BlockedReasons), date);
	}

	private async Task<DateOnly?> ConfirmedSchoolDateAsync()
	{
		var settings = await calendar.GetSettingsAsync();
		return settings.Source == "Library" && !settings.NeedsConfiguration ? await calendar.TodayAsync() : null;
	}
}
