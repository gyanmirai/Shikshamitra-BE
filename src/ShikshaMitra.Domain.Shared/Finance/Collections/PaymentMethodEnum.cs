using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.FeeCollection;

public enum PaymentMethodEnum
{
	Cash,
	Cheque,
	DemandDraft,
	BankTransfer,
	CreditCard,
	UPI,
	OnlinePaymentGateway,
	WalletPrepaidCard,
}