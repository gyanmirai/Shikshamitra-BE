using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class EfCoreConcessionRequestRepository : EfCoreRepository<ShikshaMitraDbContext, ConcessionRequest, Guid>, IConcessionRequestRepository
{
    public EfCoreConcessionRequestRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }
}
