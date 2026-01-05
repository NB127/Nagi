using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using Nagi.Core.Services.Abstractions;
using Nagi.WinUI.Services.Abstractions;
using System.Threading.Tasks;

namespace Nagi.WinUI.Services.Implementations;

public partial class TaskbarThumbnailService : ITaskbarThumbnailService
{
    private readonly ILogger<TaskbarThumbnailService> _logger;
    private readonly IMusicPlaybackService _playbackService;
    private readonly ITaskbarThumbnailGenerator _thumbnailGenerator;

    private ITaskbarList3? _taskbarList;
    private HWND _hwnd;
    private uint _wmTaskbarButtonCreated;
    private bool _isInitialized;
    private bool _areButtonsAdded;

    // Command IDs for the buttons
    private const int IdPrev = 1001;
    private const int IdPlayPause = 1002;
    private const int IdNext = 1003;

    // Glyphs
    private const string GlyphPlay = "\uE768";
    private const string GlyphPause = "\uE769";
    private const string GlyphPrevious = "\uE892";
    private const string GlyphNext = "\uE893";

    // Subclass ID
    private const uint SubclassId = 101;

    // Delegate to keep alive
    private SUBCLASSPROC _subclassProcDelegate;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr changeInfo);

    public TaskbarThumbnailService(
        ILogger<TaskbarThumbnailService> logger,
        IMusicPlaybackService playbackService,
        ITaskbarThumbnailGenerator thumbnailGenerator)
    {
        _logger = logger;
        _playbackService = playbackService;
        _thumbnailGenerator = thumbnailGenerator;
        _subclassProcDelegate = new SUBCLASSPROC(SubclassProc);
    }

    public void Initialize(IntPtr windowHandle)
    {
        if (_isInitialized) return;

        _hwnd = (HWND)windowHandle;
        _wmTaskbarButtonCreated = PInvoke.RegisterWindowMessage("TaskbarButtonCreated");

        // Ensure we receive the message even if UIPI blocks it (e.g. running as Admin)
        // MSGFLT_ALLOW = 1
        ChangeWindowMessageFilterEx((IntPtr)_hwnd, _wmTaskbarButtonCreated, 1, IntPtr.Zero);

        // Hook the window procedure to listen for messages
        PInvoke.SetWindowSubclass(_hwnd, _subclassProcDelegate, SubclassId, 0);

        // Subscribe to playback events
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playbackService.TrackChanged += OnTrackChanged;

        _isInitialized = true;

        // Initial setup attempt
        InitializeTaskbarList();

        // Start a retry loop to ensure buttons are added if the window wasn't ready immediately
        _ = RetryInitializationAsync();
    }

    private async Task RetryInitializationAsync()
    {
        // Try up to 5 times every 1 second to ensure buttons are added
        for (int i = 0; i < 5; i++)
        {
            if (_areButtonsAdded) return;
            await Task.Delay(1000);

            // Dispatch to UI thread if needed (UpdateButtons is async void but usually safe)
            // But checking _areButtonsAdded is thread-safe enough for this boolean check
            if (!_areButtonsAdded)
            {
                InitializeTaskbarList();
            }
        }
    }

    private void InitializeTaskbarList()
    {
        try
        {
            if (_taskbarList == null)
            {
                var clsId = Guid.Parse("56FDF344-FD6D-11d0-958A-006097C9A090"); // CLSID_TaskbarList
                var type = Type.GetTypeFromCLSID(clsId);
                if (type != null)
                {
                    _taskbarList = (ITaskbarList3?)Activator.CreateInstance(type);
                    _taskbarList?.HrInit();
                }
            }

            if (_taskbarList != null)
            {
                UpdateButtons();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TaskbarList.");
        }
    }

    private LRESULT SubclassProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == _wmTaskbarButtonCreated)
        {
            InitializeTaskbarList();
            return new LRESULT(0);
        }

        if (uMsg == PInvoke.WM_COMMAND)
        {
            var commandId = (int)(wParam.Value & 0xFFFF); // Low word

            if (commandId == IdPrev || commandId == IdPlayPause || commandId == IdNext)
            {
                HandleButtonClick(commandId);
                return new LRESULT(0);
            }
        }

        return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void HandleButtonClick(int commandId)
    {
        // Use fire-and-forget for async commands
        switch (commandId)
        {
            case IdPrev:
                _ = _playbackService.PreviousAsync();
                break;
            case IdPlayPause:
                 _ = _playbackService.PlayPauseAsync();
                break;
            case IdNext:
                _ = _playbackService.NextAsync();
                break;
        }
    }

    private void OnPlaybackStateChanged()
    {
        UpdateButtons();
    }

    private void OnTrackChanged()
    {
        UpdateButtons();
    }

    private async void UpdateButtons()
    {
        if (_taskbarList == null) return;

        bool hasSong = _playbackService.CurrentTrack != null;

        var isPlaying = _playbackService.IsPlaying;
        var playPauseGlyph = isPlaying ? GlyphPause : GlyphPlay;

        // Only generate icons if we are going to show them
        nint prevIcon = IntPtr.Zero;
        nint playPauseIcon = IntPtr.Zero;
        nint nextIcon = IntPtr.Zero;

        if (hasSong)
        {
            prevIcon = await _thumbnailGenerator.GenerateIconAsync(GlyphPrevious);
            playPauseIcon = await _thumbnailGenerator.GenerateIconAsync(playPauseGlyph);
            nextIcon = await _thumbnailGenerator.GenerateIconAsync(GlyphNext);
        }

        // LOGGING: Check if icons are generated
        if (hasSong && prevIcon == IntPtr.Zero)
        {
             _logger.LogWarning("Icons generation failed (Handle is 0).");
        }

        var buttons = new THUMBBUTTON[3];

        buttons[0] = new THUMBBUTTON
        {
            dwMask = THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP | THUMBBUTTONMASK.THB_FLAGS,
            iId = IdPrev,
            hIcon = (HICON)prevIcon,
            szTip = "Previous",
            dwFlags = hasSong ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_HIDDEN
        };

        buttons[1] = new THUMBBUTTON
        {
            dwMask = THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP | THUMBBUTTONMASK.THB_FLAGS,
            iId = IdPlayPause,
            hIcon = (HICON)playPauseIcon,
            szTip = isPlaying ? "Pause" : "Play",
            dwFlags = hasSong ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_HIDDEN
        };

        buttons[2] = new THUMBBUTTON
        {
            dwMask = THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP | THUMBBUTTONMASK.THB_FLAGS,
            iId = IdNext,
            hIcon = (HICON)nextIcon,
            szTip = "Next",
            dwFlags = hasSong ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_HIDDEN
        };

        try
        {
            unsafe
            {
                fixed (THUMBBUTTON* pButtons = buttons)
                {
                    if (!_areButtonsAdded)
                    {
                        try
                        {
                            _taskbarList.ThumbBarAddButtons(_hwnd, (uint)buttons.Length, pButtons);
                            _areButtonsAdded = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to ADD taskbar buttons.");
                        }
                    }
                    else
                    {
                        try
                        {
                            _taskbarList.ThumbBarUpdateButtons(_hwnd, (uint)buttons.Length, pButtons);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to UPDATE taskbar buttons. Attempting re-add.");

                            try
                            {
                                _taskbarList.ThumbBarAddButtons(_hwnd, (uint)buttons.Length, pButtons);
                                _areButtonsAdded = true;
                            }
                            catch { /* Ignore second failure */ }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error interacting with taskbar.");
        }

        // Clean up icons
        if (prevIcon != IntPtr.Zero) PInvoke.DestroyIcon((HICON)prevIcon);
        if (playPauseIcon != IntPtr.Zero) PInvoke.DestroyIcon((HICON)playPauseIcon);
        if (nextIcon != IntPtr.Zero) PInvoke.DestroyIcon((HICON)nextIcon);
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.TrackChanged -= OnTrackChanged;
            PInvoke.RemoveWindowSubclass(_hwnd, _subclassProcDelegate, SubclassId);
            _isInitialized = false;
        }
    }
}
