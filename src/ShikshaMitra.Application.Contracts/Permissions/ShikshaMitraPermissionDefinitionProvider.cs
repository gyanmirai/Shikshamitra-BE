using ShikshaMitra.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ShikshaMitra.Permissions;

public class ShikshaMitraPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ShikshaMitraPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(ShikshaMitraPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ShikshaMitraResource>(name);
    }
}
