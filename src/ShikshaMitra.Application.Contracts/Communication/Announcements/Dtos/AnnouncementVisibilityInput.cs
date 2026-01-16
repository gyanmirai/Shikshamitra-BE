using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Communication.Announcements.Dtos
{
    public class AnnouncementVisibilityInput
    {
        [Required]
        public bool IsVisible { get; set; }
    }
}
