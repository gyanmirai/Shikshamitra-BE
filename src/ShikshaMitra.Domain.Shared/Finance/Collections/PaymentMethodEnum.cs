using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.FeeCollection;

public enum PaymentMethodEnum
{
	Cash                = 0,
	Cheque              = 1,
	DemandDraft         = 2,
	BankTransfer        = 3,
	CreditCard          = 4,
	UPI                 = 5,
	OnlinePaymentGateway = 6,
	WalletPrepaidCard   = 7,
	/// <summary>Debit card (tap / swipe / online). Requires bank-receipt confirmation.</summary>
	DebitCard           = 8,
}