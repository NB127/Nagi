// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

using System;
using System.Runtime.InteropServices;

namespace Nagi.WinUI.Helpers;

internal static class TaskbarNativeMethods
{
    private const string User32 = "user32.dll";
    private const string Shell32 = "shell32.dll";
    private const string Ole32 = "ole32.dll";

    internal const int WM_COMMAND = 0x0111;
    internal const int THBN_CLICKED = 0x1800;
    
    internal const uint THB_ICON = 0x00000002;
    internal const uint THB_TOOLTIP = 0x00000004;
    internal const uint THB_FLAGS = 0x00000008;

    internal const uint THBF_ENABLED = 0x00000000;
    internal const uint THBF_DISABLED = 0x00000001;
    internal const uint THBF_HIDDEN = 0x00000008;
    internal const uint THBF_NOBACKGROUND = 0x00000010;

    [DllImport(User32, CharSet = CharSet.Auto)]
    internal static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport(Shell32, CharSet = CharSet.Auto)]
    internal static extern uint RegisterWindowMessage(string msg);

    [DllImport(Ole32)]
    internal static extern int CoCreateInstance(
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [In, MarshalAs(UnmanagedType.IUnknown)] object? pUnkOuter,
        [In] uint dwClsContext,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct THUMBBUTTON
    {
        [MarshalAs(UnmanagedType.U4)]
        public uint dwMask;
        public uint iId;
        public uint iBitmap;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szTip;
        [MarshalAs(UnmanagedType.U4)]
        public uint dwFlags;
    }

    [ComImport]
    [Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList
    {
        [PreserveSig]
        void HrInit();
        [PreserveSig]
        void AddTab(IntPtr hwnd);
        [PreserveSig]
        void DeleteTab(IntPtr hwnd);
        [PreserveSig]
        void ActivateTab(IntPtr hwnd);
        [PreserveSig]
        void SetActiveAlt(IntPtr hwnd);
    }

    [ComImport]
    [Guid("602D4487-4494-4457-A9D5-4B3819528A63")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList2 : ITaskbarList
    {
        [PreserveSig]
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList3 : ITaskbarList2
    {
        [PreserveSig]
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        [PreserveSig]
        void SetProgressState(IntPtr hwnd, uint tbpFlags);
        [PreserveSig]
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        [PreserveSig]
        void UnregisterTab(IntPtr hwndTab);
        [PreserveSig]
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        [PreserveSig]
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
        [PreserveSig]
        int ThumbBarAddButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)] THUMBBUTTON[] pButton);
        [PreserveSig]
        int ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)] THUMBBUTTON[] pButton);
        [PreserveSig]
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        [PreserveSig]
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
        [PreserveSig]
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
        [PreserveSig]
        void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
    }
    
    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class TaskbarList;
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint LoadImage(nint hinst, nint name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(nint hModule);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint hIcon);

    internal delegate nint WindowProc(nint hWnd, int msg, nint wParam, nint lParam);

    internal const int GWLP_WNDPROC = -4;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    internal static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, int msg, nint wParam, nint lParam);
}