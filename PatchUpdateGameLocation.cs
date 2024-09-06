using System.Reflection.Metadata.Ecma335;
using HarmonyLib;
using System.Linq;
using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SOD.Common.Extensions;
using UnityEngine;
using KeyValuePair = System.Collections.Generic.KeyValuePair;

namespace BusinessAsUsual;

[HarmonyPatch(typeof(Human), nameof(Human.OnGameLocationChange))]
public class PatchUpdateGameLocation
{
    public static Dictionary<string, Company> BusinessesWithSaleRecords = new Dictionary<string, Company>();
    public static Dictionary<string, string?> AlreadyCheckedPeople = new Dictionary<string, string?>();
    public static List<string> BlacklistedProducts = new List<string>();
    
    [HarmonyPostfix]
    public static void Postfix(Human __instance)
    {
        if (__instance == null || __instance.isPlayer || __instance.isMachine || __instance.isHomeless) return;
        Human human = __instance;
        if (BusinessesWithSaleRecords.Count == 0) FindBusinessesWithSalesRecords();
        if (human?.currentGameLocation?.thisAsAddress == null) return;
        if (!BusinessesWithSaleRecords.TryGetValue(human.currentGameLocation.thisAsAddress.name, out Company c)) return;
        //Don't record employees or majority of sales records will come from them.
        if (c.companyRoster.Contains(human.job)) return;
        AlreadyCheckedPeople.TryGetValue(human.name, out string? companyName);
        if (!companyName.IsNullOrWhiteSpace() && companyName == c.name) return;
        BusinessAsUsual.Instance.LogInfo(human.name+" is visiting "+human.currentGameLocation.thisAsAddress.name);
        AlreadyCheckedPeople[human.name]= c.name;
        if (!IsValidToGenerateRecord(c, human)) return;
        SimulateSalesRecord(c, human);
    }

    private static bool IsValidToGenerateRecord(Company c, Human human)
    {
        int numberOfRecordsWeHave = NumberOfRecordsInLast24Hours(c, human);
        int numberOfRecordsWeNeed = NumberOfRecordsToGenerate(c);
        if(numberOfRecordsWeHave != int.MaxValue) BusinessAsUsual.Instance.LogInfo(c.name+" has "+numberOfRecordsWeHave+" sales records out of "+numberOfRecordsWeNeed);
        return numberOfRecordsWeHave <= numberOfRecordsWeNeed && human != MurderController.Instance.currentMurderer;
    }

    private static int NumberOfRecordsToGenerate(Company c)
    {
        return c.prices.Count;
    }

    private static int NumberOfRecordsInLast24Hours(Company company, Human human)
    {
        float oneDayBeforeCurrentTime = SessionData.Instance.gameTime - 24f;
        int numberOfRecordsInLast24Hours = 0;
        List<Company.SalesRecord> salesRecords = company.sales.ToList();
        int counter = 0;
        for (int i = salesRecords.Count-1; i >= 0; i--)
        {
            Company.SalesRecord sa = salesRecords[i];
            if (sa == null) continue;
            //yes this breaks "do one thing" but I don't want to loop twice, so just return a ridiculously high number if punter exists
            if (human != null && sa.GetPunter() == human)
            {
                BusinessAsUsual.Instance.LogInfo("Punter already has a record");
                return int.MaxValue;
            }
            float saleTime = sa.time;
            if (saleTime < oneDayBeforeCurrentTime)
            {
                company.sales.Remove(sa);
                numberOfRecordsInLast24Hours--;
                counter++;
            }
            numberOfRecordsInLast24Hours++;
        }
        if(counter >0) BusinessAsUsual.Instance.LogInfo("Deleted "+counter+" records from "+company.name+" as they were over 24 hours old");
        return numberOfRecordsInLast24Hours;
    }

    private static bool SimulateSalesRecord(Company c, Human visitor, bool forceCreate = false, bool generateFakeTimeStamp = false)
    {
        double rolledNumber = BusinessAsUsual.Random.NextDouble();
        //The more desperate we are for records the more likely we are to make a "purchase"
        double chanceOfRecordCreation = 1.0d-((double)NumberOfRecordsInLast24Hours(c, visitor) / NumberOfRecordsToGenerate(c)); 
        if(!forceCreate)BusinessAsUsual.Instance.LogInfo("Rolled "+rolledNumber+" needed "+chanceOfRecordCreation);
        if (!forceCreate && chanceOfRecordCreation < rolledNumber) return false;
        if (c.prices.Count == 0 || !c.preset.recordSalesData || c.preset.isSelfEmployed) return false;
        BusinessAsUsual.Instance.LogInfo("Attempting to create fake record for "+visitor.name+" in "+c.name);
        int randomlySelectedProductIndex = BusinessAsUsual.Random.Next(0, c.prices.Count);
        Il2CppSystem.Collections.Generic.List<InteractablePreset> ipList = new Il2CppSystem.Collections.Generic.List<InteractablePreset>();
        foreach (Il2CppSystem.Collections.Generic.KeyValuePair<InteractablePreset, int> kvp in c.prices)
        {
            if (randomlySelectedProductIndex != 0)
            {
                randomlySelectedProductIndex--;
                continue;
            }
            InteractablePreset ip = kvp.Key;
            if (ip == null) continue;
            if(ProductIsBlacklisted(ip.name.ToLower())) continue;
            if (AlreadyHasItemPicked(visitor, ip)) continue;
            ipList.Add(ip);
            BusinessAsUsual.Instance.LogInfo(visitor.name+" is buying a "+ip.name);
            //If we're buying a weapon, buy some ammo too. Murderers always buy weapon + ammo so just a weapon sale will stand out like a sore thumb
            if (ip.weapon?.ammunition?.Count > 0)
            {
                List<InteractablePreset> ammo = ip.weapon.ammunition.ToList();
                ip = ammo[BusinessAsUsual.Random.Next(0, ip.weapon.ammunition.Count)];
                if (AlreadyHasItemPicked(visitor, ip)) continue;
                ipList.Add(ip);
            }
            break;
        }
        if (ipList.Count == 0) return false;
        InteractablePreset[] ipListCopy = ipList.ToArray();
        for (int i = 0; i < ipList.Count; i++)
        {
            InteractablePreset ip = ipListCopy[i];
            if (ip == null) continue;
            InteractableCreator.Instance.CreateWorldInteractable(ip, visitor, visitor, null, Vector3.zero, Vector3.zero, null, null)?.SetInInventory(visitor);
        }
        float timeStamp = generateFakeTimeStamp ? FakeTimeStamp() : SessionData.Instance.gameTime;
        c.AddSalesRecord(visitor, ipList, timeStamp);
        BusinessAsUsual.Instance.LogInfo("Fake record created");
        return true;
    }

    private static bool ProductIsBlacklisted(string productName)
    {
        for (int i = 0; i < BlacklistedProducts.Count; i++)
        {
            string s = BlacklistedProducts[i].ToLower();
            if (productName.Contains(s)) return true;
        }
        return false;
    }

    private static bool AlreadyHasItemPicked(Human visitor, InteractablePreset ip)
    {
        Interactable[] visitorsInventory = visitor.inventory.ToArray();
        for (int i = 0; i < visitorsInventory.Length; i++)
        {
            Interactable inventoryItem = visitorsInventory[i];
            if (inventoryItem.preset == ip) return true;
            //We don't want people loading up on weapons. One per customer (at the very least one gun per customer only)
            if (ip.weapon != null && inventoryItem.preset.weapon != null)
            {
                if (ip.weapon.ammunition == null && inventoryItem.preset.weapon.ammunition == null) return true;
                if (ip.weapon.ammunition != null && inventoryItem.preset.weapon.ammunition != null) return true;
            }
        }
        return false;
    }

    private static float FakeTimeStamp()
    {
        float currentTime = SessionData.Instance.gameTime;
        //first decrement by a random amount of hours
        currentTime -= BusinessAsUsual.Random.Next(1, 24);
        //Now a random selection of minutes so the player doesn't spot them all happening at the top of the hour
        currentTime += (float)BusinessAsUsual.Random.NextDouble();
        return currentTime;
    }

    private static void FindBusinessesWithSalesRecords()
    {
        List<Company> companiesInCity = CityData.Instance.companyDirectory.ToList();
        for (int i = 0; i < companiesInCity.Count; i++)
        {
            Company c = companiesInCity[i];
            if (c == null) continue;
            if (c.prices.Count == 0 || !c.preset.recordSalesData || c.preset.isSelfEmployed) continue;
            if (BusinessesWithSaleRecords.Keys.Contains(c.address.name)) continue;
            BusinessesWithSaleRecords.Add(c.address.name, c);
            BusinessAsUsual.Instance.LogInfo(c.name+" is at "+c.address.name);
        }
    }

    public static void CreateFakeRecords()
    {
        List<Company> companies = BusinessesWithSaleRecords.Values.ToList();
        List<Human> citizens = new List<Human>(CityData.Instance.citizenDirectory.ToList());
        for (int i = 0; i < companies.Count; i++)
        {
            Company c = companies[i];
            if(c == null) continue;
            
            //Keep at least a quarter of the "slots" filled at all times (a naturally high foot traffic area should never need this, but less well trod areas might)
            int minimumRecords = NumberOfRecordsToGenerate(c) / 4;
            int numberOfRecords = NumberOfRecordsInLast24Hours(c, null);
            BusinessAsUsual.Instance.LogInfo(c.name+" has "+numberOfRecords+"/"+minimumRecords+" records");
            if (numberOfRecords >= minimumRecords) continue;
            int counter = 0;
            while (minimumRecords > 0)
            {
                Human h = GetFakeCitizen(citizens);
                if (!SimulateSalesRecord(c, h, true, true)) continue;
                minimumRecords--;
                counter++;
            }
            BusinessAsUsual.Instance.LogInfo("Created "+counter+" fake records for "+c.name);
        }
    }

    private static Human GetFakeCitizen(List<Human> potentialCitizens)
    {
        while (true)
        {
            Human h = potentialCitizens[BusinessAsUsual.Random.Next(0, potentialCitizens.Count)];
            if (h != null && !h.isDead && !h.isPlayer && h != MurderController.Instance.currentMurderer && !h.isHomeless) return h;
        }
    }
}