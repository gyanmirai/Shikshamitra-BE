using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>Scalar cash read shapes, shared by execution and provider translation checks.</summary>
public static class CashManagementReadProjection
{
	public static IQueryable<CashListRead> Rows(IQueryable<CashCollection> query) =>
		query.Select(x => new CashListRead
		{
			Row = new CashRow
			{
				Id = x.Id, SourceId = x.SourceId, Reference = x.Reference, PayerName = x.PayerName,
				Amount = x.Amount, SpentAmount = x.SpentAmount, DepositedAmount = x.DepositedAmount,
				InTransitAmount = x.InTransitAmount,
				RemainingAmount = x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount,
				Status = x.Status, BankAccountId = x.BankAccountId, BankLabel = x.BankLabel,
				CollectedAt = x.CollectedAt, ApprovedAt = x.ApprovedAt, CreatorId = x.CollectedBy,
				ConcurrencyStamp = x.ConcurrencyStamp
			},
			ExtraProperties = x.ExtraProperties,
			Deposits = x.Deposits.Select(deposit => new CashDepositRead(deposit.Id, deposit.DepositDate)).ToList()
		});

	public static IQueryable<CashSourceOption> Sources(IQueryable<CashCollection> query) => query.Select(x =>
		new CashSourceOption(x.Id, x.Reference + " • " + x.PayerName,
			x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount, x.ConcurrencyStamp));
}

public sealed class CashListRead
{
	public CashRow Row { get; set; } = new();
	public ExtraPropertyDictionary ExtraProperties { get; set; } = new();
	public List<CashDepositRead> Deposits { get; set; } = [];
}

public sealed record CashDepositRead(Guid Id, DateTimeOffset DepositDate);
