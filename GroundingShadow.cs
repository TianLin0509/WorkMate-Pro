namespace WorkMatePro
{
    public struct GroundingShadowStyle
    {
        public double WidthRatio;
        public double HeightRatio;
        public double Opacity;
    }

    public static class GroundingShadowPolicy
    {
        public static GroundingShadowStyle Resolve(BehaviorState behavior, bool airborne, bool dragging, bool docked)
        {
            if (docked) return new GroundingShadowStyle { WidthRatio = 0.34, HeightRatio = 0.028, Opacity = 0 };
            if (dragging) return new GroundingShadowStyle { WidthRatio = 0.28, HeightRatio = 0.035, Opacity = 0.07 };
            if (airborne) return new GroundingShadowStyle { WidthRatio = 0.32, HeightRatio = 0.038, Opacity = 0.09 };
            if (behavior == BehaviorState.Sleepy || behavior == BehaviorState.Away)
                return new GroundingShadowStyle { WidthRatio = 0.49, HeightRatio = 0.050, Opacity = 0.15 };
            return new GroundingShadowStyle { WidthRatio = 0.42, HeightRatio = 0.044, Opacity = 0.17 };
        }
    }
}
