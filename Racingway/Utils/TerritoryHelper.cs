using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.FontIdentifier;
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
using System.Numerics;
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

        public static readonly Dictionary<sbyte, string> AptWings = new Dictionary<sbyte, string>
        {
            {-128, "wing 1" },
            {-127, "wing 2" }
        };

        /// <summary>
        /// Sets the player's map flag to the given position and opens their map.
        /// </summary>
        public static unsafe void SetFlagMarkerPosition(Vector3 position, uint territoryId, uint mapId, string title, uint iconId = 60561)
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                var agent = AgentMap.Instance();

                agent->SetFlagMapMarker(territoryId, mapId, position, iconId);
                agent->OpenMap(mapId, territoryId, title);
            });
        }

        /// <summary>
        /// Adds a map marker to the current map at the position.
        /// </summary>
        /// <param name="iconId">The icon to set. /xldata has a list of them</param>
        public static unsafe void AddMapMarker(Vector3 position, uint iconId, int scale = 0)
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                var agent = AgentMap.Instance();

                agent->AddMapMarker(position, iconId, scale);
            });
        }

        public static unsafe void AddMiniMapMarker(Vector3 position, uint iconId, int scale = 0)
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                var agent = AgentMap.Instance();

                agent->AddMiniMapMarker(position, iconId, scale);
            });
        }

        public static unsafe void ResetMapMarkers()
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                var agent = AgentMap.Instance();

                agent->ResetMiniMapMarkers();
                agent->ResetMapMarkers();
            });
        }

        public void GetLocationID()
        {
            ushort territory = Plugin.ClientState.TerritoryType;
            bool isInside = IsInside();

            Stopwatch timer = Stopwatch.StartNew();

            if (isInside)
            {
                // Funny way to wait for a value to change by polling
                // This should be turned into good code at some point but.. This works for now :D
                Plugin.polls.Add((() =>
                {
                    if (Plugin.ClientState.LocalPlayer != null)
                    {
                        Address address = new Address(GetTerritoryId(), GetMapId(), GetHouseId().ToString(), GetRoomAddress());
                        Plugin.AddressChanged(address);

                        return true;
                    }

                    return false;
                }, timer));

                return;
            }
            else
            {
                Plugin.polls.Add((() =>
                {
                    if (Plugin.ClientState.LocalPlayer != null)
                    {
                        Address address = new Address(GetTerritoryId(), GetMapId(), territory.ToString(), GetAreaName());
                        Plugin.AddressChanged(address);

                        return true;
                    }

                    return false;
                }, timer));
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

            if (AptWings.ContainsKey(plot))
            {
                sb.Append(" " + AptWings[plot]);
            } else
            {
                sb.Append($" p{plot + 1}");
            }

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

        private unsafe ulong GetHouseId()
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
