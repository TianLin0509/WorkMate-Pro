namespace WorkMatePro
{
    public enum ContextSceneKind
    {
        None,
        Book,
        Headset,
        FocusHalo,
        Hourglass
    }

    /// <summary>
    /// 情境道具只解释已有状态，不创造新的监控信号；当前前台语境优先于后台任务。
    /// </summary>
    public static class ContextScenePolicy
    {
        public static bool AllowsAutonomousAction(bool buildWaiting)
        {
            return !buildWaiting;
        }

        public static ContextSceneKind Resolve(BehaviorState behavior, bool buildWaiting, bool flowActive)
        {
            if (behavior == BehaviorState.Meeting || behavior == BehaviorState.Watching) return ContextSceneKind.Headset;
            if (buildWaiting) return ContextSceneKind.Hourglass;
            if (behavior == BehaviorState.Reading) return ContextSceneKind.Book;
            if (flowActive && (behavior == BehaviorState.Typing || behavior == BehaviorState.Idle || behavior == BehaviorState.Thinking))
                return ContextSceneKind.FocusHalo;
            return ContextSceneKind.None;
        }
    }
}
