using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Conversion;

namespace OmsiStudio.Core.Services;

public interface IAssetConversionService
{
    Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default);
}
