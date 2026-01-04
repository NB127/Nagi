using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nagi.WinUI.Services.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Nagi.WinUI.Services.Implementations;

public class TaskbarThumbnailGenerator : ITaskbarThumbnailGenerator
{
    private readonly ILogger<TaskbarThumbnailGenerator> _logger;
    private Grid? _hostGrid;
    private readonly DispatcherQueue _dispatcherQueue;

    public TaskbarThumbnailGenerator(ILogger<TaskbarThumbnailGenerator> logger, DispatcherQueue dispatcherQueue)
    {
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
    }

    private void EnsureHostGrid()
    {
        if (_hostGrid != null) return;

        if (App.RootWindow is MainWindow mainWindow &&
            mainWindow.Content is Grid mainGrid)
        {
            // Find the hidden grid defined in XAML
            _hostGrid = mainGrid.FindName("TaskbarIconHost") as Grid;
            if (_hostGrid == null)
            {
                _logger.LogWarning("TaskbarIconHost grid not found in MainWindow.");
            }
        }
    }

    public async Task<nint> GenerateIconAsync(string glyph)
    {
        // Must run on UI thread
        var tcs = new TaskCompletionSource<nint>();

        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var result = await GenerateIconInternalAsync(glyph);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }

        return await GenerateIconInternalAsync(glyph);
    }

    private async Task<nint> GenerateIconInternalAsync(string glyph)
    {
        EnsureHostGrid();

        if (_hostGrid == null)
        {
            _logger.LogError("Cannot generate icon: Host grid is null.");
            return IntPtr.Zero;
        }

        try
        {
            // 1. Create the visual element
            var buttonContainer = new Grid
            {
                Width = 40,
                Height = 40,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };

            var button = new Button
            {
                Style = Application.Current.Resources["MediaControlButtonStyle"] as Style,
                Content = new FontIcon
                {
                    Glyph = glyph,
                    FontSize = 16,
                    FontFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily
                },
                Width = 40,
                Height = 40,
                IsEnabled = true
            };

            buttonContainer.Children.Add(button);

            // Add to the visual tree
            _hostGrid.Children.Add(buttonContainer);

            // Force update layout
            _hostGrid.UpdateLayout();

            // 2. Render to RenderTargetBitmap
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(buttonContainer);

            var pixelBuffer = await rtb.GetPixelsAsync();
<<<<<<< Updated upstream
            var bytes = pixelBuffer.ToArray(); // BGRA8 - Now valid with WindowsRuntime
            var width = rtb.PixelWidth;
            var height = rtb.PixelHeight;
=======
            var bytes = new byte[pixelBuffer.Length];
            Windows.Storage.Streams.DataReader.FromBuffer(pixelBuffer).ReadBytes(bytes);
>>>>>>> Stashed changes

            // Clean up
            _hostGrid.Children.Remove(buttonContainer);

            // 3. Process with ImageSharp and convert to HICON
            return ProcessAndCreateIcon(bytes, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate taskbar icon.");
            return IntPtr.Zero;
        }
    }

    private unsafe nint ProcessAndCreateIcon(byte[] bgraBytes, int width, int height)
    {
        // Load into ImageSharp
        using var image = Image.LoadPixelData<Bgra32>(bgraBytes, width, height);

        // Note: If icons appear upside down, uncomment the following line:
        // image.Mutate(x => x.Flip(FlipMode.Vertical));

        var processedBytes = new byte[width * height * 4];
        image.CopyPixelDataTo(processedBytes);

        // AND mask: 1 bit per pixel.
        // Row stride must be a multiple of 2 bytes (WORD) or 4 (DWORD).
        // For 40 pixels: 40 bits = 5 bytes. Aligned to 16-bit (2 bytes) -> 6 bytes stride.
        int stride = ((width + 15) / 16) * 2;
        byte[] andMask = new byte[stride * height];
        // All zeros = opaque (alpha channel in XOR overrides).

        fixed (byte* pAndBits = andMask)
        fixed (byte* pXorBits = processedBytes)
        {
             return (nint)PInvoke.CreateIcon(
                new HINSTANCE(PInvoke.GetModuleHandle((PCWSTR)null)),
                width,
                height,
                1,  // Planes
                32, // BitsPerPixel
                pAndBits,
                pXorBits
            );
        }
    }
}
