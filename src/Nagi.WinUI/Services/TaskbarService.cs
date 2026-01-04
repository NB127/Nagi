using System;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;
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
    private readonly ILogger<TaskbarService> _logger;


    private ITaskbarList3? _taskbarList;
    private nint _windowHandle;
    private bool _isDisposed;

    private THUMBBUTTON[]? _buttons;
    private nint _prevIcon;
    private nint _nextIcon;
    private nint _playIcon;
    private nint _pauseIcon;

    public TaskbarService(IMusicPlaybackService playbackService, ILogger<TaskbarService> logger)
    {
        _playbackService = playbackService;
        _logger = logger;
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
            _logger.LogError(ex, "Failed to initialize TaskbarList3");
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
            dwMask = THB.Icon | THB.Tooltip | THB.Flags,
            hIcon = _prevIcon,
            szTip = "Previous"
        };

        // Play/Pause Button
        _buttons[1] = new THUMBBUTTON
        {
            iId = PLAY_PAUSE_BUTTON_ID,
            dwMask = THB.Icon | THB.Tooltip | THB.Flags,
            hIcon = _playIcon,
            szTip = "Play"
        };

        // Next Button
        _buttons[2] = new THUMBBUTTON
        {
            iId = NEXT_BUTTON_ID,
            dwMask = THB.Icon | THB.Tooltip | THB.Flags,
            hIcon = _nextIcon,
            szTip = "Next"
        };

        var addButtonsResult = _taskbarList?.ThumbBarAddButtons(_windowHandle, (uint)_buttons.Length, _buttons);
        if (addButtonsResult != 0)
        {
            _logger.LogError("Failed to add thumbnail buttons. HRESULT: {Result}", addButtonsResult);
        }
    }

    private void UpdateTaskbarButtons()
    {
        if (_taskbarList == null || _buttons == null) return;

        var canGoPrevious = _playbackService.CurrentQueueIndex > 0 || _playbackService.CurrentRepeatMode == Core.Services.Data.RepeatMode.RepeatAll;
        _buttons[0].dwFlags = canGoPrevious ? THBF.Enabled : THBF.Disabled;

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
        _buttons[1].dwFlags = THBF.Enabled;

        // Next button state
        var canGoNext = _playbackService.CurrentQueueIndex < _playbackService.PlaybackQueue.Count - 1 || _playbackService.CurrentRepeatMode == Core.Services.Data.RepeatMode.RepeatAll;
        _buttons[2].dwFlags = canGoNext ? THBF.Enabled : THBF.Disabled;
        
        var updateButtonsResult = _taskbarList.ThumbBarUpdateButtons(_windowHandle, (uint)_buttons.Length, _buttons);
        if (updateButtonsResult != 0)
        {
            _logger.LogError("Failed to update thumbnail buttons. HRESULT: {Result}", updateButtonsResult);
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