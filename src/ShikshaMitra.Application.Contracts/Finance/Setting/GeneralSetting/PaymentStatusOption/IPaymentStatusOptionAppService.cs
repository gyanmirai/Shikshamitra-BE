using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;


public interface IPaymentStatusOptionAppService : ICrudAppService<PaymentStatusOptionDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePaymentStatusOptionDto>
{
}
