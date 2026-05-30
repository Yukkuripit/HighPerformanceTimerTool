using System.Windows.Controls;

namespace HighPerformanceTimer
{
    public partial class TimerToolWindow : UserControl
    {
        public TimerToolWindow()
        {
            InitializeComponent();
            // DataContext は YMM4 が ViewModelType から自動設定するので、ここでは何もしない
        }
    }
}