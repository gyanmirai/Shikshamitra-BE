using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Portal;

/// <summary>Internal canonical values for an already verified portal record. Never a remote API.</summary>
[Volo.Abp.RemoteService(false)]
public class PortalStudentProfileSectionReader(StudentRecordCurrentRuleReader general,
	IRepository<StudentFamilyDetail, Guid> family, IRepository<StudentCommunicationDetail, Guid> communication,
	IRepository<StudentAdditionalDetail, Guid> additional, IRepository<StudentMedicalRecord, Guid> medical,
	IRepository<StudentBankDetail, Guid> bank, IRepository<StudentPreviousSchoolDetail, Guid> previous)
	: ApplicationService, ITransientDependency
{
	internal async Task<JsonObject> ReadAsync(Guid studentId, string section)
	{
		if (section == "general") return await general.ReadBasicGeneralAsync(studentId);
		object? value = section switch
		{
			"family" => await family.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			"communication" => await communication.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			"additional" => await additional.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			"medical" => await medical.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			"bank" => await bank.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			"previous-school" => await previous.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id),
			_ => null
		};
		// Only schema-bound property paths are projected from this internal object into the response.
		return value == null ? new() : JsonSerializer.SerializeToNode(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
	}
}
