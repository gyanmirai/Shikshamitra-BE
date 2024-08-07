using Volo.Abp.Localization;

namespace ShikshaMitra.Localization;

public static class LocalizationHelper
{
    public static LocalizableString L(string name)
    {
        return LocalizableString.Create<ShikshaMitraResource>(name);
    }
}