using Volo.Abp.Authorization.Permissions;
using static ShikshaMitra.Localization.LocalizationHelper;

namespace ShikshaMitra.Permissions;

public class ShikshaMitraPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var bookStoreGroup =
            context.AddGroup(ShikshaMitraPermissions.GroupName, L("Permission:BookStore"));

        var booksPermission =
            bookStoreGroup.AddPermission(ShikshaMitraPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(ShikshaMitraPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(ShikshaMitraPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(ShikshaMitraPermissions.Books.Delete, L("Permission:Books.Delete"));


        var authorsPermission = bookStoreGroup.AddPermission(
            ShikshaMitraPermissions.Authors.Default, L("Permission:Authors"));
        authorsPermission.AddChild(
            ShikshaMitraPermissions.Authors.Create, L("Permission:Authors.Create"));
        authorsPermission.AddChild(
            ShikshaMitraPermissions.Authors.Edit, L("Permission:Authors.Edit"));
        authorsPermission.AddChild(
            ShikshaMitraPermissions.Authors.Delete, L("Permission:Authors.Delete"));
    }
}