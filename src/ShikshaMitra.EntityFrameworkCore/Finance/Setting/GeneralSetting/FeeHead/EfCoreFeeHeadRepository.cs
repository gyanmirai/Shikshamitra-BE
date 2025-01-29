using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using System;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;


public class EfCoreFeeHeadRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, FeeHead, Guid>(dbContextProvider), IFeeHeadRepository
{
}
