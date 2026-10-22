using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionSeatCommandService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionSeatPool, Guid> pools,
	IRepository<AdmissionSeatReservation, Guid> reservations,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionSeatAccessService access,
	AdmissionSeatReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionSeatStageDto> ReserveAsync(Guid applicationId, Guid stageId, ReserveAdmissionSeatDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var reason = input.OverrideCapacity
			? Required(input.Reason, "Enter a reason for overriding seat capacity.") : Optional(input.Reason);
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("reserve", new { input.ExpiresAt, input.JoinWaitlistWhenFull, input.OverrideCapacity, Reason = reason });
		if (await IsRetryAsync(context, key, "seat-reserve", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var pool = await GetOrCreatePoolAsync(context);
		await ReconcileExpiredAsync(pool);
		var reservation = await reservations.FindAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId);
		if (reservation != null) EnsureReservationConcurrency(reservation, input.ReservationConcurrencyStamp);
		if (reservation?.Status == AdmissionSeatReservationStatus.Confirmed)
			throw Invalid("A confirmed seat cannot be reserved again.");

		if (pool.OccupiedCount >= pool.Capacity && !input.OverrideCapacity)
		{
			if (!input.JoinWaitlistWhenFull || context.Policy!.AllowWaitlist != true)
				throw new BusinessException(AdmissionSeatStageErrorCodes.CapacityFull);
			return await WaitlistInternalAsync(context, pool, reservation, reason, key, fingerprint, "seat-reserve");
		}

		var expiresAt = Expiry(context.Policy!, input.ExpiresAt);
		pool.Reserve(input.OverrideCapacity);
		if (reservation == null)
		{
			reservation = new AdmissionSeatReservation(GuidGenerator.Create(), CurrentTenant.Id, pool.Id,
				applicationId, stageId, AdmissionSeatReservationStatus.Reserved, Clock.Now, expiresAt, CurrentUser.Id, reason);
			await reservations.InsertAsync(reservation, false);
		}
		else
		{
			reservation.Reserve(Clock.Now, expiresAt, CurrentUser.Id, reason);
			await reservations.UpdateAsync(reservation, false);
		}
		return await SaveAsync(context, pool, "seat-reserve", key, fingerprint, reason,
			new { expiresAt, input.OverrideCapacity });
	}

	public async Task<AdmissionSeatStageDto> ConfirmAsync(Guid applicationId, Guid stageId, ConfirmAdmissionSeatDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var reason = Optional(input.Reason);
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("confirm", new { Reason = reason });
		if (await IsRetryAsync(context, key, "seat-confirm", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var pool = await RequiredPoolAsync(context);
		await ReconcileExpiredAsync(pool);
		var reservation = await RequiredReservationAsync(context);
		EnsureReservationConcurrency(reservation, input.ReservationConcurrencyStamp);
		pool.Confirm();
		reservation.Confirm(Clock.Now, CurrentUser.Id, reason);
		await reservations.UpdateAsync(reservation, false);
		return await SaveAsync(context, pool, "seat-confirm", key, fingerprint, reason, null);
	}

	public async Task<AdmissionSeatStageDto> RenewAsync(Guid applicationId, Guid stageId, RenewAdmissionSeatDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		var reason = Optional(input.Reason);
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("renew", new { input.ExpiresAt, Reason = reason });
		if (await IsRetryAsync(context, key, "seat-renew", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (context.Policy?.AllowRenewal != true)
			throw new BusinessException(AdmissionSeatStageErrorCodes.RenewalNotAllowed);
		var pool = await RequiredPoolAsync(context);
		await ReconcileExpiredAsync(pool);
		var reservation = await RequiredReservationAsync(context);
		EnsureReservationConcurrency(reservation, input.ReservationConcurrencyStamp);
		var expiresAt = Expiry(context.Policy, input.ExpiresAt);
		reservation.Renew(Clock.Now, expiresAt, context.Policy.MaximumRenewals, CurrentUser.Id, reason);
		await reservations.UpdateAsync(reservation, false);
		return await SaveAsync(context, pool, "seat-renew", key, fingerprint, reason, new { expiresAt });
	}

	public async Task<AdmissionSeatStageDto> ReleaseAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input)
	{
		var context = await ManagedAsync(applicationId, stageId, requireActive: false);
		var reason = context.Policy!.RequireReleaseReason
			? Required(input.Reason, "Enter a reason for releasing the seat.") : Optional(input.Reason);
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("release", new { Reason = reason });
		if (await IsRetryAsync(context, key, "seat-release", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var pool = await RequiredPoolAsync(context);
		await ReconcileExpiredAsync(pool);
		var reservation = await RequiredReservationAsync(context);
		EnsureReservationConcurrency(reservation, input.ReservationConcurrencyStamp);
		if (!context.IsActive && (reservation.Status != AdmissionSeatReservationStatus.Confirmed ||
			context.Application.Status == AdmissionApplicationStatus.AdmissionFinalized))
			throw Invalid("Only a confirmed seat can be released after leaving the Seat stage and before finalization.");
		pool.Release(reservation.Status);
		reservation.Release(Clock.Now, CurrentUser.Id, reason);
		await reservations.UpdateAsync(reservation, false);
		return await SaveAsync(context, pool, "seat-release", key, fingerprint, reason, null);
	}

	public async Task<AdmissionSeatStageDto> WaitlistAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		if (context.Policy?.AllowWaitlist != true)
			throw new BusinessException(AdmissionSeatStageErrorCodes.WaitlistNotAllowed);
		var reason = Optional(input.Reason);
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("waitlist", new { Reason = reason });
		if (await IsRetryAsync(context, key, "seat-waitlist", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var pool = await GetOrCreatePoolAsync(context);
		await ReconcileExpiredAsync(pool);
		var reservation = await reservations.FindAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId);
		if (reservation != null) EnsureReservationConcurrency(reservation, input.ReservationConcurrencyStamp);
		return await WaitlistInternalAsync(context, pool, reservation, reason, key, fingerprint, "seat-waitlist");
	}

	public async Task<AdmissionSeatStageDto> UpdateCapacityAsync(Guid applicationId, Guid stageId, UpdateAdmissionSeatCapacityDto input)
	{
		var context = await ManagedAsync(applicationId, stageId);
		if (context.Policy?.AllowCapacityOverride != true)
			throw new BusinessException(AdmissionSeatStageErrorCodes.CapacityInvalid);
		var reason = Required(input.Reason, "Enter a reason for changing seat capacity.");
		var key = Key(input.IdempotencyKey);
		var fingerprint = Fingerprint("capacity", new { input.Capacity, input.AllowBelowOccupied, Reason = reason });
		if (await IsRetryAsync(context, key, "seat-capacity", fingerprint)) return await reader.GetAsync(applicationId, stageId);
		AdmissionSeatAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var existingPool = await pools.FindAsync(item => item.AdmissionOfferingId == context.Application.AdmissionOfferingId);
		var pool = existingPool ?? await GetOrCreatePoolAsync(context);
		if (existingPool != null) AdmissionSeatAccessService.EnsureConcurrency(pool, input.PoolConcurrencyStamp);
		await ReconcileExpiredAsync(pool);
		pool.SetCapacity(input.Capacity, input.AllowBelowOccupied);
		return await SaveAsync(context, pool, "seat-capacity", key, fingerprint, reason,
			new { input.Capacity, input.AllowBelowOccupied });
	}

	private async Task<AdmissionSeatStageDto> WaitlistInternalAsync(
		AdmissionSeatContext context, AdmissionSeatPool pool, AdmissionSeatReservation? reservation,
		string? reason, string key, string fingerprint, string action)
	{
		if (context.Policy?.AllowWaitlist != true)
			throw new BusinessException(AdmissionSeatStageErrorCodes.WaitlistNotAllowed);
		if (reservation?.Status == AdmissionSeatReservationStatus.Confirmed)
			throw Invalid("A confirmed seat cannot be moved to the waitlist.");
		if (reservation?.Status == AdmissionSeatReservationStatus.Reserved) pool.Release(reservation.Status);
		if (reservation == null)
		{
			reservation = new AdmissionSeatReservation(GuidGenerator.Create(), CurrentTenant.Id, pool.Id,
				context.Application.Id, context.Stage.Id, AdmissionSeatReservationStatus.Waitlisted,
				Clock.Now, null, CurrentUser.Id, reason);
			await reservations.InsertAsync(reservation, false);
		}
		else
		{
			reservation.Waitlist(CurrentUser.Id, reason);
			await reservations.UpdateAsync(reservation, false);
		}
		return await SaveAsync(context, pool, action == "seat-reserve" ? "seat-waitlist" : action,
			key, fingerprint, reason, new { RequestedAction = action });
	}

	private async Task ReconcileExpiredAsync(AdmissionSeatPool pool)
	{
		var expired = await reservations.GetListAsync(item => item.PoolId == pool.Id &&
			item.Status == AdmissionSeatReservationStatus.Reserved && item.ExpiresAt <= Clock.Now);
		if (expired.Count == 0) return;
		pool.ExpireReserved(expired.Count);
		foreach (var reservation in expired)
		{
			reservation.Expire(Clock.Now);
			await reservations.UpdateAsync(reservation, false);
			var application = await applications.GetAsync(reservation.ApplicationId);
			var key = $"system:expiry:{reservation.Id:N}";
			if (await history.FindAsync(item => item.ApplicationId == reservation.ApplicationId &&
				item.StageInstanceId == reservation.StageInstanceId && item.IdempotencyKey == key) == null)
				await history.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
					reservation.ApplicationId, application.Status, "Reservation expired.", null,
					"seat-expire", reservation.StageInstanceId, key), false);
		}
	}

	private async Task<AdmissionSeatStageDto> SaveAsync(
		AdmissionSeatContext context, AdmissionSeatPool pool, string action, string key,
		string fingerprint, string? reason, object? details)
	{
		var metadataJson = JsonSerializer.Serialize(new
		{
			RequestFingerprint = fingerprint, Seat = context.Policy, Details = details
		});
		await applications.UpdateAsync(context.Application, false);
		await history.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
			context.Application.Id, context.Application.Status, reason, CurrentUser.Id, action,
			context.Stage.Id, key, metadataJson), false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity($"AdmissionSeat{string.Concat(action.Split('-').Skip(1).Select(value => char.ToUpperInvariant(value[0]) + value[1..]))}",
			$"{context.Stage.Name}: {action.Replace('-', ' ')} recorded.", context.Application.ApplicantId,
			context.Application.Id, metadataJson: metadataJson), false);
		try { await CurrentUnitOfWork!.SaveChangesAsync(); }
		catch (AbpDbConcurrencyException)
		{ throw new BusinessException(AdmissionSeatStageErrorCodes.ConcurrencyConflict); }
		catch (Exception exception) when (IsSeatUniquenessConflict(exception))
		{ throw new BusinessException(AdmissionSeatStageErrorCodes.ConcurrencyConflict); }
		return await reader.GetAsync(context.Application.Id, context.Stage.Id);
	}

	private async Task<AdmissionSeatContext> ManagedAsync(Guid applicationId, Guid stageId, bool requireActive = true)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		if (context.Policy == null || (requireActive && !context.IsActive)) throw Invalid("The managed Seat stage must be active.");
		return context;
	}

	private async Task<AdmissionSeatPool> GetOrCreatePoolAsync(AdmissionSeatContext context)
	{
		var pool = await pools.FindAsync(item => item.AdmissionOfferingId == context.Application.AdmissionOfferingId);
		if (pool != null) return pool;
		pool = new AdmissionSeatPool(GuidGenerator.Create(), CurrentTenant.Id,
			context.Application.AdmissionOfferingId, context.Policy!.DefaultCapacity);
		await pools.InsertAsync(pool, false);
		return pool;
	}

	private async Task<AdmissionSeatPool> RequiredPoolAsync(AdmissionSeatContext context) =>
		await pools.FindAsync(item => item.AdmissionOfferingId == context.Application.AdmissionOfferingId) ??
		throw Invalid("Reserve or waitlist the applicant before continuing.");

	private async Task<AdmissionSeatReservation> RequiredReservationAsync(AdmissionSeatContext context) =>
		await reservations.FindAsync(item => item.ApplicationId == context.Application.Id &&
			item.StageInstanceId == context.Stage.Id) ?? throw Invalid("No seat allocation exists for this application.");

	private async Task<bool> IsRetryAsync(AdmissionSeatContext context, string key, string action, string fingerprint)
	{
		var existing = await history.FindAsync(item => item.ApplicationId == context.Application.Id &&
			item.StageInstanceId == context.Stage.Id && item.IdempotencyKey == key);
		if (existing == null) return false;
		var matchingAction = existing.Action == action ||
			(action == "seat-reserve" && existing.Action == "seat-waitlist");
		if (matchingAction && ReadFingerprint(existing.MetadataJson) == fingerprint) return true;
		throw new BusinessException(AdmissionSeatStageErrorCodes.IdempotencyConflict);
	}

	private DateTime Expiry(AdmissionSeatStagePolicy policy, DateTime? requested)
	{
		var now = Clock.Now;
		var expiresAt = requested?.ToUniversalTime() ?? now.AddHours(policy.ReservationDurationHours);
		var hours = (expiresAt - now).TotalHours;
		if (expiresAt <= now || hours < policy.MinimumDurationHours - 0.01 || hours > policy.MaximumDurationHours + 0.01)
			throw new BusinessException(AdmissionSeatStageErrorCodes.DurationInvalid);
		if (!policy.AllowDurationOverride && requested.HasValue &&
			Math.Abs(hours - policy.ReservationDurationHours) > 0.01)
			throw new BusinessException(AdmissionSeatStageErrorCodes.DurationInvalid);
		return expiresAt;
	}

	private static void EnsureReservationConcurrency(AdmissionSeatReservation reservation, string? stamp) =>
		AdmissionSeatAccessService.EnsureConcurrency(reservation, stamp);
	private static string Key(string? value) => Required(value, "Enter an idempotency key.", 80);
	private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Required(value, "Seat note cannot exceed 512 characters.");
	private static string Required(string? value, string message, int max = 512) =>
		string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw Invalid(message) : value.Trim();
	private static string Fingerprint(string action, object payload) => Convert.ToHexString(
		SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { action, payload })))).ToLowerInvariant();
	private static string? ReadFingerprint(string? metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata)) return null;
		using var json = JsonDocument.Parse(metadata);
		return json.RootElement.TryGetProperty("RequestFingerprint", out var value) ? value.GetString() : null;
	}
	private static bool IsSeatUniquenessConflict(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			if (current.Message.Contains("UX_AdmissionSeatPools_Offering", StringComparison.Ordinal) ||
				current.Message.Contains("UX_AdmissionSeatReservations_ApplicationStage", StringComparison.Ordinal) ||
				current.Message.Contains("AppAdmissionSeatPools.TenantId, AppAdmissionSeatPools.AdmissionOfferingId", StringComparison.Ordinal) ||
				current.Message.Contains("AppAdmissionSeatReservations.TenantId, AppAdmissionSeatReservations.ApplicationId, AppAdmissionSeatReservations.StageInstanceId", StringComparison.Ordinal))
				return true;
		}
		return false;
	}
	private static BusinessException Invalid(string message) => new(AdmissionSeatStageErrorCodes.InvalidState, message);
}
