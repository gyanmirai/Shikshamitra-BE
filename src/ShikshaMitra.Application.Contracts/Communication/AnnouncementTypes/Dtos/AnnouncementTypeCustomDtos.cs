using System.Collections.Generic;

namespace ShikshaMitra.Communication.AnnouncementTypes.Dtos
{
    public class AnnouncementTypeListResultDto<T>
    {
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public List<T> Items { get; set; }
        public int CurrentPage { get; set; }
        public string TotalCount { get; set; }

        public AnnouncementTypeListResultDto(List<T> items, long totalCount, int currentPage, int pageSize)
        {
            Items = items;
            TotalCount = totalCount.ToString();
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
        }
    }

    public class MessageDto
    {
        public string Message { get; set; }
    }
}
