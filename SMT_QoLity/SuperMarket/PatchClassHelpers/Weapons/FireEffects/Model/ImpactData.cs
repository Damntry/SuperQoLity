using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model {

    public interface IImpactData {
        GameObject[] ImpactObjs { get; }
    }

    public record struct HitMarkSpatial(Vector3 Position, Quaternion HitFaceRotation);

    public readonly struct HitMarkData(GameObject impactObj) : IImpactData {
        public GameObject[] ImpactObjs { get; } = [impactObj];
    }


    public record struct SparkInitialData(Vector3 Position, float Distance);

    
    public readonly struct SparkData(SparkInitialData sparkData, GameObject[] impactSparkObjs) : IImpactData {
        public Vector3 Position { get; } = sparkData.Position;
        public GameObject[] ImpactObjs { get; } = impactSparkObjs;
    }
}
