using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Communication.AnnouncementTypes.Dtos
{
    public class AnnouncementTypeCreateDto
    {
        [Required]
        public string AnnouncementType { get; set; }

        public string Description { get; set; }

        [Required]
        public string Color { get; set; }
    }
}
