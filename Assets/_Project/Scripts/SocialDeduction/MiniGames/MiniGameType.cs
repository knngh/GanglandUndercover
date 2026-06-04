namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 小游戏类型枚举。用于从任务名称映射到对应的 MiniGame 类型。
    /// </summary>
    public enum MiniGameType
    {
        WireTask,       // 连线类
        MemoryTask,     // 记忆类
        SwipeCardTask,  // 刷卡/扫描类
        KeypadTask,     // 数字密码键盘
        SortTask,       // 拖拽分类/排序
        ScanTask,       // 圆形扫描
        TapTask,        // 快速点击
        CalibrateTask,  // 航向校准
        AsteroidTask,       // 清理陨石
        DownloadTask,       // 下载数据
        EvidenceArchiveTask,// 证据归档
    }
}