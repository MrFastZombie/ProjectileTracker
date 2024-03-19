using Newtonsoft.Json;
using ProtoBuf;

namespace ProjectileTracker;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class Ptconfig
{
    public bool EnableProjectileTracker = true;

    public bool allowWelcomeMessage = true;
    public string icon = "ptarrow";
    public string color = "#f9d0dc";

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}