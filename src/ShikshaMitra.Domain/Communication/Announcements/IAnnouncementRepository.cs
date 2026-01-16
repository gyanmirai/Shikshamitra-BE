using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Communication.Announcements
{
    public interface IAnnouncementRepository : IRepository<Announcement, Guid>
    {
    }
}
