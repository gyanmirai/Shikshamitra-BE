using System;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Communication.Announcements
{
    public class AnnouncementRepository : EfCoreRepository<ShikshaMitraDbContext, Announcement, Guid>, IAnnouncementRepository
    {
        public AnnouncementRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }
}
