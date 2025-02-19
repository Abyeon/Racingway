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

        public async Task<long> GetLocationID(ushort territory)
        {
            try
            {
                bool isInside = IsInside();
                long idToReturn = 0;
                Stopwatch timer = Stopwatch.StartNew();

                if (isInside)
                {
                    long id = GetHouseId();
                    while (id == -1)
                    {
                        if (timer.ElapsedMilliseconds > 3000)
                        {
                            break;
                        }

                        id = GetHouseId();
                        Plugin.Log.Debug("Searching for house id");
                        await Task.Delay(25);
                    }

                    idToReturn = id;
                } else if (HousingTerritories.Contains(territory)) // Currently is the same as if it didnt exist. Potentially will change this to include current ward etc
                {
                    byte division = 0;
                    while (division == 0)
                    {
                        if (timer.ElapsedMilliseconds > 3000)
                        {
                            break;
                        }

                        division = GetDivision();
                        Plugin.Log.Debug("Searching for division");
                        await Task.Delay(25);
                    }

                    idToReturn = territory;
                } else
                {
                    idToReturn = territory;
                }

                //Plugin.ChatGui.Print($"Inside: {IsInside().ToString()}, Division: {GetDivision().ToString()}, Ward: {GetWard().ToString()}, Plot: {GetPlot().ToString()}, HouseID: {GetHouseId().ToString()}, Room: {GetRoomId().ToString()}");
                return idToReturn;
            }
            catch (Exception e)
            {
                Plugin.Log.Warning("Couldnt get housing state on territory change. " + e.Message);
            }
            return 0;
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
