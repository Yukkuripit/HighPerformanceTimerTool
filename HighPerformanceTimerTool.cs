using System;
using YukkuriMovieMaker.Plugin;

namespace HighPerformanceTimer
{
    public class HighPerformanceTimerTool : IToolPlugin
    {
        public string Name => "高性能タイマーツール";
        public Type ViewType => typeof(TimerToolWindow);
        public Type ViewModelType => typeof(TimerToolViewModel);

        // Showメソッドはインターフェース要件で必要だが、YMM4側で自動管理されるため中身は空でよい
        public static void Show() { }
    }
}