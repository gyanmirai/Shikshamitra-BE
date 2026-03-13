using ShikshaMitra.EntityFrameworkCore;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EfCoreProvidentFundRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, ProvidentFund, Guid>(dbContextProvider), IProvidentFundRepository
{

}