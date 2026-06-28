using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Thread-safe in-memory cache implementation for texture images using an LRU eviction policy.
/// </summary>
public class TextureImageCacheService : ITextureImageCacheService
{
    private readonly ITextureImageLoader _loader;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public int MaxCachedCount { get; set; } = 50;
    public long MaxTotalPixelBytes { get; set; } = 128 * 1024 * 1024; // 128 MB

    public TextureImageCacheService(ITextureImageLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <inheritdoc />
    public async Task<TextureLoadResult> GetOrLoadAsync(string resolvedPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return new TextureLoadResult { Status = TextureLoadStatus.Invalid };
        }

        Task<TextureLoadResult>? loadTask = null;
        bool isNewTask = false;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(resolvedPath, out var cachedNode))
            {
                _lruList.Remove(cachedNode);
                _lruList.AddFirst(cachedNode);
                loadTask = cachedNode.Value.LoadTask;
            }
            else
            {
                isNewTask = true;
                loadTask = _loader.LoadAsync(resolvedPath, cancellationToken);
                
                var cacheEntry = new CacheEntry(resolvedPath, loadTask);
                var linkedListNode = new LinkedListNode<CacheEntry>(cacheEntry);
                
                _cache.Add(resolvedPath, linkedListNode);
                _lruList.AddFirst(linkedListNode);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        try
        {
            var result = await loadTask;

            if (isNewTask)
            {
                if (result.Status == TextureLoadStatus.Failed)
                {
                    await RemoveFromCacheAsync(resolvedPath);
                }
                else
                {
                    await EnforceEvictionLimitsAsync();
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            if (isNewTask)
            {
                await RemoveFromCacheAsync(resolvedPath);
            }
            throw;
        }
        catch (Exception)
        {
            if (isNewTask)
            {
                await RemoveFromCacheAsync(resolvedPath);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _semaphore.Wait();
        try
        {
            _cache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RemoveFromCacheAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _cache.Remove(key);
                _lruList.Remove(node);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnforceEvictionLimitsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            while (_cache.Count > MaxCachedCount || GetTotalPixelBytes() > MaxTotalPixelBytes)
            {
                if (_lruList.Last == null)
                {
                    break;
                }
                
                var lastNode = _lruList.Last;
                _cache.Remove(lastNode.Value.Path);
                _lruList.RemoveLast();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private long GetTotalPixelBytes()
    {
        long total = 0;
        foreach (var node in _cache.Values)
        {
            var task = node.Value.LoadTask;
            if (task.IsCompletedSuccessfully)
            {
                var res = task.Result;
                if (res.Image?.PixelsRgba32 != null)
                {
                    total += res.Image.PixelsRgba32.Length;
                }
            }
        }
        return total;
    }

    private sealed class CacheEntry
    {
        public string Path { get; }
        public Task<TextureLoadResult> LoadTask { get; }

        public CacheEntry(string path, Task<TextureLoadResult> loadTask)
        {
            Path = path;
            LoadTask = loadTask;
        }
    }
}
