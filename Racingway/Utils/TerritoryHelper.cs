using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace Racingway.Utils
{
    public class TerritoryHelper
    {
        private Plugin Plugin;

        public TerritoryHelper (Plugin plugin)
        {
            Plugin = plugin;
        }

        public static readonly ushort[] HousingTerritories = {
            339, // Mist
            340, // Lavender Beds
            341, // Goblet
            641, // Shirogane
            979 // Empyreum
        };

        public async void GetLocationID(ushort territory)
        {
            try
            {
                bool isInside = IsInside();
                long idToReturn = 0;
                Stopwatch timer = Stopwatch.StartNew();

                if (isInside)
                {
                    //long id = GetHouseId();
                    
                    // Funny way to wait for a value to change by polling
                    Plugin.polls.Add((() =>
                    {
                        long id = GetHouseId();
                        if (id != -1)
                        {
                            Plugin.CurrentTerritory = id;
                            Plugin.AddressChanged(Compression.ToCompressedString(id.ToString()));
                            return true;
                        }

                        return false;
                    }, timer));

                    return;
                } else if (HousingTerritories.Contains(territory))
                {
                    Plugin.polls.Add((() =>
                    {
                        long division = GetDivision();
                        if (division != 0)
                        {
                            Plugin.CurrentTerritory = division;
                            Plugin.AddressChanged(Compression.ToCompressedString(territory.ToString() + " " + GetWard().ToString() + " " + division.ToString()));
                            return true;
                        }

                        return false;
                    }, timer));
                } else
                {
                    Plugin.CurrentTerritory = territory;
                    Plugin.AddressChanged(Compression.ToCompressedString(territory.ToString()));
                }

                //Plugin.ChatGui.Print($"Inside: {IsInside().ToString()}, Division: {GetDivision().ToString()}, Ward: {GetWard().ToString()}, Plot: {GetPlot().ToString()}, HouseID: {GetHouseId().ToString()}, Room: {GetRoomId().ToString()}");
            }
            catch (Exception e)
            {
                Plugin.Log.Warning("Couldnt get housing state on territory change. " + e.Message);
            }
        }

        private string UpdateAddress(string input)
        {
            return Compression.ToCompressedBase64(input);
        }

        private unsafe HousingTerritoryType GetTerritoryType()
        {
            var manager = HousingManager.Instance();
            return manager->CurrentTerritory->GetTerritoryType();
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
