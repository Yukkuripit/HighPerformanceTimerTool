using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HighPerformanceTimer
{
    public class TimerToolViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly Stopwatch _sessionStopwatch = new();
        private readonly DispatcherTimer _countdownTimer;
        private TimeSpan _countdownInitialRemaining;   // 設定された初期残り時間
        private TimeSpan _countdownRemaining;          // 現在の残り時間（表示用）
        private Stopwatch? _countdownStopwatch;        // カウントダウン計測用
        private bool _countdownRunning;

        private readonly Stopwatch _stopwatch = new();
        private readonly DispatcherTimer _stopwatchTimer;
        private TimeSpan _lastLapTime;
        private ObservableCollection<LapRecord> _laps = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // --- プロパティ (前回と同じ) ---
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss.fff");
        public string CurrentTime
        {
            get => _currentTime;
            set => SetField(ref _currentTime, value);
        }

        private string _elapsedTime = "00:00:00.000";
        public string ElapsedTime
        {
            get => _elapsedTime;
            set => SetField(ref _elapsedTime, value);
        }

        private int _countdownMinutes = 0;
        public int CountdownMinutes
        {
            get => _countdownMinutes;
            set
            {
                if (SetField(ref _countdownMinutes, Math.Clamp(value, 0, 59)))
                    if (!_countdownRunning) UpdateCountdownDisplay();
            }
        }

        private int _countdownSeconds = 0;
        public int CountdownSeconds
        {
            get => _countdownSeconds;
            set
            {
                if (SetField(ref _countdownSeconds, Math.Clamp(value, 0, 59)))
                    if (!_countdownRunning) UpdateCountdownDisplay();
            }
        }

        private string _countdownDisplay = "00:00";
        public string CountdownDisplay
        {
            get => _countdownDisplay;
            set => SetField(ref _countdownDisplay, value);
        }

        private string _countdownButtonText = "スタート";
        public string CountdownButtonText
        {
            get => _countdownButtonText;
            set => SetField(ref _countdownButtonText, value);
        }

        private string _stopwatchDisplay = "00:00:00.000";
        public string StopwatchDisplay
        {
            get => _stopwatchDisplay;
            set => SetField(ref _stopwatchDisplay, value);
        }

        private string _stopwatchButtonText = "スタート";
        public string StopwatchButtonText
        {
            get => _stopwatchButtonText;
            set => SetField(ref _stopwatchButtonText, value);
        }

        public ObservableCollection<LapRecord> Laps
        {
            get => _laps;
            set => SetField(ref _laps, value);
        }

        public bool CanClearLaps => Laps.Count > 0;

        // --- コマンド ---
        public ICommand ResetElapsedCommand => new RelayCommand(_ => _sessionStopwatch.Restart());
        public ICommand CountdownStartStopCommand => new RelayCommand(_ => ToggleCountdown());
        public ICommand CountdownResetCommand => new RelayCommand(_ => ResetCountdown());
        public ICommand StopwatchStartStopCommand => new RelayCommand(_ => ToggleStopwatch());
        public ICommand StopwatchResetCommand => new RelayCommand(_ => ResetStopwatch());
        public ICommand StopwatchLapCommand => new RelayCommand(_ => AddLap(), _ => _stopwatch.IsRunning);
        public ICommand ClearLapsCommand => new RelayCommand(_ => ClearLaps(), _ => CanClearLaps);

        public TimerToolViewModel()
        {
            // 現在時刻 (16ms)
            _clockTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss.fff");
            _clockTimer.Start();

            // 経過時間 (16ms)
            _sessionStopwatch.Start();
            var elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = _sessionStopwatch.Elapsed;
                ElapsedTime = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
            };
            elapsedTimer.Start();

            // カウントダウンタイマー (16ms) - ただし内部処理はStopwatch基準
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // ストップウォッチ表示更新 (16ms)
            _stopwatchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _stopwatchTimer.Tick += (s, e) =>
            {
                if (_stopwatch.IsRunning)
                {
                    var ts = _stopwatch.Elapsed;
                    StopwatchDisplay = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
                }
            };
        }

        // カウントダウン表示を設定された分秒から更新
        private void UpdateCountdownDisplay()
        {
            var totalSeconds = CountdownMinutes * 60 + CountdownSeconds;
            _countdownRemaining = TimeSpan.FromSeconds(totalSeconds);
            _countdownInitialRemaining = _countdownRemaining;
            CountdownDisplay = $"{_countdownRemaining.Minutes:D2}:{_countdownRemaining.Seconds:D2}";
        }

        // カウントダウンタイマーのTick処理 (Stopwatch基準)
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (!_countdownRunning || _countdownStopwatch == null) return;

            var elapsed = _countdownStopwatch.Elapsed;
            var remaining = _countdownInitialRemaining - elapsed;

            if (remaining.TotalMilliseconds <= 0)
            {
                // タイマー終了
                _countdownRunning = false;
                _countdownTimer.Stop();
                _countdownStopwatch = null;
                CountdownButtonText = "スタート";
                SystemSounds.Beep.Play();
                MessageBox.Show("タイマーが終了しました。", "汎用タイマー通知", MessageBoxButton.OK, MessageBoxImage.Information);
                // 設定された時間を再表示
                UpdateCountdownDisplay();
                return;
            }

            _countdownRemaining = remaining;
            CountdownDisplay = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        private void ToggleCountdown()
        {
            if (_countdownRunning)
            {
                // 停止
                _countdownRunning = false;
                _countdownTimer.Stop();
                _countdownStopwatch = null;
                CountdownButtonText = "スタート";
                // 現在の残り時間を初期値として保持（再開時に使用）
                _countdownInitialRemaining = _countdownRemaining;
            }
            else
            {
                // 開始
                if (_countdownRemaining.TotalSeconds <= 0 && CountdownMinutes == 0 && CountdownSeconds == 0)
                {
                    MessageBox.Show("時間を設定してください。", "汎用タイマー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // リセット後の未開始状態なら、現在の設定値を初期値とする
                if (_countdownRemaining.TotalSeconds <= 0)
                {
                    UpdateCountdownDisplay();
                }
                _countdownInitialRemaining = _countdownRemaining;
                _countdownStopwatch = Stopwatch.StartNew();
                _countdownRunning = true;
                _countdownTimer.Start();
                CountdownButtonText = "停止";
            }
        }

        private void ResetCountdown()
        {
            _countdownRunning = false;
            _countdownTimer.Stop();
            _countdownStopwatch = null;
            CountdownButtonText = "スタート";
            // 設定された分秒で再初期化
            UpdateCountdownDisplay();
        }

        private void ToggleStopwatch()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                _stopwatchTimer.Stop();
                StopwatchButtonText = "スタート";
            }
            else
            {
                _stopwatch.Start();
                _stopwatchTimer.Start();
                StopwatchButtonText = "停止";
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void ResetStopwatch()
        {
            _stopwatch.Reset();
            _stopwatchTimer.Stop();
            StopwatchDisplay = "00:00:00.000";
            StopwatchButtonText = "スタート";
            ClearLaps();
        }

        private void AddLap()
        {
            if (!_stopwatch.IsRunning) return;
            var now = _stopwatch.Elapsed;
            var lapTime = now - _lastLapTime;
            _lastLapTime = now;

            Laps.Add(new LapRecord
            {
                LapNumber = Laps.Count + 1,
                LapTime = lapTime,
                TotalTime = now
            });
            OnPropertyChanged(nameof(CanClearLaps));
        }

        private void ClearLaps()
        {
            Laps.Clear();
            _lastLapTime = TimeSpan.Zero;
            OnPropertyChanged(nameof(CanClearLaps));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // LapRecord クラス (変更なし)
    public class LapRecord : INotifyPropertyChanged
    {
        private int _lapNumber;
        private TimeSpan _lapTime;
        private TimeSpan _totalTime;

        public int LapNumber
        {
            get => _lapNumber;
            set { _lapNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(LapTimeDisplay)); }
        }
        public TimeSpan LapTime
        {
            get => _lapTime;
            set { _lapTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LapTimeDisplay)); }
        }
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set { _totalTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTimeDisplay)); }
        }
        public string LapTimeDisplay => $"{_lapTime.Minutes:D2}:{_lapTime.Seconds:D2}.{_lapTime.Milliseconds:D3}";
        public string TotalTimeDisplay => $"{_totalTime.Minutes:D2}:{_totalTime.Seconds:D2}.{_totalTime.Milliseconds:D3}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // RelayCommand クラス (変更なし)
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}