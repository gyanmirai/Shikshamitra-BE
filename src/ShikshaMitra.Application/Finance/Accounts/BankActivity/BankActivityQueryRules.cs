using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Validation;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
public static class BankActivityQueryRules
{
	public static readonly IReadOnlyDictionary<string, LambdaExpression> Fields = new Dictionary<string, LambdaExpression>
	{
		["occurredAt"] = (Expression<Func<BankActivityFact, DateTimeOffset>>) (x => x.OccurredAt),
		["originalPaymentAt"] = (Expression<Func<BankActivityFact, DateTimeOffset?>>) (x => x.OriginalPaymentAt),
		["paymentReference"] = (Expression<Func<BankActivityFact, string?>>) (x => x.PaymentReference),
		["recordedBy"] = (Expression<Func<BankActivityFact, string?>>) (x => x.RecordedBy),
		["reference"] = (Expression<Func<BankActivityFact, string>>) (x => x.Reference),
		["source"] = (Expression<Func<BankActivityFact, string>>) (x => x.Source),
		["counterparty"] = (Expression<Func<BankActivityFact, string>>) (x => x.Counterparty),
		["categoryName"] = (Expression<Func<BankActivityFact, string>>) (x => x.CategoryName),
		["paymentMethod"] = (Expression<Func<BankActivityFact, int>>) (x => x.PaymentMethod),
		["incoming"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.Incoming),
		["outgoing"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.Outgoing),
		["pendingAmount"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.PendingAmount),
		["recognitionStatus"] = (Expression<Func<BankActivityFact, int>>) (x => x.RecognitionStatus),
		["bankConfirmationStatus"] = (Expression<Func<BankActivityFact, int>>) (x => x.BankConfirmationStatus),
		["depositDate"] = (Expression<Func<BankActivityFact, DateTimeOffset?>>) (x => x.DepositDate),
		["bankCreditAt"] = (Expression<Func<BankActivityFact, DateTimeOffset?>>) (x => x.BankCreditAt),
		["depositAmount"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.DepositAmount),
		["creditedAmount"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.CreditedAmount),
		["inTransitAmount"] = (Expression<Func<BankActivityFact, decimal>>) (x => x.InTransitAmount)
	};
	private static readonly IReadOnlyDictionary<string, FinanceTableColumn> Columns = Fields.ToDictionary(x => x.Key, x => new FinanceTableColumn(
	 x.Value.ReturnType == typeof(string) ? FinanceTableValueType.Text :
	 x.Value.ReturnType == typeof(DateTimeOffset) || x.Value.ReturnType == typeof(DateTimeOffset?) ? FinanceTableValueType.Date :
	 x.Value.ReturnType == typeof(int) ? FinanceTableValueType.Enum : FinanceTableValueType.Number, Values: x.Key switch
	 { "paymentMethod" => Enum.GetValues<PaymentMethodEnum>().Select(v => (int) v).ToArray(), "recognitionStatus" => [0, 1, 2, 3], "bankConfirmationStatus" => [0, 1, 2], _ => null }));
	public static FinanceTableQuery Validate(BankActivityQuery input)
	{
		if (input.AcademicYearId == Guid.Empty || input.Page < 1 || input.PageSize is < 1 or > 100 || (long) (input.Page - 1) * input.PageSize > int.MaxValue ||
		 input.Search?.Length > 100 || input.Source is not (null or "income" or "expense" or "collection" or "refund" or "cash-deposit") ||
		 input.Direction is not (null or "incoming" or "outgoing") || input.Panel is not (null or "activity" or "deposits") ||
		 input.PaymentMethod.HasValue && !Enum.IsDefined(typeof(PaymentMethodEnum), input.PaymentMethod.Value) || input.Status is < 0 or > 3 ||
		 input.CategoryId == Guid.Empty || input.From.HasValue && input.ToExclusive.HasValue && (input.From >= input.ToExclusive || input.ToExclusive - input.From > TimeSpan.FromDays(36600)))
			throw new BusinessException(BankActivityErrorCodes.InvalidQuery);
		try { return FinanceTableQuery.Parse(input.Filters, input.Search, input.SortBy, input.SortOrder, Columns); }
		catch (AbpValidationException) { throw new BusinessException(BankActivityErrorCodes.InvalidQuery); }
	}
	public static IQueryable<BankActivityFact> AuthorizedSlices(IQueryable<BankActivityFact> facts, bool income, bool expense, bool cash, bool provisional)
	 => facts.Where(x => ((income && (x.Source == "income" || x.Source == "collection")) || (expense && (x.Source == "expense" || x.Source == "refund")) || (cash && x.Source == "cash-deposit")) &&
	  (provisional || x.Source != "collection" || x.RecognitionStatus != (int) BankActivityRecognitionStatus.Pending));
	public static IQueryable<BankActivityFact> Scope(IQueryable<BankActivityFact> facts, IQueryable<Guid> students, IQueryable<Guid> accounts, bool manual)
	 => facts.Where(x => x.FinanceFeeAccountId.HasValue ? accounts.Contains(x.FinanceFeeAccountId.Value) : x.StudentId.HasValue ? students.Contains(x.StudentId.Value) : manual);
	public static IQueryable<BankActivitySummary> Aggregates(IQueryable<BankActivityFact> query) => query.GroupBy(x => 1).Select(g => new BankActivitySummary
	{ Incoming = g.Sum(x => x.Incoming), Outgoing = g.Sum(x => x.Outgoing), Net = g.Sum(x => x.Incoming - x.Outgoing), PendingIncoming = g.Sum(x => x.PendingAmount), InTransit = g.Sum(x => x.InTransitAmount) });
	public static IQueryable<BankActivityFact> Filter(IQueryable<BankActivityFact> query, BankActivityQuery input, FinanceTableQuery table)
	{
		var from = input.From!.Value.ToUniversalTime();
		var toExclusive = input.ToExclusive!.Value.ToUniversalTime();
		query = query.Where(x => x.Panel == (input.Panel ?? "activity") && x.OccurredAt >= from && x.OccurredAt < toExclusive);
		if (input.Source != null) query = query.Where(x => x.Source == input.Source);
		if (input.Direction == "incoming") query = query.Where(x => x.Incoming > 0 || x.PendingAmount > 0 || x.InTransitAmount > 0);
		if (input.Direction == "outgoing") query = query.Where(x => x.Outgoing > 0);
		if (input.PaymentMethod.HasValue) query = query.Where(x => x.PaymentMethod == input.PaymentMethod.Value);
		if (input.Status.HasValue) query = query.Where(x => x.RecognitionStatus == input.Status.Value);
		if (input.CategoryId.HasValue) { var id = input.CategoryId.Value.ToString(); query = query.Where(x => x.CategoryIds.Contains(id)); }
		if (!string.IsNullOrWhiteSpace(input.Search)) { var search = input.Search.Trim().ToLowerInvariant(); query = query.Where(x => x.Reference.ToLower().Contains(search) || x.Counterparty.ToLower().Contains(search) || x.CategoryName.ToLower().Contains(search)); }
		return table.Apply(query, Fields);
	}
	public static IQueryable<BankActivityFact> Order(IQueryable<BankActivityFact> query, FinanceTableQuery table)
	 => ((IOrderedQueryable<BankActivityFact>) table.Order(query, Fields, "occurredAt", true, x => x.SourceId)).ThenBy(x => x.Key);
}
