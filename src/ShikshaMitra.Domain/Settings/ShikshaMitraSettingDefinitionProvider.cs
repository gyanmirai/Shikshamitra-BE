using Volo.Abp.Settings;

namespace ShikshaMitra.Settings;

public class ShikshaMitraSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(ShikshaMitraSettings.MySetting1));
    }
}
