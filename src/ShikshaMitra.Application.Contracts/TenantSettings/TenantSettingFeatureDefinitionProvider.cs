using ShikshaMitra.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;

namespace ShikshaMitra.TenantSettings;

public class TenantSettingFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        FeatureGroupDefinition tenantSettingsGroup =
            context.AddGroup("TenantSettings",
                displayName: LocalizableString.Create<ShikshaMitraResource>("Features:TenantSettings"));

        tenantSettingsGroup.AddFeature("TenantSettings.Onboarding", defaultValue: "true",
            displayName: LocalizableString.Create<ShikshaMitraResource>("Features:TenantSettings.Onboarding"));
    }
}