using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace Racingway.Race.Collision
{
    internal class Raycasting
    {
        public static bool TryRaycast(Vector3 start, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 10000f, bool useSphere = false)
        {
            return useSphere
                ? BGCollisionModule.SweepSphereMaterialFilter(start, direction, out hitInfo, maxDistance)
                : BGCollisionModule.RaycastMaterialFilter(start, direction, out hitInfo, maxDistance);
        }
    }
}
