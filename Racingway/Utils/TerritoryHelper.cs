using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Racingway.Race;
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
                    //long id = GetHouseId();
                    
                    // Funny way to wait for a value to change by polling
                    Plugin.polls.Add((() =>
                    {
                        long id = GetHouseId();
                        if (id != -1)
                        {
                            //Plugin.CurrentTerritory = id;
                            try
                            {
                                Address address = new Address(GetTerritoryId(), GetMapId(), id.ToString());

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
                } else if (HousingTerritories.Contains(territory))
                {
                    Plugin.polls.Add((() =>
                    {
                        long division = GetDivision();
                        if (division != 0)
                        {
                            try
                            {
                                Address address = new Address(GetTerritoryId(), GetMapId(), territory.ToString() + " " + GetWard().ToString() + " " + division.ToString());

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
                } else
                {
                    try
                    {
                        Address address = new Address(GetTerritoryId(), GetMapId(), territory.ToString());

                        //Plugin.CurrentTerritory = territory;
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

        private string UpdateAddress(string input)
        {
            return Compression.ToCompressedBase64(input);
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
