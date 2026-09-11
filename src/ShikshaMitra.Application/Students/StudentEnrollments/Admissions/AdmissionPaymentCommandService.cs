using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionPaymentCommandService(
	IRepository<AdmissionPaymentTransaction, Guid> payments,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionPaymentAccessService access,
	AdmissionPaymentReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionPaymentStageDto> RecordAsync(Guid applicationId, Guid stageId, RecordAdmissionPaymentDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var key = Required(input.IdempotencyKey, 80, "Enter an idempotency key.");
		var existing = await payments.FindAsync(item => item.StageInstanceId == stageId && item.IdempotencyKey == key);
		if (existing != null)
		{
			if (existing.Amount != input.Amount || existing.Method != input.Method?.Trim() ||
				existing.ExternalReference != Optional(input.ExternalReference) || existing.Notes != Optional(input.Notes))
				throw Invalid("This idempotency key was already used for a different payment request.");
			return await reader.GetAsync(applicationId, stageId);
		}
		EnsureManagedActive(context);
		AdmissionPaymentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var state = await reader.GetAsync(applicationId, stageId);
		var policy = context.Policy!;
		var method = Required(input.Method, 32, "Choose a payment method.");
		if (!state.CanRecord) throw Invalid(state.Blockers.FirstOrDefault() ?? "A staff payment cannot be recorded now.");
		if (method == "onlineGateway" || !policy.AcceptedMethods.Contains(method, StringComparer.Ordinal))
			throw new BusinessException(AdmissionPaymentStageErrorCodes.MethodNotAllowed, "Choose an allowed staff payment method.");
		var available = state.OutstandingAmount - state.PendingAmount;
		EnsureAmount(policy, input.Amount, available);
		var rows = await payments.GetListAsync(item => item.StageInstanceId == stageId);
		var id = GuidGenerator.Create();
		var payment = AdmissionPaymentTransaction.RecordStaffPayment(id, CurrentTenant.Id, applicationId, stageId,
			rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1, input.Amount, policy.PurposeCode,
			policy.Currency, method, input.ExternalReference, Receipt(id), input.Notes, Snapshot(policy), key,
			Actor(), Clock.Now, policy.RequireIndependentVerification);
		await payments.InsertAsync(payment, false);
		return await SaveAsync(context, payment, "AdmissionPaymentRecorded");
	}

	public async Task<AdmissionPaymentStageDto> ReviewAsync(Guid transactionId, ReviewAdmissionPaymentDto input)
	{
		var payment = await payments.FindAsync(transactionId) ??
			throw new EntityNotFoundException(typeof(AdmissionPaymentTransaction), transactionId);
		var context = await access.GetContextAsync(payment.ApplicationId, payment.StageInstanceId);
		var key = Required(input.IdempotencyKey, 80, "Enter an idempotency key.");
		var operation = input.Approved ? "verify" : "reject";
		if (payment.IsOperationRetry(operation, key)) return await reader.GetAsync(payment.ApplicationId, payment.StageInstanceId);
		EnsureManagedActive(context);
		AdmissionPaymentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		AdmissionPaymentAccessService.EnsureConcurrency(payment, input.TransactionConcurrencyStamp);
		payment.Verify(input.Approved, input.Reason, key, Actor(), Clock.Now);
		await payments.UpdateAsync(payment, false);
		return await SaveAsync(context, payment, input.Approved ? "AdmissionPaymentVerified" : "AdmissionPaymentRejected");
	}

	public async Task<AdmissionPaymentStageDto> WaiveAsync(Guid applicationId, Guid stageId, AdmissionPaymentReasonDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var key = Required(input.IdempotencyKey, 80, "Enter an idempotency key.");
		var existing = await payments.FindAsync(item => item.StageInstanceId == stageId && item.IdempotencyKey == key);
		if (existing != null)
		{
			if (existing.Status != AdmissionPaymentStatuses.Waived || existing.DecisionReason != Required(input.Reason, 1024, "Enter a waiver reason."))
				throw Invalid("This idempotency key was already used for a different payment request.");
			return await reader.GetAsync(applicationId, stageId);
		}
		EnsureManagedActive(context);
		AdmissionPaymentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (context.Policy?.AllowWaiver != true) throw new BusinessException(AdmissionPaymentStageErrorCodes.WaiverNotAllowed,
			"This Payment stage does not allow waivers.");
		var state = await reader.GetAsync(applicationId, stageId);
		if (!state.CanWaive || state.SettledAmount > 0m || state.PendingAmount > 0m)
			throw Invalid("Reject or reverse existing payments before waiving the full amount.");
		var rows = await payments.GetListAsync(item => item.StageInstanceId == stageId);
		var payment = AdmissionPaymentTransaction.RecordWaiver(GuidGenerator.Create(), CurrentTenant.Id, applicationId,
			stageId, rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1, context.Policy.PurposeCode,
			context.Policy.Currency, input.Reason, Snapshot(context.Policy), key, Actor(), Clock.Now);
		await payments.InsertAsync(payment, false);
		return await SaveAsync(context, payment, "AdmissionPaymentWaived");
	}

	public async Task<AdmissionPaymentStageDto> ReverseAsync(Guid transactionId, AdmissionPaymentReasonDto input)
	{
		var payment = await payments.FindAsync(transactionId) ??
			throw new EntityNotFoundException(typeof(AdmissionPaymentTransaction), transactionId);
		var context = await access.GetContextAsync(payment.ApplicationId, payment.StageInstanceId);
		var key = Required(input.IdempotencyKey, 80, "Enter an idempotency key.");
		if (payment.IsOperationRetry("reverse", key)) return await reader.GetAsync(payment.ApplicationId, payment.StageInstanceId);
		EnsureManagedActive(context);
		AdmissionPaymentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		AdmissionPaymentAccessService.EnsureConcurrency(payment, input.TransactionConcurrencyStamp);
		payment.Reverse(input.Reason, key, Actor(), Clock.Now);
		await payments.UpdateAsync(payment, false);
		return await SaveAsync(context, payment, "AdmissionPaymentReversed");
	}

	private async Task<AdmissionPaymentStageDto> SaveAsync(AdmissionPaymentContext context, AdmissionPaymentTransaction payment, string action)
	{
		await applications.UpdateAsync(context.Application, false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, action,
			$"{context.Stage.Name}: {payment.Currency} {payment.Amount:0.00}, {payment.Status}.",
			context.Application.ApplicantId, context.Application.Id, metadataJson: JsonSerializer.Serialize(new
			{ PaymentId = payment.Id, payment.StageInstanceId, payment.AttemptNumber, payment.Method, payment.Status })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(context.Application.Id, context.Stage.Id);
	}

	private static void EnsureAmount(AdmissionPaymentStagePolicy policy, decimal amount, decimal available)
	{
		if (amount <= 0m || amount > available || decimal.Round(amount, 2) != amount)
			throw new BusinessException(AdmissionPaymentStageErrorCodes.AmountMismatch, $"Enter an amount up to {available:0.00}.");
		if (!policy.AllowInstallments && amount != available)
			throw new BusinessException(AdmissionPaymentStageErrorCodes.AmountMismatch, $"Collect the full remaining amount of {available:0.00}.");
		if (policy.AllowInstallments && amount < policy.MinimumInstallmentAmount && amount != available)
			throw new BusinessException(AdmissionPaymentStageErrorCodes.AmountMismatch,
				$"Installments must be at least {policy.MinimumInstallmentAmount:0.00}, except for the final balance.");
	}

	private Guid Actor() => CurrentUser.Id ?? throw Invalid("An authenticated staff member is required.");
	private static void EnsureManagedActive(AdmissionPaymentContext context)
	{
		if (context.Policy == null || !context.IsActive) throw Invalid("The managed Payment stage must be active.");
	}
	private static string Snapshot(AdmissionPaymentStagePolicy policy) => JsonSerializer.Serialize(new { payment = policy });
	private static string Receipt(Guid id) => $"ADM-{DateTime.UtcNow:yyyyMMdd}-{id:N}"[..23].ToUpperInvariant();
	private static string Required(string? value, int max, string message) =>
		string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw Invalid(message) : value.Trim();
	private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	private static BusinessException Invalid(string message) => new(AdmissionPaymentStageErrorCodes.InvalidState, message);
}
