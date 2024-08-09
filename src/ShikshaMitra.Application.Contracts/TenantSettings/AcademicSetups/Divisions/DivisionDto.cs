using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.Divisions;

public class DivisionDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }

	public int Strength { get; set; }

	public Guid StandardId { get; set; }
}