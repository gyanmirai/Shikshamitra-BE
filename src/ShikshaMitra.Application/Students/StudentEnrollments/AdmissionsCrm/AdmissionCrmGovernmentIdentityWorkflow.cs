using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGovernmentIdentityWorkflow(
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	AdmissionCrmIdentityEntityStore entityStore,
	AdmissionCrmIdentityReadinessWorkflow readinessWorkflow,
	IStringEncryptionService encryptionService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPersonDto> UpdateAsync(Guid personId, UpdateAdmissionCrmPersonIdentityDto input)
	{
		var person = await entityStore.GetPersonAsync(personId);
		var aadhaar = AdmissionCrmIdentityMapper.NormalizeContact(input.AadhaarNo);
		if (!string.IsNullOrWhiteSpace(aadhaar) && aadhaar.Length != 12)
			throw new BusinessException("AdmissionCrm:AadhaarInvalid");
		if (!string.IsNullOrWhiteSpace(aadhaar))
		{
			var hash = AdmissionCrmIdentityMapper.Sha256(aadhaar);
			var queryable = await personRepository.GetQueryableAsync();
			if (await AsyncExecuter.AnyAsync(queryable.Where(item =>
				item.TenantId == CurrentTenant.Id && item.Id != personId && item.AadhaarHash == hash)))
				throw new BusinessException("AdmissionCrm:AadhaarAlreadyLinkedToAnotherPerson");
			person.SetAadhaar(encryptionService.Encrypt(aadhaar), hash, aadhaar[^4..], input.AadhaarStatus);
		}
		person.SetGovernmentReadiness(
			input.PenNumber, input.PenStatus, input.ApaarId, input.ApaarConsentStatus,
			input.ApaarStatus, input.UdiseStudentCode, input.UdiseStatus);
		await personRepository.UpdateAsync(person, autoSave: true);
		await readinessWorkflow.RecomputeForPersonAsync(personId);
		return AdmissionCrmIdentityMapper.Map(person);
	}
}
