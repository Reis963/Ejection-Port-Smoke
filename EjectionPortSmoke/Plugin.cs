using BepInEx;

namespace EjectionPortSmoke;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.SPT.core", "4.1.3")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.reis963.ejectionportsmoke";
    public const string PluginName = "Ejection Port Smoke";
    public const string PluginVersion = "1.0.0";

    private void Awake()
    {
        EjectionPortSmokeEmitter.Bind(Config, Logger, this);

        new FirearmsShellExtractionPatch().Enable();
        new UnderbarrelShellExtractionPatch().Enable();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
    }
}
