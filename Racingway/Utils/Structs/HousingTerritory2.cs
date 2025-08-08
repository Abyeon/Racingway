using System.Runtime.InteropServices;

namespace Racingway.Utils.Structs
{
    /// <summary>
    /// Gets the underlying territory ID from housing territory.
    /// https://github.com/Critical-Impact/CriticalCommonLib/blob/4b0f6b4fe431817a2b4779f2377d80cbd0bf66c6/GameStructs/HousingTerritory2.cs#L9
    /// </summary>

    [StructLayout(LayoutKind.Explicit, Size = 41376)]
    public unsafe partial struct HousingTerritory2
    {
        [FieldOffset(38560)] public ulong HouseId;

        public uint TerritoryTypeId
        {
            get
            {
                return (uint)((HouseId >> 32) & 0xFFFF);
            }
        }
    }
}
