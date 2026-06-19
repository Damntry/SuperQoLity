using Cysharp.Threading.Tasks;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SuperQoLity.SuperMarket.Patches.Misc {

    /*
	/// <summary>
	/// Spawns a truck to spit out all the ordered product boxes, instead of them raining from the sky.
	/// </summary>
	public class TruckBoxOrderSpawner : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableMiscPatches.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - " +
			$"Truck for box orders patch failed. Disabled.";

        //TODO 0 TruckDelivery - An option so boxes ordered are spawned in a 3x3 pattern next to each other, instead of a single one.
		//	As a big alternative, it would be cool if a shitty truck with no animations came and started spitting boxes 
		//	out the back. The motor would make vocalized vroom vroom noises. Once the truck arrives and stops, it plays 
		//	a horn sound and each box should have a sound when spawning too. Not too stupid though, or it would be annoying.
		//	In ManagerBlackboard.ServerCargoSpawner

        //TODO 0 TruckDelivery - Probably the easiest way of doing an animation is to do the path myself with the minitransporter
        //	while recording positions every frame in an AnimationCurve. The problem is how I make it so I manually
        //	adjust the final position from where I end up, to where I intend the car to stop to spit boxes.
        //TODO 0 TruckDelivery - The truck animation when arriving should be incredibly stupid, and multiple of them. Some options:
        //	- Rotating 360º all the way until reaching the destination.
        //	- Some weird pathing all over the place
        //	- Bouncing up and down.
        //	- Truck is both reversed and upside down
        //	- Truck coming from the sky with an angelic sound.
        //	- Different truck skins?
        //	- Deform the truck model
        //	- Think some more.
        //	- A random different jingle sound when coming:
        //		Ice cream truck
        //		Circus
        //		Think some more.
        //TODO 0 TruckDelivery - The truck wont have a real collider, but when a player touches a truck, moving or not, it ll get
        //	teleported somewhere else so it cant get inside.
        //TODO 0 TruckDelivery - When another order comes when a truck is still on the world. That order will
        //	have to wait for the existing truck to go away.
        //TODO 0 TruckDelivery - Settings for sound volumes of all truck stuff, down to zero, in case they dont like it.


        [HarmonyPatch(typeof(ManagerBlackboard), nameof(ManagerBlackboard.ServerCargoSpawner), MethodType.Enumerator)]
		[HarmonyPrefix]
        static async Task<bool> ServerCargoSpawner(ManagerBlackboard __instance) {
            __instance.isSpawning = true;

			//TODO 0 TruckDelivery - Truck comes from outside the map

            Vector3 halfExtents = new (0.3f, 0.3f, 0.45f);

            int waitTime1 = 500;
            int waitTime2 = 200;
            //WaitForSeconds waitTime1 = new WaitForSeconds(0.5f);
            //WaitForSeconds waitTime2 = new WaitForSeconds(0.2f);

            //Make a copy of the ids, so it doesnt get refilled while the order is being delivered.
			//	Any new products being ordered will come out in the next truck.
            List<int> boxIdsToSpawn = new (__instance.idsToSpawn);
            __instance.idsToSpawn.Clear();

            foreach (int boxId in boxIdsToSpawn) {
                //TODO 0 TruckDelivery - The spawn point must be a specific point at the back of the truck.
                Vector3 spawnPosition = merchandiseSpawnpoint.transform.position + 
					new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                if (Physics.BoxCast(spawnPosition + new Vector3(0f, 5f, 0f), halfExtents, 
						-Vector3.up, Quaternion.identity, 7.5f, __instance.boxSpawnLayerMask)) {

                    await UniTask.Delay(waitTime1);
                }

                await UniTask.Delay(waitTime1);

                GameObject val = Object.Instantiate<GameObject>(__instance.boxPrefab, spawnPosition, Quaternion.identity);
                val.GetComponent<BoxData>().NetworkproductID = boxId;
                int maxItemsPerBox = __instance.GetComponent<ProductListing>().productsData[boxId].maxItemsPerBox;
                val.GetComponent<BoxData>().NetworknumberOfProducts = maxItemsPerBox;
                if (boxId < StatisticsManager.Instance.productsAcquired.Count) {
                    StatisticsManager.Instance.productsAcquired[boxId] += maxItemsPerBox;
                    StatisticsManager.Instance.costPerProductAcquired[boxId] += (int)__instance.PricePerBoxRetrieve(boxId);
                }
                Sprite productSprite = __instance.GetComponent<ProductListing>().productsData[boxId].productSprite;
                val.transform.Find("Canvas/Image1").GetComponent<Image>().sprite = productSprite;
                val.transform.Find("Canvas/Image2").GetComponent<Image>().sprite = productSprite;
                val.transform.SetParent(__instance.boxParent);
                NetworkServer.Spawn(val);
                __instance.RpcParentBoxOnClient(val);
            }

			//TODO 0 TruckDelivery - Here it needs to do the animation to go back to base.

            await UniTask.Delay(waitTime2);
            __instance.isSpawning = false;

			return false;
        }

    }
    */
}
