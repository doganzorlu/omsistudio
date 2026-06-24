using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
