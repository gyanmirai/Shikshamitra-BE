using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Authors;

public class AuthorDto : EntityDto<Guid>
{
    public string Name { get; set; }

    public DateTime BirthDate { get; set; }
}