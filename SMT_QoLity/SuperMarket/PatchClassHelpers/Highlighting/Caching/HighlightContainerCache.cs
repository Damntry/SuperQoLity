using HighlightPlus;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Caching {

    /// <summary>
    /// Holds any currently highlighted containers.
    /// Each unique HighlighEffect id instance can have one or many associated 
    /// HighlightTargets, to account for the way storage slots work.
    /// </summary>
    public class HighlightContainerCache {

        private Dictionary<HighlightEffect, HighlightTargetCollection> ObjectCache;


        public HighlightContainerCache() {
            ObjectCache = new(new HighlightEffectComparer());
        }

        public bool HasActiveObjects() =>
            ObjectCache.Count > 0 && ObjectCache.Any(o => o.Value.HasActiveObjects());

        public bool TryAddHighlightedTarget(HighlightEffect he, Transform t, ContainerType containerType) {
            if (!ObjectCache.TryGetValue(he, out HighlightTargetCollection objCollection)) {
                objCollection = new(containerType);
                ObjectCache.Add(he, objCollection);
            }

            return objCollection.TryAddHighlightTarget(t, isEnableHighlight: true);
        }

        public Dictionary<HighlightEffect, HighlightTargetCollection> GetActiveCachedObjects() {
            RemoveDeadReferences();

            return ObjectCache;
        }

        private void RemoveDeadReferences() {
            ObjectCache = ObjectCache
                //Filter out highlight effect keys where no Transform is active
                .Where(
                    pair => pair.Value.HasActiveObjects()
                )
                //Convert back to dictionary while removing individual inactive transforms from the collections
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.GetActiveObjectCollection()
                );
        }

        public void ClearCache() {
            ObjectCache.Clear();
        }


        private sealed class HighlightEffectComparer : IEqualityComparer<HighlightEffect> {
            public bool Equals(HighlightEffect o1, HighlightEffect o2) =>
                o1.GetInstanceID() == o2.GetInstanceID();
            public int GetHashCode(HighlightEffect o) {
                unchecked {
                    return 83 * 4211 + o.GetInstanceID();
                }
            }

        }

    }
}
