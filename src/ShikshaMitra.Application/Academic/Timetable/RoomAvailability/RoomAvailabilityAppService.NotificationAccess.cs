using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
    internal async Task<bool> CanReceiveReviewNoticeAsync(RoomReservation booking, Guid requester, Guid reviewer,
        IReadOnlyCollection<string> roles)
    {
        // Approval and the reservation list both require current access to this booking.
        var privileged = IsPrivilegedRoomOperator(roles);
        var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeService>().GetUserScopeAsync(reviewer);
        if (scope.TenantId != booking.TenantId || (IsInstituteWideReservation(booking)
            ? !privileged && !scope.IsAdmin && !scope.HasAnyAccess
            : !UserScopeQueryExtensions.HasAccessToEntity(booking, scope)))
            return false;
        var visible = await GetVisibleReservationUserIdsAsync(reviewer);
        return CanReviewReservation(requester, reviewer, privileged, visible) &&
            (visible.Contains(requester) || IsAdministrator(roles) || await _classTeacherAccessService.HasAssignmentAsync(reviewer));
    }

    internal async Task<bool> CanViewNotificationSourceAsync(Guid tenantId, Guid sourceId, CancellationToken token)
    {
        var reservation = await _reservationRepository.FindAsync(x => x.Id == sourceId && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken: token);
        if (reservation == null) return false;
        var (visible, _) = await ReservationCohortAsync(null);
        visible = visible.Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (reservation.IsRecurring || !reservation.RecurringConfigId.HasValue)
            return await AsyncExecuter.AnyAsync(visible.Where(x => x.Id == sourceId), token);

        // A recurring occurrence is reached through its visible series. Match its own scope and owner as well.
        return await AsyncExecuter.AnyAsync(visible.Where(x => x.IsRecurring && x.RecurringConfigId == reservation.RecurringConfigId &&
            x.RoomId == reservation.RoomId && x.CreatorId == reservation.CreatorId && x.StandardId == reservation.StandardId &&
            x.DivisionId == reservation.DivisionId), token);
    }
}
