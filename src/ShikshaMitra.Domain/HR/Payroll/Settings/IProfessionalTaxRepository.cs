using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProfessionalTaxRepository : IRepository<ProfessionalTax, Guid>
{
	
}