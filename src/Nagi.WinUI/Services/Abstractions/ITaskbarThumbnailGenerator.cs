using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Nagi.WinUI.Services.Abstractions;

public interface ITaskbarThumbnailGenerator
{
    /// <summary>
    ///     Generates an HICON handle for the given glyph and style.
    ///     The caller is responsible for destroying the icon.
    /// </summary>
    /// <param name="glyph">The font glyph to render.</param>
    /// <returns>A handle to the generated icon.</returns>
    Task<nint> GenerateIconAsync(string glyph);
}
