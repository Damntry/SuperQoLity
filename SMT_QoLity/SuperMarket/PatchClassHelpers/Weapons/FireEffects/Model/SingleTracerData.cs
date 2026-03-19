using Damntry.UtilsUnity.Vectors;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model {

    /// <summary>
    /// 
    /// </summary>
    /// <param name="projectileRay">Full ray from muzzle to its max possible distance</param>
    /// <param name="endPoint">The position where projectileRay hit something that stopped it.</param>
    /// <param name="totalDistance">Total distance traveled by the projectile up to the hit point.</param>
    /// <param name="hitFaceRotation">Rotation facing the surface of the point hit.</param>
    /// <param name="createsImpactMark">True if this projectile can create a hit effect on the target.</param>
    public class SingleTracerData {

        /// <summary>
        /// Unique identifier for the interpolation of a smoke trail.
        /// </summary>
        public uint SmokeTrailId { get; private set; }
        public Vector3CurvesDistanceWidth FullRayPath { get; }

        public Ray ProjectileRay { get; }
        public Vector3 EndPoint { get; }
        public float TotalDistance { get; }
        public LineRenderer LineRender { get; set; }
        /// <summary>Calculated time in Time.time when the tracer is meant to end.</summary>
        public float EndTime { get; private set; }
        /// <summary>Total flight time for the tracer.</summary>
        public float TotalFlightTime { get; private set; }
        public bool CanCreateImpactMark { get; private set; }
        /// <summary>
        /// Rotation facing the surface of the point hit.
        /// </summary>
        public Quaternion HitFaceRotation { get; private set; }
        public bool HasCalculatedEndTime { get; private set; }

        public bool SpawnsSmokeTrail { get; }


        public SingleTracerData(Ray projectileRay, Vector3 startOffset, Vector3 endPoint, float totalDistance,
                IWeaponDefinition weaponDefinition, Vector3 hitFaceRotation, bool createsImpactMark, int totalTracers) {

            SmokeTrailId = TracerGroupData.GlobalSmokeIdCounter++;

            ProjectileRay = projectileRay;
            EndPoint = endPoint;
            TotalDistance = totalDistance;
            CanCreateImpactMark = createsImpactMark;
            HitFaceRotation =
                //No need to calculate look rotation if it wont be used.
                createsImpactMark ? Quaternion.LookRotation(hitFaceRotation) : Quaternion.identity;

            FullRayPath = GenerateVector3TracePath(startOffset, projectileRay.direction,
                ProjectileRay.origin, EndPoint, TotalDistance, weaponDefinition);

            //To reduce particles, the more tracers in the group, the less of a chance that each will spawn a trail.
            SpawnsSmokeTrail = Random.value < 1f / (totalTracers / 3f);
        }

        private static Vector3CurvesDistanceWidth GenerateVector3TracePath(
                Vector3 startOffset, Vector3 direction, Vector3 startPoint, Vector3 endPoint,
                float totalProjectileDistance, IWeaponDefinition weaponDefinition) {

            ITracerSettings tracers = weaponDefinition.Tracers;

            float maxWidthDistance = tracers.MaxWidthDistance >= 0 ? 
                tracers.MaxWidthDistance : weaponDefinition.ProjectileMaxRange;

            Vector3 startPosition = startPoint;
            if (startOffset != Vector3.zero) {
                Quaternion facing = Quaternion.LookRotation(direction);
                startPosition = startPoint + facing * startOffset;
            }

            return new(startPosition, endPoint, tracers.WidthStart, tracers.WidthEnd,
                totalProjectileDistance, maxWidthDistance);
        }

        public void CalculateTracerEndTime(float startLifeTime, float projectileSpeed) {
            EndTime = startLifeTime + TotalDistance / projectileSpeed;
            TotalFlightTime = EndTime - startLifeTime;
            HasCalculatedEndTime = true;
        }

    }
}
