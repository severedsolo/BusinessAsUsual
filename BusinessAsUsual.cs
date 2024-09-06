using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using JetBrains.Annotations;
using SOD.Common;
using SOD.Common.Helpers;

namespace BusinessAsUsual;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class BusinessAsUsual : BasePlugin
{
    public const string PLUGIN_GUID = "Severedsolo.SOD.BusinessAsUsual";
    public const string PLUGIN_NAME = "BusinessAsUsual";
    public const string PLUGIN_VERSION = "1.0.0";
    public static BusinessAsUsual Instance;
    public static Random Random = new Random();
    private static ConfigEntry<bool> debugMode { get; set; }

    public override void Load()
    {
        Instance = this;
        Harmony h = new Harmony(PLUGIN_GUID);
        h.PatchAll(Assembly.GetExecutingAssembly());
        Lib.Time.OnTimeInitialized += AddTimeEvents;
        Lib.SaveGame.OnAfterNewGame += ResetMod;
        Lib.SaveGame.OnAfterLoad += ResetMod;
        debugMode = Config.Bind("Debugging", "BusinessAsUsual.DebuggingEnabled", false, "Debugging Enabled");
        LogInfo("Plugin is initialised", true);
    }

    private void ResetMod(object? sender, EventArgs e)
    {
        LoadBlacklist();
        PatchUpdateGameLocation.AlreadyCheckedPeople.Clear();
        PatchUpdateGameLocation.BusinessesWithSaleRecords.Clear();
        LogInfo("All data reset", true);
    }

    private void LoadBlacklist()
    {
        PatchUpdateGameLocation.BlacklistedProducts.Clear();
        string? path = Lib.SaveGame.GetPluginDirectoryPath(Assembly.GetExecutingAssembly());
        if (path.IsNullOrWhiteSpace()) return;
        path = Path.Combine(path, "blacklist.txt");
        if (!File.Exists(path))
        {
            Log.LogWarning("Blacklist file (expected to be at "+path+") does not exist!");
            return;
        }
        int counter = 0;
        using (StreamReader reader = new StreamReader(path))
        {
            while (true)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                PatchUpdateGameLocation.BlacklistedProducts.Add(line);
                counter++;
                LogInfo("Added " + line + " to blacklist", true);
            }
        }
        LogInfo("Loaded "+counter+" items from blacklist", true);
    }

    private void AddTimeEvents(object? sender, TimeChangedArgs e)
    {
        Lib.Time.OnHourChanged += CheckIfNeedToBackFillRecords;
    }

    private void CheckIfNeedToBackFillRecords(object? sender, TimeChangedArgs e)
    {
        PatchUpdateGameLocation.CreateFakeRecords();
    }

    public void LogInfo(string messageToLog, bool forcePrint = false)
    {
        if (!forcePrint && !debugMode.Value) return;
        Log.LogInfo(messageToLog);
    }
}