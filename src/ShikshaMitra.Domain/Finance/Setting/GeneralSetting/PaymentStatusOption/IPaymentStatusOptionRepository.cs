using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public interface IPaymentStatusOptionRepository: IRepository<PaymentStatusOption, Guid>
{
}
