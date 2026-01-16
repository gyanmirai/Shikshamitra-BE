using System;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Communication.Announcements;

namespace ShikshaMitra.Communication.Announcements.Dtos
{
    public class GetAnnouncementListInput : PagedAndSortedResultRequestDto
    {
        public global::ShikshaMitra.AnnouncementStatus Status { get; set; }

        public Guid AcademicMediumId { get; set; }
        // Status is already defined above
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
