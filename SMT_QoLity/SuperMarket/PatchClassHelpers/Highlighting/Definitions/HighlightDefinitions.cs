using HighlightPlus;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using System;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions {

#pragma warning disable IDE0060 //Want to keep all parameters since usage might be bound to change over time.

    public static class HighlightValueDefinitions {

        public static float GetOutlineStrength(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                HighlightMode.OutlineOnly => 1f,
                HighlightMode.OutlineGlow or
                    HighlightMode.OutlineBlurredGlow or
                    //HighlightMode.PerformanceGlow => containerType.IsSlotOrBox() ? 1.1f : 1.7f,
                HighlightMode.SeeThrough => containerType.IsSlotOrBox() ? 1.0f : 1.4f,
                _ => throw new NotImplementedException($"{nameof(GetOutlineStrength)} ({highlightMode})"),
            };

        public static Visibility GetOutlineVisibility(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                //The idea was that in these modes, it would show the sharper outline behind occlussions
                //  instead of a glow, but its not working for storage boxes. Those will use glow instead.
                HighlightMode.OutlineOnly or HighlightMode.SeeThrough 
                    when !HighlightLogic.IsBox(containerType)
                        => Visibility.AlwaysOnTop,
                //Show outline only when in direct view
                _ => Visibility.Normal,
            };

        public static float GetOutlineWidth(HighlightMode highlightMode, ContainerType containerType, float colorAlpha) {
            float widthMultiplier = 1f;

            var alphaExtraWidth = colorAlpha - 0.85f;
            if (alphaExtraWidth > 0) {
                widthMultiplier = 1 + (alphaExtraWidth * 5);
            }

            return (containerType == ContainerType.ProdShelfSlot ? 0.5f : 0.3f) * widthMultiplier;
        }


        public static float GetGlowStrength(HighlightMode highlightMode, ContainerType containerType, float colorAlpha) {
            float strengthMultiplier = 1f;

            var alphaExtraStrength = colorAlpha - 0.90f;
            if (alphaExtraStrength > 0) {
                strengthMultiplier = 1 + (alphaExtraStrength * 10);
            }

            return strengthMultiplier * highlightMode switch {
                HighlightMode.OutlineOnly or HighlightMode.SeeThrough
                    when !HighlightLogic.IsBox(containerType)
                        => 0f,
                //Show outline only when in direct view
                _ => containerType switch {
                    ContainerType.ProdShelfSlot => 2.4f,
                    ContainerType.StorageSlot or
                        ContainerType.GroundBox => 1f,
                    ContainerType.ProdShelf or
                        ContainerType.Storage => 1.4f,
                    _ => throw new NotImplementedException($"{nameof(GetGlowStrength)} ({containerType})")
                }
            };
        }

            
        /*
        public static float GetMaxSeeThroughIntensity(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                HighlightMode.SeeThrough =>
                        containerType switch {
                            ContainerType.ProdShelfSlot => 1f,
                            ContainerType.StorageSlot or
                                ContainerType.GroundBox => 3f,
                            ContainerType.ProdShelf or
                                ContainerType.Storage => 1f,
                            _ => throw new NotImplementedException($"{nameof(GetMaxSeeThroughIntensity)} ({containerType})")
                        },
                _ => 0f,
            };
        */
        public static Visibility GetGlowVisibility(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                HighlightMode.OutlineOnly or HighlightMode.SeeThrough
                    when !HighlightLogic.IsBox(containerType)
                        => Visibility.Normal,   //Doesnt matter since these cases have glow disabled (.Glow = 0f).
                //Show glow when not in view. This is a screen space postprocess in highest quality, so even in
                //  front of it, there will be glow from the back parts that are occluded by the object itself.
                _ => Visibility.OnlyWhenOccluded
            };

        public static BlurMethod GetGlowBlurMethod(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                //Gaussian is high quality, less performant, and a very big fuzzy blur from a distance.
                //  I havent found a way to limit the size of the blue enough, no matter the settings.
                HighlightMode.OutlineBlurredGlow => BlurMethod.Gaussian,
                //Sharper look, a bit uglier maybe and a few artifacts when moving the camera
                //  or character, but with downsampling at 1 is barely noticeable, and it doesnt
                //  look like pure fuzz at a distance. Also faster to render.
                HighlightMode.OutlineGlow => BlurMethod.Kawase,
                _ => BlurMethod.Gaussian    //Unused. In PerformanceGlow it has no effect.
            };

        public static int GetGlowDownsampling(HighlightMode highlightMode, ContainerType containerType) =>
            //Expansion of the glow effect. Higher numbers means larger but fuzzier. 1 is the minimum value.
            highlightMode switch {
                //HighlightMode.PerformanceGlow or 
                    HighlightMode.OutlineGlow or
                    HighlightMode.OutlineBlurredGlow
                        => 1,
                _ => 1  //Unused
            };

        public static QualityLevel GetGlowQuality(HighlightMode highlightMode, ContainerType containerType) =>
        highlightMode switch {
           // HighlightMode.PerformanceGlow => QualityLevel.High,
            _ => QualityLevel.Highest
        };

        public static SeeThroughMode GetSeeThrough(HighlightMode highlightMode, ContainerType containerType) =>
            highlightMode switch {
                HighlightMode.SeeThrough => SeeThroughMode.WhenHighlighted,
                _ => SeeThroughMode.Never
            };
    }
}
