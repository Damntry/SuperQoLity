using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Standalone.MainMenuLogo.Fsm;

[ActionCategory(ActionCategory.Array)]
public class ArrayGetRandomButNotReally : ArrayGetRandom {

    public static bool IsLastItemModLogo { get; private set; }

    private static int currentLogoArrayIndex = 0;

    private int prodsUntilLogoCounter = 0;

    private List<GameObject> mainMenuLogos;


    public ArrayGetRandomButNotReally(ArrayGetRandom instance, List<GameObject> mainMenuLogos) : base() {
        this.mainMenuLogos = mainMenuLogos;

        //Copy object properties
        array = instance.array;
        storeValue = instance.storeValue;
        index = instance.index;
        noRepeat = instance.noRepeat;
        everyFrame = instance.everyFrame;
        randomIndex = instance.randomIndex;
        lastIndex = instance.lastIndex;

        //Force logos to start showing first before any other product, so user knows quick if the mod is active.
        prodsUntilLogoCounter = MainMenuLogos.ProductsInBetween;
    }

    public override void Reset() {
        mainMenuLogos = null;
        currentLogoArrayIndex = 0;
        prodsUntilLogoCounter = 0;

        prodsUntilLogoCounter = MainMenuLogos.ProductsInBetween;

        base.Reset();
    }

    public override void OnEnter() {
        DoGetAlmostRandomValue();
        if (!everyFrame) {
            Finish();
        }
    }

    public override void OnUpdate() {
        DoGetAlmostRandomValue();
    }

    private void DoGetAlmostRandomValue() {
        if (prodsUntilLogoCounter < MainMenuLogos.ProductsInBetween) {
            //Default random selection
            prodsUntilLogoCounter++;
            DoGetRandomValue();
            IsLastItemModLogo = false;
        } else {
            //Get the next logo
            storeValue.SetValue(mainMenuLogos[currentLogoArrayIndex++]);
            IsLastItemModLogo = true;

            if (currentLogoArrayIndex >= mainMenuLogos.Count) {
                //Reset and start over again
                prodsUntilLogoCounter = 0;
                currentLogoArrayIndex = 0;
            }
        }
    }
}
