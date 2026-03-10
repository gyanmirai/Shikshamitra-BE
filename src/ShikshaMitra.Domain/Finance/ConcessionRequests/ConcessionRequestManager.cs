using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequestManager : DomainService
{
    private readonly IConcessionRequestRepository _concessionrequestRepository;

    public ConcessionRequestManager(IConcessionRequestRepository concessionrequestRepository)
    {
        _concessionrequestRepository = concessionrequestRepository;
    }
}
