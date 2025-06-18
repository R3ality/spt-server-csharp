using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace SPTarkov.Server.Core.Routers.SaveLoad;

[Injectable]
public class InsuranceSaveLoadRouter : SaveLoadRouter
{
    protected override List<HandledRoute> GetHandledRoutes()
    {
        return [new HandledRoute("spt-insurance", false)];
    }

    public override SptProfile HandleLoad(SptProfile profile)
    {
        if (profile.InsuranceList == null)
        {
            profile.InsuranceList = new List<Insurance>();
        }

        return profile;
    }
}
