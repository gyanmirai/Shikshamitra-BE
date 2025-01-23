using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Security.Permissions;

public class PermissionWithChildrenDto
{
	public string Name { get; set; }
	public string DisplayName { get; set; }
	public bool IsGranted { get; set; }
	public List<ProviderInfoDto> GrantedProviders { get; set; } = new List<ProviderInfoDto>();
	public List<PermissionWithChildrenDto> Children { get; set; } = new List<PermissionWithChildrenDto>();
}
