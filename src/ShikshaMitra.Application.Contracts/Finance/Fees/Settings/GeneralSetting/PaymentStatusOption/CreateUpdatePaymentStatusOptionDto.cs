using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Settings.GeneralSetting.PaymentStatusOption;

public class CreateUpdatePaymentStatusOptionDto
{
	public string PaymentStatusOptionName { get; set; } = null!; // Retained mutation contract: initialized by request binding before validation.
}
