using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class ProfessionalTaxAppService(
	IProfessionalTaxRepository professionalTaxRepository,
	PayrollStatutoryRuleInvalidator statutoryRuleInvalidator)
	: CrudAppService<ProfessionalTax, ProfessionalTaxDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProfessionalTaxDto>(professionalTaxRepository),
		IProfessionalTaxAppService
{
	public override async Task<ProfessionalTaxDto> CreateAsync(CreateUpdateProfessionalTaxDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await EnsureNoOverlapAsync(input);
		var entity = new ProfessionalTax(
			GuidGenerator.Create(), CurrentTenant.Id, input.JurisdictionCode, input.EffectiveFrom,
			input.EffectiveTo, input.FromSalary, input.ToSalary, input.TaxAmount, input.IsActive,
			input.DeductInCessationMonth);
		await Repository.InsertAsync(entity, autoSave: true);
		await statutoryRuleInvalidator.InvalidateProfessionalTaxAsync();
		return ObjectMapper.Map<ProfessionalTax, ProfessionalTaxDto>(entity);
	}

	public override async Task<ProfessionalTaxDto> UpdateAsync(Guid id, CreateUpdateProfessionalTaxDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await EnsureNoOverlapAsync(input, id);
		var entity = await Repository.GetAsync(id);
		entity.Update(
			input.JurisdictionCode, input.EffectiveFrom, input.EffectiveTo, input.FromSalary,
			input.ToSalary, input.TaxAmount, input.IsActive, input.DeductInCessationMonth);
		await Repository.UpdateAsync(entity, autoSave: true);
		await statutoryRuleInvalidator.InvalidateProfessionalTaxAsync();
		return ObjectMapper.Map<ProfessionalTax, ProfessionalTaxDto>(entity);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await base.DeleteAsync(id);
		await statutoryRuleInvalidator.InvalidateProfessionalTaxAsync();
	}

	private async Task EnsureNoOverlapAsync(CreateUpdateProfessionalTaxDto input, Guid? excludedId = null)
	{
		if (!input.IsActive)
		{
			return;
		}

		var jurisdiction = input.JurisdictionCode.Trim().ToUpperInvariant();
		var effectiveEnd = input.EffectiveTo ?? DateTime.MaxValue;
		var salaryEnd = input.ToSalary ?? decimal.MaxValue;
		var overlappingSlabs = (await Repository.GetQueryableAsync()).Where(x =>
			x.Id != excludedId && x.IsActive && x.JurisdictionCode == jurisdiction &&
			x.EffectiveFrom <= effectiveEnd && input.EffectiveFrom <= (x.EffectiveTo ?? DateTime.MaxValue) &&
			x.FromSalary <= salaryEnd && input.FromSalary <= (x.ToSalary ?? decimal.MaxValue));
		if (await AsyncExecuter.AnyAsync(overlappingSlabs))
		{
			throw new BusinessException("HR:Payroll:ProfessionalTaxSlabOverlap")
				.WithData("jurisdictionCode", jurisdiction);
		}
	}
}
