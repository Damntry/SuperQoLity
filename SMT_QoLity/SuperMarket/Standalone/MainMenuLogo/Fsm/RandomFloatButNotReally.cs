using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Standalone.MainMenuLogo.Fsm;

[ActionCategory(ActionCategory.Math)]
public class RandomFloatButNotReally : RandomFloat {

    public RandomFloatButNotReally(RandomFloat instance) : base() {
        //Copy object properties
        min = instance.min;
        max = instance.max;
        storeResult = instance.storeResult;
    }

    public override void OnEnter() {
        //If the spawned item is a mod logo, restrict rotation range so its easier to see the image.
        float rotation;
        if (ArrayGetRandomButNotReally.IsLastItemModLogo) {
            rotation = Random.Range(MainMenuLogos.LogoRotationYRange.min, MainMenuLogos.LogoRotationYRange.max);
        } else {
            rotation = Random.Range(min.Value, max.Value);
        }

        storeResult.Value = rotation;
        Finish();
    }

}
