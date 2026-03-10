using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestRepository : IRepository<ConcessionRequest, Guid>
{
}
