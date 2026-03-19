using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Standalone.MainMenuLogo.Fsm;

[ActionCategory(ActionCategory.Transform)]
public class SetPositionOffset : SetPosition {

    public SetPositionOffset(SetPosition instance) : base() {
        //Copy object properties
        gameObject = instance.gameObject;
        vector = instance.vector;
        x = instance.x;
        y = instance.y;
        z = instance.z;
        space = instance.space;
        everyFrame = instance.everyFrame;
        lateUpdate = instance.lateUpdate;
    }

    public override void OnEnter() {
        if (!everyFrame && !lateUpdate) {
            DoSetPositionOffset();
            Finish();
        }
    }

    public override void OnUpdate() {
        if (!lateUpdate) {
            DoSetPositionOffset();
        }
    }

    public override void OnLateUpdate() {
        if (lateUpdate) {
            DoSetPositionOffset();
        }
        if (!everyFrame) {
            Finish();
        }
    }

    private void DoSetPositionOffset() {
        DoSetPosition();
        if (ArrayGetRandomButNotReally.IsLastItemModLogo) {
            //Apply small offset to logo position so it doesnt collide with register.
            cachedTransform.localPosition += MainMenuLogos.LogoLocalPositionOffset;
        }
    }

}
