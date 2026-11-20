using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library;
using ShikshaMitra.Students;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalSettlementAppService
{
	private async Task<FinanceExternalCollectionFormDto> FormAsync(FinanceExternalSettlementContext context)
	{
		var binding = await BindingAsync(context);
		var receivable = await projection.ReadAsync(context.Link, context.Source);
		var form = new FinanceExternalCollectionFormDto
		{
			Receivable = receivable, Revision = await RevisionAsync(context, binding?.Id),
			SettlementPolicyBindingId = binding?.Id, CollectionPolicyVersionId = binding?.CollectionPolicyVersionId,
			RefundPolicyVersionId = binding?.RefundPolicyVersionId
		};
		if (binding == null) form.SetupIssues.Add("Confirm the Finance settlement policy before collecting this assessment.");
		else
		{
			await policyRules.ReadCollectionAsync(context, binding.CollectionPolicyVersionId);
			await policyRules.ValidateRefundReferenceAsync(context, binding.RefundPolicyVersionId);
			form.PaymentMethods = Enum.GetValues<PaymentMethodEnum>().ToList();
		}
		if (context.Charge.Status != ChargeStatus.Approved) form.SetupIssues.Add("Finance approval is required before collection.");
		form.CanCollect = form.SetupIssues.Count == 0 && receivable.OutstandingAmount > 0m;
		receivable.SettlementAvailable = form.CanCollect;
		receivable.SettlementBlockedReason = form.SetupIssues.Count == 0 ? null : string.Join(" ", form.SetupIssues);
		return form;
	}

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public Task<FinanceExternalCollectionDto> CollectAsync(FinanceExternalCollectInput input) =>
		CommandAsync(input, "Collect", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
			async (context, replay) =>
			{
				if (replay != null) return await CollectionResultAsync(context, replay.ResultEntityId!.Value);
				await EnsureRevisionAsync(context, input.Revision);
				var form = await FormAsync(context);
				if (!form.CanCollect || input.SchoolReceiptDate == default || input.Payments == null)
					throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
				var total = FinanceExternalSettlementAmounts.PaymentTotal(input.Payments.Select(row => row.Amount));
				if (total > form.Receivable.OutstandingAmount) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
				var category = await routing.RequireCategoryAsync(context.Link.AcademicYearId,
					context.Charge.IncomeCategoryId, TransactionType.Income);
				var payments = new List<FeeCollectionManager.ExternalPayment>();
				foreach (var payment in input.Payments)
				{
					if (!form.PaymentMethods.Contains(payment.PaymentMethod)) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
					var bank = payment.PaymentMethod == PaymentMethodEnum.Cash && !payment.BankAccountId.HasValue
						? null : await routing.RequireBankAsync(context.Link.AcademicYearId, payment.BankAccountId);
					payments.Add(new(payment.PaymentMethod, payment.Amount,
						FeeCollectionManager.MapPaymentDetails(payment.PaymentMethod, payment.Details), bank?.Id,
						bank == null ? null : $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}"));
				}
				var payer = new FinanceExternalPayerIdentity(context.Source.Payer.Kind,
					context.Source.Payer.StudentId ?? context.Source.Payer.EmployeeId!.Value);
				var collection = await collectionManager.CollectExternalAsync(context.Charge, payer, context.Link.Currency,
					input.SchoolReceiptDate, payments, input.IdempotencyKey.Trim(), Fingerprint(context, input), input.Notes, balances);
				var payerEvidence = await PayerEvidenceAsync(context);
				FinanceReceiptEvidence.CapturePayer(collection.Metadata, payerEvidence);
				var settlement = FinanceReceiptEvidence.FromBalance(collection.BalanceAfter, total);
				FinanceReceiptEvidence.CaptureSettlement(collection.Metadata, settlement);
				FinanceReceiptEvidence.CaptureSettlement(collection.Receipt, settlement);
				ReceiptCollectionSnapshot.Capture(collection.Metadata, [new(context.Charge.ChargeName,
					"Library assessment", context.Charge.Amount, collection.BalanceAfter.Settled,
					collection.BalanceAfter.Unsettled, total, true)]);
				for (var index = 0; index < payments.Count; index++)
				{
					var payment = payments[index];
					var instrument = collection.Payments[index];
					if (payment.Method == PaymentMethodEnum.Cash)
						await cashCollections.InsertAsync(new CashCollection(GuidGenerator.Create(), CurrentTenant.Id,
							context.Link.AcademicYearId, collection.Metadata.Id, $"collection:{collection.Metadata.Id:N}:{index}:",
							null, collection.Receipt.ReceiptNumber, payerEvidence.Name, payment.Amount, instrument.PaymentDate,
							context.ActorId, payer.StudentId, null, instrument.Id, payer.EmployeeId), autoSave: false);
					await transactions.RecordAsync(context.Link.AcademicYearId, category.Id,
						context.Charge.IncomeCategoryName ?? category.Name, payment.BankAccountId, payment.BankLabel,
						collection.Receipt.ReceiptNumber, payment.Amount, (int)payment.Method, TransactionType.Income,
						$"collection:{collection.Metadata.Id:N}:{index}:0", collection.Metadata.Id, payer.StudentId,
						confirmed: false, paymentDate: instrument.PaymentDate.UtcDateTime, employeeId: payer.EmployeeId);
				}
				await RecordAsync(context, input, "Collect", collection.Receipt.Id, "Receipt");
				return await CollectionResultAsync(context, collection.Receipt.Id);
			});

	private async Task<FinanceExternalCollectionDto> CollectionResultAsync(FinanceExternalSettlementContext context, Guid id)
	{
		var receipt = await receipts.GetAsync(id, includeDetails: false);
		if (receipt.TenantId != CurrentTenant.Id || receipt.AcademicYearId != context.Link.AcademicYearId ||
			receipt.StudentId != context.Source.Payer.StudentId || receipt.EmployeeId != context.Source.Payer.EmployeeId)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		return new() { CollectionId = receipt.FeeCollectionTransactionId, ReceiptId = receipt.Id,
			ReceiptNumber = receipt.ReceiptNumber, ReceiptKind = receipt.ReceiptKind,
			VerificationStatus = receipt.VerificationStatus, SchoolReceiptDate = receipt.ReceiptDate,
			AcceptedAmount = receipt.TotalPaidAmount, Receivable = await projection.ReadAsync(context.Link, context.Source) };
	}

	private async Task<FinanceReceiptPayerEvidence> PayerEvidenceAsync(FinanceExternalSettlementContext context)
	{
		if (context.Source.Payer.StudentId.HasValue)
		{
			var query = await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync();
			var name = await AsyncExecuter.SingleAsync(query.Where(row => row.TenantId == CurrentTenant.Id && row.Id == context.Source.Payer.StudentId)
				.Select(row => new { row.FirstName, row.LastName, row.GrNumber, row.GrNo }));
			return new(FinanceExternalPayerKind.Student, context.Source.Payer.StudentId.Value,
				string.Join(" ", new[] { name.FirstName, name.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
				name.GrNumber ?? name.GrNo?.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
		var employees = await LazyServiceProvider.LazyGetRequiredService<IRepository<Employee, Guid>>().GetQueryableAsync();
		var staff = await AsyncExecuter.SingleAsync(employees.Where(row => row.TenantId == CurrentTenant.Id && row.Id == context.Source.Payer.EmployeeId)
			.Select(row => new { row.FirstName, row.LastName, row.EmployeeCode }));
		return new(FinanceExternalPayerKind.Employee, context.Source.Payer.EmployeeId!.Value,
			string.Join(" ", new[] { staff.FirstName, staff.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))), staff.EmployeeCode);
	}
}
