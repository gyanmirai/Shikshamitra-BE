using System;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

public partial class EmployeeManager : DomainService
{
	private readonly EmployeeLookupReader _employeeLookupReader;
	private readonly EmployeeCreationManager _creationManager;
	private readonly EmployeeUpdateManager _updateManager;
	private readonly EmployeeFamilyInfoManager _familyInfoManager;

	public EmployeeManager(
		EmployeeLookupReader employeeLookupReader,
		EmployeeCreationManager creationManager,
		EmployeeUpdateManager updateManager,
		EmployeeFamilyInfoManager familyInfoManager)
	{
		_employeeLookupReader = employeeLookupReader;
		_creationManager = creationManager;
		_updateManager = updateManager;
		_familyInfoManager = familyInfoManager;
	}
}
