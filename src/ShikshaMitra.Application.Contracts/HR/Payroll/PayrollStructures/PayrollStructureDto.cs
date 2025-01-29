using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructureDto : EntityDto<Guid>
{
	//public Guid EducationMediumStageId { get; set; }
	public Guid EmployeeId { get; set; }
	public Guid PayElementId { get; set; }
	public PayElementType PayElementType { get; set; }
	public double Amount { get; set; }

	public PayElementDto PayElement { get; set; }
	public EducationMediumStageDto EducationMediumStage { get; set; }
}