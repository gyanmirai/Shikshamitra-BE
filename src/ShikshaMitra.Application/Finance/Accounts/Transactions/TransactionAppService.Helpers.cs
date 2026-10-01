using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;

namespace ShikshaMitra.Finance.Accounts.Transactions;

public partial class TransactionAppService
{
	private static void EnsureTransactionType(Transaction transaction, TransactionType expected)
	{
		if (transaction.TransactionType != expected)
			throw new Volo.Abp.BusinessException("ShikshaMitra:TransactionTypeMismatch")
				.WithData("TransactionId", transaction.Id)
				.WithData("Expected", expected)
				.WithData("Actual", transaction.TransactionType);
	}

	private static TransactionListItem MapToListItem(Transaction transaction, TransactionCategory category) =>
		new()
		{
			Id           = transaction.Id,
			CategoryId   = transaction.CategoryId,
			CategoryName = category.Name,
			CategoryTags = ToTagValues(transaction.CategoryTags),
			Name         = transaction.Name,
			Amount       = transaction.Amount,
			PaymentMethod = transaction.PaymentMethod,
			PaymentDate  = transaction.PaymentDate,
			Notes        = transaction.Notes,
			CreatedAt    = transaction.CreationTime,
		};

	private static TransactionItem MapToTransactionItem(Transaction transaction) =>
		new()
		{
			TransactionId = transaction.Id,
			IncomeName    = transaction.Name,
			ReceivedDate  = transaction.PaymentDate,
			PaymentMethod = transaction.PaymentMethod,
			Amount        = transaction.Amount,
		};

	private static TransactionCreateUpdateResponse MapToCreateUpdateResponse(
		Transaction transaction, TransactionCategory category) =>
		new()
		{
			Success = true,
			Data = new TransactionResponseData
			{
				Id           = transaction.Id,
				CategoryId   = transaction.CategoryId,
				CategoryName = category.Name,
				CategoryTags = ToTagValues(transaction.CategoryTags),
				Name         = transaction.Name,
				Amount       = transaction.Amount,
				PaymentMethod = transaction.PaymentMethod,
				PaymentDate  = transaction.PaymentDate,
				Notes        = transaction.Notes,
				CreatedAt    = transaction.CreationTime,
			},
		};

	private static List<string> ToTagValues(IEnumerable<Tag> tags) =>
		tags.Select(t => t.Value).ToList();
}
