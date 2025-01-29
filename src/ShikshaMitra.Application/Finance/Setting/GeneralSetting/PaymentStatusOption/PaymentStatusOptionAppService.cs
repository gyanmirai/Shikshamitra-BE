using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public class PaymentStatusOptionAppService(IPaymentStatusOptionRepository paymentStatusOptionRepository) :CrudAppService<PaymentStatusOption,  PaymentStatusOptionDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePaymentStatusOptionDto>(paymentStatusOptionRepository),IPaymentStatusOptionAppService
{
}
