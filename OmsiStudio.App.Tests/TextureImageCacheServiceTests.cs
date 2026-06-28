using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using Xunit;

namespace OmsiStudio.App.Tests;

public class TextureImageCacheServiceTests
{
    private class FakeTextureImageLoader : ITextureImageLoader
    {
        public int LoadCount { get; private set; }
        public Func<string, CancellationToken, Task<TextureLoadResult>>? OnLoad { get; set; }

        public Task<TextureLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return OnLoad?.Invoke(filePath, cancellationToken)
                ?? Task.FromResult(new TextureLoadResult { Status = TextureLoadStatus.Success });
        }
    }

    [Fact]
    public async Task GetOrLoadAsync_LoadsOnceForSamePath()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader();
        var cache = new TextureImageCacheService(fakeLoader);
        string path = "/path/to/texture.png";

        // Act
        var result1 = await cache.GetOrLoadAsync(path);
        var result2 = await cache.GetOrLoadAsync(path);

        // Assert
        Assert.Equal(TextureLoadStatus.Success, result1.Status);
        Assert.Equal(TextureLoadStatus.Success, result2.Status);
        Assert.Equal(1, fakeLoader.LoadCount);
    }

    [Fact]
    public async Task GetOrLoadAsync_CaseInsensitiveKeys_ShareEntry()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader();
        var cache = new TextureImageCacheService(fakeLoader);
        string path1 = "/path/to/TEXTURE.png";
        string path2 = "/path/to/texture.png";

        // Act
        await cache.GetOrLoadAsync(path1);
        await cache.GetOrLoadAsync(path2);

        // Assert
        Assert.Equal(1, fakeLoader.LoadCount);
    }

    [Fact]
    public async Task GetOrLoadAsync_FailedLoads_AreNotCached()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoad = (p, t) => Task.FromResult(new TextureLoadResult { Status = TextureLoadStatus.Failed })
        };
        var cache = new TextureImageCacheService(fakeLoader);
        string path = "/path/to/failed.png";

        // Act
        var result1 = await cache.GetOrLoadAsync(path);
        var result2 = await cache.GetOrLoadAsync(path);

        // Assert
        Assert.Equal(TextureLoadStatus.Failed, result1.Status);
        Assert.Equal(TextureLoadStatus.Failed, result2.Status);
        Assert.Equal(2, fakeLoader.LoadCount); // Loaded twice because it failed
    }

    [Fact]
    public async Task GetOrLoadAsync_Cancellation_IsRethrownAndNotCached()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoad = (p, t) => throw new OperationCanceledException()
        };
        var cache = new TextureImageCacheService(fakeLoader);
        string path = "/path/to/cancelled.png";

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => cache.GetOrLoadAsync(path));

        // Setup loader to return success next time
        fakeLoader.OnLoad = null;

        // Try load again
        var result = await cache.GetOrLoadAsync(path);

        // Assert
        Assert.Equal(TextureLoadStatus.Success, result.Status);
        Assert.Equal(2, fakeLoader.LoadCount); // Called twice
    }

    [Fact]
    public async Task GetOrLoadAsync_EvictionLimits_RemovesLeastRecentlyUsed()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader();
        var cache = new TextureImageCacheService(fakeLoader)
        {
            MaxCachedCount = 2
        };

        string path1 = "/path/to/1.png";
        string path2 = "/path/to/2.png";
        string path3 = "/path/to/3.png";

        // Load 1 and 2
        await cache.GetOrLoadAsync(path1);
        await cache.GetOrLoadAsync(path2);
        Assert.Equal(2, fakeLoader.LoadCount);

        // Touch 1 (makes 2 the LRU entry)
        await cache.GetOrLoadAsync(path1);
        Assert.Equal(2, fakeLoader.LoadCount); // 1 was cached, no load

        // Load 3 (should evict 2)
        await cache.GetOrLoadAsync(path3);
        Assert.Equal(3, fakeLoader.LoadCount);

        // Touch 1 again to make 3 the LRU entry
        await cache.GetOrLoadAsync(path1);
        Assert.Equal(3, fakeLoader.LoadCount);

        // Load 2 again (should reload because evicted, making 3 evict)
        await cache.GetOrLoadAsync(path2);
        Assert.Equal(4, fakeLoader.LoadCount);

        // Load 1 again (should be cached)
        await cache.GetOrLoadAsync(path1);
        Assert.Equal(4, fakeLoader.LoadCount); // still 4
    }

    [Fact]
    public async Task GetOrLoadAsync_EvictionByBytes_RemovesLeastRecentlyUsed()
    {
        // Arrange
        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoad = (p, t) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.Success,
                Image = new TextureImageData
                {
                    Width = 10,
                    Height = 10,
                    PixelsRgba32 = new byte[400] // 400 bytes
                }
            })
        };
        
        var cache = new TextureImageCacheService(fakeLoader)
        {
            MaxTotalPixelBytes = 500 // Can only fit one 400-byte image safely, second will exceed 500 + 400 = 800 and trigger eviction
        };

        string path1 = "/path/to/1.png";
        string path2 = "/path/to/2.png";

        await cache.GetOrLoadAsync(path1);
        await cache.GetOrLoadAsync(path2); // Loads 2, evicts 1 (total bytes limit exceeded)

        // Act
        await cache.GetOrLoadAsync(path1); // Reloads 1

        // Assert
        Assert.Equal(3, fakeLoader.LoadCount);
    }
}
