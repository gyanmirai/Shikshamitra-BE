using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using System;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public class EfCorePaymentStatusOptionRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, PaymentStatusOption, Guid>(dbContextProvider), IPaymentStatusOptionRepository
{
}
