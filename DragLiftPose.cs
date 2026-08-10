using System;

namespace WorkMatePro
{
    public struct DragLiftPose
    {
        public double TiltDegrees;
        public double LagXDip;
        public double LiftDip;
    }

    /// <summary>
    /// 被拎起时不播放与手速无关的固定摇摆，而让身体对真实拖动速度产生低幅滞后。
    /// 水平速度决定倾角与反向位移；垂直速度只轻微增加提起量，所有通道都限幅。
    /// </summary>
    public static class DragLiftPosePolicy
    {
        public const double MaxTiltDegrees = 3.2;
        public const double MaxLagDip = 2.4;

        public static DragLiftPose Resolve(double velocityX, double velocityY)
        {
            DragLiftPose pose = new DragLiftPose();
            pose.TiltDegrees = Math.Max(-MaxTiltDegrees, Math.Min(MaxTiltDegrees, -velocityX / 165.0));
            pose.LagXDip = Math.Max(-MaxLagDip, Math.Min(MaxLagDip, -velocityX / 230.0));
            pose.LiftDip = -4.0 - Math.Min(1.2, Math.Abs(velocityY) / 520.0);
            return pose;
        }
    }
}
