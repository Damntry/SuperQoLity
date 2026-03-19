using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Caching {


    public readonly struct HighlightTargetCollection(ContainerType containerType) {

        public ContainerType ContainerType { get; } = containerType;

        private readonly HashSet<HighlightTarget> ObjectCollection { get; } = new (new HighlightTargetComparer());

        /*
        public bool TryAddHighlightTarget(Transform t, HighlightStatus highlightStatus) {
            return ObjectCollection.Add(new(t, highlightStatus));
        }
        */
        public bool TryAddHighlightTarget(Transform t, bool isEnableHighlight) {
            return ObjectCollection.Add(new(t, isEnableHighlight));
        }


        public bool HasActiveObjects() => ObjectCollection.Any(ho => ho.Transform == true);

        public void RemoveDeadReferences() => ObjectCollection.RemoveWhere(ho => ho.Transform == false);

        public HighlightTargetCollection GetActiveObjectCollection() {
            RemoveDeadReferences();

            return this;
        }

        public IReadOnlyCollection<HighlightTarget> GetActiveObjectSet() => GetActiveObjectCollection().ObjectCollection;


        private sealed class HighlightTargetComparer : IEqualityComparer<HighlightTarget> {
            public bool Equals(HighlightTarget o1, HighlightTarget o2) {
                return o1.Equals(o2);
            }
            public int GetHashCode(HighlightTarget o) => o.GetHashCode();
        }

    }

    public readonly struct HighlightTarget(Transform transform, bool highlightStatus) 
            : IEquatable<HighlightTarget> {

        public Transform Transform { get; } = transform;

        public bool HighlightStatus { get; } = highlightStatus;


        public override bool Equals(object obj) => Equals((HighlightTarget)obj);

        public bool Equals(HighlightTarget obj) {
            return Transform.GetInstanceID() == obj.Transform.GetInstanceID();
        }

        public override int GetHashCode() {
            unchecked {
                return 83 * 2991 + Transform.GetInstanceID();
            }
        }

    }

}
