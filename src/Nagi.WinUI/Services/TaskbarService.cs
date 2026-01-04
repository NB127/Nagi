using System;
using System.Runtime.InteropServices;

using Nagi.Core.Services.Abstractions;
using Nagi.WinUI.Helpers;
using Nagi.WinUI.Services.Abstractions;
using static Nagi.WinUI.Helpers.TaskbarNativeMethods;

namespace Nagi.WinUI.Services.Implementations;

public class TaskbarService : ITaskbarService, IDisposable
{
    private const int PREVIOUS_BUTTON_ID = 1;
    private const int PLAY_PAUSE_BUTTON_ID = 2;
    private const int NEXT_BUTTON_ID = 3;

    private readonly IMusicPlaybackService _playbackService;


    private ITaskbarList3? _taskbarList;
    private nint _windowHandle;
    private bool _isDisposed;

    private THUMBBUTTON[]? _buttons;
    private nint _prevIcon;
    private nint _nextIcon;
    private nint _playIcon;
    private nint _pauseIcon;

    public TaskbarService(IMusicPlaybackService playbackService)
    {
        _playbackService = playbackService;
        
    }

    public void Initialize(nint windowHandle)
    {
        _windowHandle = windowHandle;

        try
        {
            _taskbarList = (ITaskbarList3)new TaskbarList();
            _taskbarList.HrInit();
        }
        catch (Exception ex)
        {
            Console.Write("Failed to initialize TaskbarList3: " + ex.Message);
            return;
        }

        LoadIcons();
        InitializeButtons();

        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playbackService.TrackChanged += OnTrackChanged;
        
        UpdateTaskbarButtons();
    }

    public void HandleWindowMessage(int msg, nint wParam, nint lParam)
    {
        if (msg != WM_COMMAND || (int)lParam != THBN_CLICKED) return;

        switch ((int)wParam)
        {
            case PREVIOUS_BUTTON_ID:
                _playbackService.PreviousAsync();
                break;
            case PLAY_PAUSE_BUTTON_ID:
                _playbackService.PlayPauseAsync();
                break;
            case NEXT_BUTTON_ID:
                _playbackService.NextAsync();
                break;
        }
    }

    private void OnPlaybackStateChanged() => UpdateTaskbarButtons();
    private void OnTrackChanged() => UpdateTaskbarButtons();

    private void InitializeButtons()
    {
        _buttons = new THUMBBUTTON[3];

        // Previous Button
        _buttons[0] = new THUMBBUTTON
        {
            iId = PREVIOUS_BUTTON_ID,
            dwMask = THB_ICON | THB_TOOLTIP | THB_FLAGS,
            hIcon = _prevIcon,
            szTip = "Previous"
        };

        // Play/Pause Button
        _buttons[1] = new THUMBBUTTON
        {
            iId = PLAY_PAUSE_BUTTON_ID,
            dwMask = THB_ICON | THB_TOOLTIP | THB_FLAGS,
            hIcon = _playIcon,
            szTip = "Play"
        };

        // Next Button
        _buttons[2] = new THUMBBUTTON
        {
            iId = NEXT_BUTTON_ID,
            dwMask = THB_ICON | THB_TOOLTIP | THB_FLAGS,
            hIcon = _nextIcon,
            szTip = "Next"
        };

        try
        {
            _taskbarList?.ThumbBarAddButtons(_windowHandle, (uint)_buttons.Length, _buttons);
        }
        catch (Exception ex)
        {
           Console.Write("Failed to add thumbnail buttons: " + ex.Message);
        }
    }

    private void UpdateTaskbarButtons()
    {
        if (_taskbarList == null || _buttons == null) return;

        var isMusicPlaying = _playbackService.CurrentTrack != null;
        if (!isMusicPlaying)
        {
            for (var i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].dwFlags = THBF_HIDDEN;
            }
        }
        else
        {
            var canGoPrevious = _playbackService.CurrentQueueIndex > 0 || _playbackService.CurrentRepeatMode == Core.Services.Data.RepeatMode.RepeatAll;
            _buttons[0].dwFlags = canGoPrevious ? THBF_ENABLED : THBF_DISABLED;

            // Play/Pause button state
            if (_playbackService.IsPlaying)
            {
                _buttons[1].hIcon = _pauseIcon;
                _buttons[1].szTip = "Pause";
            }
            else
            {
                _buttons[1].hIcon = _playIcon;
                _buttons[1].szTip = "Play";
            }
            _buttons[1].dwFlags = THBF_ENABLED;

            // Next button state
            var canGoNext = _playbackService.CurrentQueueIndex < _playbackService.PlaybackQueue.Count - 1 || _playbackService.CurrentRepeatMode == Core.Services.Data.RepeatMode.RepeatAll;
            _buttons[2].dwFlags = canGoNext ? THBF_ENABLED : THBF_DISABLED;
        }
        
        try
        {
            _taskbarList.ThumbBarUpdateButtons(_windowHandle, (uint)_buttons.Length, _buttons);
        }
        catch (Exception ex)
        {
           Console.WriteLine("Failed to update thumbnail buttons: " + ex.Message);
        }
    }
    
    private void LoadIcons()
    {
        _prevIcon = LoadIconFromSystem("imageres.dll", 222);
        _nextIcon = LoadIconFromSystem("imageres.dll", 223);
        _playIcon = LoadIconFromSystem("imageres.dll", 220);
        _pauseIcon = LoadIconFromSystem("imageres.dll", 221);
    }
    
    private nint LoadIconFromSystem(string dllName, int resourceId)
    {
        var lib = LoadLibrary(dllName);
        var icon = LoadImage(lib, (nint)resourceId, 1, 0, 0, 0);
        FreeLibrary(lib);
        return icon;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _playbackService.TrackChanged -= OnTrackChanged;

        DestroyIcon(_prevIcon);
        DestroyIcon(_nextIcon);
        DestroyIcon(_playIcon);
        DestroyIcon(_pauseIcon);

        if (_taskbarList != null)
        {
            Marshal.ReleaseComObject(_taskbarList);
        }
        
        GC.SuppressFinalize(this);
    }
}