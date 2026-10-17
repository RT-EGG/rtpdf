using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ApplicationHelper.View.Dialogs;

[HotReloadTrigger]
public abstract class DialogBase
{
    private static Window? _mainWindow;

    private OverlayLayer? _overlayLayer;
    private Grid? _backdrop;
    private IDisposable? _sizeChangedSubscription;

    /// <summary>
    /// デフォルトで表示対象とするウィンドウを設定します。
    /// </summary>
    /// <param name="mainWindow"></param>
    public static void SetMainWindow(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void ShowModal(Window? owner = null)
    {
        if (_overlayLayer != null)
        {
            Close();
        }

        _overlayLayer = OverlayLayer.GetOverlayLayer(owner ?? _mainWindow ?? throw new InvalidProgramException());
        if (_overlayLayer == null)
        {
            return;
        }

        // 画面全体を覆うコンテナ（Grid）を作成
        // ※ OverlayLayer は Canvas ベースだが、全画面サイズの Grid を入れることで自動追従させる
        _backdrop?.Children?.Clear();
        _backdrop = new Grid
        {
            Background = ShowBackdrop ? new SolidColorBrush(Colors.Black, 0.4) : null, // 半透明の黒背景
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // 背景をクリックしたときに下のコントロールにイベントが透過するのを防ぐ（操作ブロック）
        if (ShowBackdrop)
        {
            _backdrop.PointerPressed += (s, e) => e.Handled = true;
        }

        _backdrop.Children.Add(CreateBody());

        // OverlayLayer 自体のサイズ（親ウィンドウのサイズ）を backdrop に強制同期させる設定
        // ※ OverlayLayer 直下の要素はサイズが自動で広がらないため、明示的にバインドするかサイズを同期させる
        _backdrop.Width = _overlayLayer.Bounds.Width;
        _backdrop.Height = _overlayLayer.Bounds.Height;

        // ウィンドウサイズが変更されたら背景サイズも追従させる
        _sizeChangedSubscription = _overlayLayer.GetObservable(Visual.BoundsProperty).Subscribe(bounds =>
        {
            _backdrop.Width = bounds.Width;
            _backdrop.Height = bounds.Height;
            OnWindowSizeChanged(bounds.Size);
        });

        // 最終的に OverlayLayer に追加して表示
        _overlayLayer.Children.Add(_backdrop);

#if DEBUG
        HotReloadHandler.OnHotReload += OnHotReload;
#endif

        OnShown();
    }

    public void Close()
    {
        if (_overlayLayer == null)
        {
            return;
        }

#if DEBUG
        HotReloadHandler.OnHotReload -= OnHotReload;
#endif

        // OverlayLayer 内のすべての子要素を削除して閉じる
        _overlayLayer.Children.Clear();
        _overlayLayer = null;

        _sizeChangedSubscription?.Dispose();
        _sizeChangedSubscription = null;
    }

    private void OnHotReload()
    {
        if (_backdrop is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _backdrop.Children.Clear();
                _backdrop.Children.Add(CreateBody());
            });
        }
    }

    protected abstract Control CreateBody();
    protected virtual void OnShown() { }
    protected virtual bool ShowBackdrop => true;
    protected virtual void OnWindowSizeChanged(Size newSize) { }
}
