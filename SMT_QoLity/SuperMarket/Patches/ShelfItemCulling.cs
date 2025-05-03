using SuperQoLity.SuperMarket.PatchClassHelpers;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches {


	//TODO 0 Culling - On 11_ProduceShelf.asset mesh, from which new shelves are instantiated, I found the private
	//	field m_LocalAABB. So it seems like its already calculated? I could use that one.
	//	There is also one from m_SubMeshes.localAABB. It has different values.

	/*TODO 0 Culling 

	Ok so Unity automatically performs frustum culling apparently. So what is the point of this?? 
		Anyway, it doesnt performa culling of object behind each other (nor GpuInstancingFrustumCulling), so
		I would have to create my own implementation.

	- Apparently gpu instance is off, or at least on the product Material I checked. It could be that thats the work that 
		TeoGames.MeshCombiner does? Check if its the same damn thing, and if not, try instancing.
		SRP batcher would need to be disabled but Im not sure how it is currently.
		Check here: https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html
	  Actually I found this in the Unity docs:
		Meshes that have a low number of vertices can’t be processed efficiently using GPU instancing because the GPU can’t
		distribute the work in a way that fully uses the GPU’s resources. This processing inefficiency can have a detrimental
		effect on performance. The threshold at which inefficiencies begin depends on the GPU, but as a general rule, don’t use
		GPU instancing for meshes that have fewer than 256 vertices.
	  Pretty sure that most product meshes have less than 256, but not sure.

	Things to take into account:
		Im not 100% sure how it works yet, but I imagine I should add this class as a component to the shelf, and manually 
		add each subcontainer renderer to _meshRenderers as a reference.
	This is manually doing the rendering in the Update. I imagine this means I need to somehow disable the automatic
		rendering of whatever I add this to?

	GpuInstancingFrustumCulling uses AABB culling. The problem with AABB is that it takes the object bounds in every posible rotation
		to create the AABB bounding box, which means that the box will be larger than needed so we ll get more false positives.
		In our case we are going to use it on shelf containers that are never going to rotate, except when repositioned 
		manually (in which case we can simply reconstruct the bounding box) so this approach is not great.
		Alternatives: Oriented Bounding Boxes (OBB), or bounding volume hierarchy (BVH).
	*/

	public class ShelfItemCulling {

		public static void Initialize() {
			//TODO 0 Culling - Temp comment until ready
			//WorldState.BuildingsEvents.OnShelfBuilt += ShelfBuilt;
			//WorldState.BuildingsEvents.OnProductShelfLoadedOrUpdated += CreateProductShelfBounds;
		}

		public static void ShelfBuilt(NetworkSpawner instance, int prefabID) {
			GameObject buildable = instance.buildables[prefabID];
			buildable.GetComponent<Data_Container>();
		}

		public static void CreateProductShelfBounds(Data_Container instance) {
			foreach (Transform subcontainer in instance.transform.Find("ProductContainer")) {

			}
		}


		public static void OnStorageLoadedOrUpdated(Data_Container __instance) {
			
		}

		public static void NewBuildableConstructed(NetworkSpawner __instance, int prefabID) {
			/*
			GameObject buildable = __instance.buildables[prefabID];

			if (buildable.TryGetComponent(out Data_Container dataContainer) && 
					dataContainer.GetContainerType() == DataContainerType.StorageShelf) {
				int index = dataContainer.parentIndex;
				Transform buildableParent = __instance.levelPropsOBJ.transform.GetChild(index);
				GameObject lastStorageObject = buildableParent.GetChild(buildableParent.childCount - 1).gameObject;

				ShelfHighlighting.AddHighlightMarkersToStorage(lastStorageObject.transform);
			}
			*/
		}


	}
}
