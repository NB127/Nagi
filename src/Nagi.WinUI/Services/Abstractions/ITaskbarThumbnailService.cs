using System;

namespace Nagi.WinUI.Services.Abstractions;

public interface ITaskbarThumbnailService : IDisposable
{
    void Initialize(IntPtr windowHandle);
}
