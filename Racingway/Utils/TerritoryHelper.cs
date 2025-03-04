using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Racingway.Race;
using Racingway.Utils.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;

using Sheet = Lumina.Excel.Sheets;

namespace Racingway.Utils
{
    public class TerritoryHelper
    {
        private Plugin Plugin;

        public TerritoryHelper (Plugin plugin)
        {
            Plugin = plugin;
        }

        //private static readonly Dictionary<ushort, string> HousingMap = new Dictionary<ushort, string>
        //{
        //    // Small                Medium                  Large                   Chambers                Apartment
        //    { 282, "Mist"},         { 283, "Mist"},         { 284, "Mist"},         { 384, "Mist"},         { 608, "Mist"},

        //    { 342, "Lavender Beds"},{ 343, "Lavender Beds"},{ 344, "Lavender Beds"},{ 385, "Lavender Beds"},{ 609, "Lavender Beds"},

        //    { 345, "Goblet"},       { 346, "Goblet"},       { 347, "Goblet"},       { 386, "Goblet"},       { 610, "Goblet"},

        //    { 649, "Shirogane"},    { 650, "Shirogane"},    { 651, "Shirogane"},    { 652, "Shirogane"},    { 655, "Shirogane"},

        //    { 980, "Empyreum"},     { 981, "Empyreum"},     { 982, "Empyreum"},     { 983, "Empyreum"},     { 999, "Empyreum"}
        //};

        //public static readonly ushort[] HousingTerritories = {
        //    339, // Mist
        //    340, // Lavender Beds
        //    341, // Goblet
        //    641, // Shirogane
        //    979 // Empyreum
        //};

        public static readonly Dictionary<uint, string> HousingDistricts = new Dictionary<uint, string>
        {
            {502, "Mist"},
            {505, "Goblet"},
            {507, "Lavender Beds"},
            {512, "Empyreum"},
            {513, "Shirogane"}
        };

        public async void GetLocationID()
        {
            try
            {
                ushort territory = Plugin.ClientState.TerritoryType;
                bool isInside = IsInside();
                long idToReturn = 0;
                Stopwatch timer = Stopwatch.StartNew();

                if (isInside)
                {                    
                    // Funny way to wait for a value to change by polling
                    Plugin.polls.Add((() =>
                    {
                        long id = GetHouseId();
                        if (id != -1)
                        {
                            try
                            {
                                Address address = new Address(GetTerritoryId(), GetMapId(), id.ToString(), GetRoomAddress());

                                Plugin.AddressChanged(address);
                                return true;
                            } catch (Exception e)
                            {
                                Plugin.Log.Error(e.ToString());
                            }
                        }

                        return false;
                    }, timer));

                    return;
                } /*else if (HousingTerritories.Contains(territory))
                {
                    Plugin.polls.Add((() =>
                    {
                        long division = GetDivision();
                        if (division != 0)
                        {
                            try
                            {
                                Address address = new Address(GetTerritoryId(), GetMapId(), territory.ToString() + " " + GetWard().ToString() + " " + division.ToString(), string.Empty);

                                //Plugin.CurrentTerritory = division;
                                Plugin.AddressChanged(address);
                                return true;
                            } catch (Exception e)
                            {
                                Plugin.Log.Error(e.ToString());
                            }
                        }

                        return false;
                    }, timer));
                }*/ else
                {
                    try
                    {
                        Address address = new Address(GetTerritoryId(), GetMapId(), territory.ToString(), GetAreaName());

                        Plugin.AddressChanged(address);
                    } catch (Exception e)
                    {
                        Plugin.Log.Error(e.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warning("Couldnt get housing state on territory change. " + e.Message);
            }
        }

        private unsafe uint GetMapId()
        {
            var agent = AgentMap.Instance();
            return agent->CurrentMapId;
        }

        private unsafe uint GetTerritoryId()
        {
            var agent = AgentMap.Instance();
            return agent->CurrentTerritoryId;
        }

        private string GetAreaName()
        {
            uint correctedTerritory = CorrectedTerritoryTypeId();
            return Plugin.DataManager.GetExcelSheet<Sheet.TerritoryType>().GetRow(correctedTerritory).PlaceName.Value.Name.ExtractText();
        }

        private unsafe string GetRoomAddress()
        {
            var manager = HousingManager.Instance();
            var territoryInfo = TerritoryInfo.Instance();

            uint correctedTerritory = CorrectedTerritoryTypeId();
            uint rowId = Plugin.DataManager.GetExcelSheet<Sheet.TerritoryType>()
                .GetRow(correctedTerritory).PlaceNameZone.RowId;

            string district = string.Empty;

            if (HousingDistricts.ContainsKey(rowId))
            {
                district = HousingDistricts[rowId];
            }

            var ward = manager->GetCurrentWard();
            var plot = manager->GetCurrentPlot();
            var room = manager->GetCurrentRoom();

            StringBuilder sb = new StringBuilder();
            sb.Append(Plugin.ClientState.LocalPlayer.CurrentWorld.Value.Name.ExtractText());
            sb.Append(" " + district);
            sb.Append($" w{ward+1}");
            sb.Append($" p{plot+1}");

            if (room != 0)
            {
                sb.Append($" room {room}");
            }

            return sb.ToString().Trim();
        }

        //https://github.com/Critical-Impact/CriticalCommonLib/blob/bc358bd4acb1ce8110e51e9eaa495ff12a0300bc/Services/CharacterMonitor.cs#L899
        private unsafe uint CorrectedTerritoryTypeId()
        {
            var manager = HousingManager.Instance();
            if (manager == null)
            {
                return Plugin.ClientState.TerritoryType;
            }

            var character = Plugin.ClientState.LocalPlayer;
            if (character != null && manager->CurrentTerritory != null)
            {
                var territoryType = manager->IndoorTerritory != null
                    ? ((HousingTerritory2*)manager->CurrentTerritory)->TerritoryTypeId
                    : Plugin.ClientState.TerritoryType;

                return territoryType;
            }

            return Plugin.ClientState.TerritoryType;
        }

        private unsafe long GetHouseId()
        {
            var manager = HousingManager.Instance();
            return manager->GetCurrentIndoorHouseId();
        }

        private unsafe short GetRoomId()
        {
            var manager = HousingManager.Instance();
            return manager->GetCurrentRoom();
        }

        private unsafe sbyte GetWard()
        {
            var manager = HousingManager.Instance();
            return manager->GetCurrentWard();
        }

        private unsafe sbyte GetPlot()
        {
            var manager = HousingManager.Instance();
            return manager->GetCurrentPlot();
        }

        private unsafe byte GetDivision()
        {
            var manager = HousingManager.Instance();
            return manager->GetCurrentDivision();
        }

        private unsafe bool IsInside()
        {
            var manager = HousingManager.Instance();
            return manager->IsInside();
        }
    }
}
