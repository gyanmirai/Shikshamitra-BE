using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;


public class EfCoreFeeHeadRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, FeeHead, Guid>(dbContextProvider), IFeeHeadRepository
{
    }
