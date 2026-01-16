using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
    public interface IAnnouncementTypeRepository : IRepository<AnnouncementType, Guid>
    {
    }
}
