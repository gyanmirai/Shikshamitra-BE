using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
public class EmployeeDocumentConfigurationValidator(IDocumentSettingRepository documentSettingRepository)
	: ApplicationService, ITransientDependency
{
	public async Task EnsureExistsAsync(Guid documentNameId)
	{
		var setting = await documentSettingRepository.FindAsync(documentNameId);
		if (setting == null)
		{
			throw new BusinessException(HRErrorCodes.EmployeeDocumentConfigurationNotFound);
		}
	}
}
