using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;

namespace OmsiStudio.OmsiFormat.Tests;

public class ParserAndScannerTests : IDisposable
{
    private readonly string _testTempDir;

    public ParserAndScannerTests()
    {
        _testTempDir = Path.Combine(AppContext.BaseDirectory, "TestTemp_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_testTempDir))
        {
            Directory.CreateDirectory(_testTempDir);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Parser_ShouldParse_ValidScoFile()
    {
        // Arrange
        var filePath = Path.Combine(_testTempDir, "valid.sco");
        var content = @"
' This is a comment
[friendlyname]
My Scenery Object

[description]
This is a very cool test scenery object.

[groups]
2
Category A
Category B

[mesh]
model.o3d

texture1.png
texture2.tga
";
        File.WriteAllText(filePath, content);
        var parser = new ScoFileParser();

        // Act
        var result = parser.Parse(filePath, "valid.sco");

        // Assert
        Assert.Equal("My Scenery Object", result.DisplayName);
        Assert.Equal("This is a very cool test scenery object.", result.Description);
        Assert.Equal(2, result.Groups.Count);
        Assert.Equal("Category A", result.Groups[0]);
        Assert.Equal("Category B", result.Groups[1]);
        Assert.Single(result.ModelReferences);
        Assert.Equal("model.o3d", result.ModelReferences[0].MeshPath);
        Assert.Equal(2, result.TextureReferences.Count);
        Assert.Contains("texture1.png", result.TextureReferences);
        Assert.Contains("texture2.tga", result.TextureReferences);
        Assert.True(result.HasTextures);
        Assert.False(result.HasNoTextures);
    }

    [Fact]
    public void Parser_ShouldTolerate_MissingSections()
    {
        // Arrange
        var filePath = Path.Combine(_testTempDir, "partial.sco");
        var content = @"
[friendlyname]
Partial Object
";
        File.WriteAllText(filePath, content);
        var parser = new ScoFileParser();

        // Act
        var result = parser.Parse(filePath, "partial.sco");

        // Assert
        Assert.Equal("Partial Object", result.DisplayName);
        Assert.Empty(result.Description);
        Assert.Empty(result.Groups);
        Assert.Empty(result.ModelReferences);
    }

    [Fact]
    public void Parser_ShouldTolerate_MalformedGroups()
    {
        // Arrange
        var filePath = Path.Combine(_testTempDir, "malformed_groups.sco");
        var content = @"
[groups]
invalid_number
Category A
";
        File.WriteAllText(filePath, content);
        var parser = new ScoFileParser();

        // Act
        var result = parser.Parse(filePath, "malformed_groups.sco");

        // Assert
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void Parser_ShouldReturnEmptyMetadata_ForInvalidOrMissingFile()
    {
        // Arrange
        var filePath = Path.Combine(_testTempDir, "non_existent.sco");
        var parser = new ScoFileParser();

        // Act
        var result = parser.Parse(filePath, "non_existent.sco");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("non_existent", result.DisplayName); // falls back to filename
    }

    [Fact]
    public void ScoParser_ShouldParse_Utf8WithBom_AndWithoutBom()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var parser = new OmsiStudio.OmsiFormat.Sco.ScoParser();

        // 1. UTF-8 without BOM
        var pathWithoutBom = Path.Combine(_testTempDir, "utf8_nobom.sco");
        var content = "[friendlyname]\nObj ÖÜŞİĞÇ öüşığç";
        File.WriteAllText(pathWithoutBom, content, System.Text.Encoding.UTF8);

        var resultNoBom = parser.ParseFile(pathWithoutBom);
        Assert.Equal("Obj ÖÜŞİĞÇ öüşığç", resultNoBom.FriendlyName);

        // 2. UTF-8 with BOM
        var pathWithBom = Path.Combine(_testTempDir, "utf8_bom.sco");
        var utf8WithBom = new System.Text.UTF8Encoding(true);
        File.WriteAllText(pathWithBom, content, utf8WithBom);

        var resultBom = parser.ParseFile(pathWithBom);
        Assert.Equal("Obj ÖÜŞİĞÇ öüşığç", resultBom.FriendlyName);
    }

    [Fact]
    public void ScoParser_ShouldParse_Windows1254Turkish()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var parser = new OmsiStudio.OmsiFormat.Sco.ScoParser();
        var win1254 = System.Text.Encoding.GetEncoding(1254);

        var filePath = Path.Combine(_testTempDir, "win1254.sco");
        var content = "[friendlyname]\nObj ÖÜŞİĞÇ öüşığç";
        File.WriteAllText(filePath, content, win1254);

        var result = parser.ParseFile(filePath);
        Assert.Equal("Obj ÖÜŞİĞÇ öüşığç", result.FriendlyName);
    }

    [Fact]
    public void ScoParser_ShouldParse_Windows1252German()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var parser = new OmsiStudio.OmsiFormat.Sco.ScoParser();
        var win1252 = System.Text.Encoding.GetEncoding(1252);

        var filePath = Path.Combine(_testTempDir, "win1252.sco");
        var content = "[friendlyname]\nObj ÄÖÜß äöü";
        File.WriteAllText(filePath, content, win1252);

        var result = parser.ParseFile(filePath);
        Assert.Equal("Obj ÄÖÜß äöü", result.FriendlyName);
    }

    [Fact]
    public async Task Scanner_ShouldFindAndParseScoFiles_InValidRoot()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "Sceneryobjects");
        Directory.CreateDirectory(sceneryObjectsDir);

        var subDir = Path.Combine(sceneryObjectsDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(sceneryObjectsDir, "object1.sco");
        File.WriteAllText(file1, "[friendlyname]\nObj 1");

        var file2 = Path.Combine(subDir, "object2.sco");
        File.WriteAllText(file2, "[friendlyname]\nObj 2");

        var file3 = Path.Combine(subDir, "readme.txt");
        File.WriteAllText(file3, "hello");

        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        // Act
        var assets = new List<OmsiAsset>();
        await foreach (var asset in scanner.ScanDirectoryAsync(_testTempDir))
        {
            assets.Add(asset);
        }

        // Assert
        Assert.Equal(2, assets.Count);
        Assert.Contains(assets, a => a.RelativePath == "object1.sco" && a.DisplayName == "Obj 1");
        Assert.Contains(assets, a => a.RelativePath == Path.Combine("SubFolder", "object2.sco") && a.DisplayName == "Obj 2");
    }

    [Fact]
    public async Task Scanner_ShouldReturnEmpty_WhenSceneryobjectsDoesNotExist()
    {
        // Arrange
        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        // Act
        var assets = new List<OmsiAsset>();
        await foreach (var asset in scanner.ScanDirectoryAsync(_testTempDir))
        {
            assets.Add(asset);
        }

        // Assert
        Assert.Empty(assets);
    }

    [Fact]
    public async Task DirectoryScanner_ShouldDiscoverScoFiles_InSceneryObjects()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "sceneryobjects"); // lowercase to verify case-insensitivity
        Directory.CreateDirectory(sceneryObjectsDir);

        var subDir = Path.Combine(sceneryObjectsDir, "Sub");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(sceneryObjectsDir, "test1.sco");
        File.WriteAllText(file1, "");

        var file2 = Path.Combine(subDir, "test2.SCO"); // uppercase extension
        File.WriteAllText(file2, "");

        var file3 = Path.Combine(subDir, "ignore.txt");
        File.WriteAllText(file3, "");

        var scanner = new OmsiDirectoryScanner();

        // Act
        var results = new List<string>();
        await foreach (var file in scanner.FindScoFilesAsync(_testTempDir))
        {
            results.Add(file);
        }

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
    }

    [Fact]
    public async Task DirectoryScanner_ShouldReturnEmpty_WhenSceneryobjectsDoesNotExist()
    {
        // Arrange
        var scanner = new OmsiDirectoryScanner();

        // Act
        var results = new List<string>();
        await foreach (var file in scanner.FindScoFilesAsync(_testTempDir))
        {
            results.Add(file);
        }

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Scanner_ShouldProduceCorrectRelativePath_UnderLowercaseSceneryObjectsFolder()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "sceneryobjects"); // lowercase
        Directory.CreateDirectory(sceneryObjectsDir);

        var subDir = Path.Combine(sceneryObjectsDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(sceneryObjectsDir, "object1.sco");
        File.WriteAllText(file1, "[friendlyname]\nObj 1");

        var file2 = Path.Combine(subDir, "object2.sco");
        File.WriteAllText(file2, "[friendlyname]\nObj 2");

        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        // Act
        var assets = new List<OmsiAsset>();
        await foreach (var asset in scanner.ScanDirectoryAsync(_testTempDir))
        {
            assets.Add(asset);
        }

        // Assert
        Assert.Equal(2, assets.Count);
        Assert.Contains(assets, a => a.RelativePath == "object1.sco" && a.DisplayName == "Obj 1");
        Assert.Contains(assets, a => a.RelativePath == Path.Combine("SubFolder", "object2.sco") && a.DisplayName == "Obj 2");
    }

    [Fact]
    public async Task Scanner_ShouldProduceScanErrorMessage_WhenSceneryobjectsDoesNotExist()
    {
        // Arrange
        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        // Act
        var result = await scanner.ScanAsync(_testTempDir);

        // Assert
        Assert.Empty(result.DiscoveredAssets);
        Assert.Single(result.Errors);
        Assert.Contains("Sceneryobjects directory does not exist", result.Errors[0]);
    }

    [Fact]
    public async Task Scanner_ShouldPropagateParserWarnings_ToScanResult()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "Sceneryobjects");
        Directory.CreateDirectory(sceneryObjectsDir);

        var malformedFile = Path.Combine(sceneryObjectsDir, "malformed.sco");
        File.WriteAllText(malformedFile, @"
[friendlyname]
Malformed Object

[groups]
invalid_count
Category A
");
        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        // Act
        var result = await scanner.ScanAsync(_testTempDir);

        // Assert
        Assert.Single(result.DiscoveredAssets);
        Assert.Equal("Malformed Object", result.DiscoveredAssets[0].DisplayName);
        Assert.Single(result.Warnings);
        Assert.Contains("Malformed [groups] count", result.Warnings[0]);
    }

    [Fact]
    public async Task Scanner_ShouldContinue_WhenOneScoFileThrowsException()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "Sceneryobjects");
        Directory.CreateDirectory(sceneryObjectsDir);

        var validFile = Path.Combine(sceneryObjectsDir, "valid.sco");
        File.WriteAllText(validFile, "[friendlyname]\nValid Object");

        var badFile = Path.Combine(sceneryObjectsDir, "bad.sco");
        File.WriteAllText(badFile, "[friendlyname]\nBad Object");

        // We can create a scanner using a mock/stub parser that throws on "bad.sco"
        var mockParser = new MockFileParserThatThrows("bad.sco");
        var scanner = new OmsiAssetScanner(mockParser);

        // Act
        var result = await scanner.ScanAsync(_testTempDir);

        // Assert: should discover 2 assets, and have 1 error for bad.sco
        Assert.Equal(2, result.DiscoveredAssets.Count);
        Assert.Single(result.Errors);
        Assert.Contains("Error parsing file", result.Errors[0]);
        Assert.Contains(result.DiscoveredAssets, a => a.DisplayName == "Valid Object");
        Assert.Contains(result.DiscoveredAssets, a => a.DisplayName == "bad"); // fallback
    }

    private class MockFileParserThatThrows : IScoFileParser
    {
        private readonly string _badFileName;

        public MockFileParserThatThrows(string badFileName)
        {
            _badFileName = badFileName;
        }

        public OmsiAsset Parse(string filePath, string relativePath)
        {
            return Parse(filePath, relativePath, out _);
        }

        public OmsiAsset Parse(string filePath, string relativePath, out IReadOnlyList<string> warnings)
        {
            warnings = Array.Empty<string>();
            if (filePath.Contains(_badFileName))
            {
                throw new InvalidOperationException("Failed parsing bad file");
            }
            return new OmsiAsset
            {
                DisplayName = "Valid Object",
                SourceScoPath = filePath,
                RelativePath = relativePath,
                AssetType = OmsiAssetType.SceneryObject
            };
        }
    }

    [Fact]
    public async Task Scanner_ShouldProduceScanErrorMessage_WhenDirectoryScannerThrowsException()
    {
        // Arrange
        var sceneryObjectsDir = Path.Combine(_testTempDir, "Sceneryobjects");
        Directory.CreateDirectory(sceneryObjectsDir);

        var parser = new ScoFileParser();
        var mockDirScanner = new MockDirectoryScannerThatThrows();
        var scanner = new OmsiAssetScanner(parser, mockDirScanner);

        // Act
        var result = await scanner.ScanAsync(_testTempDir);

        // Assert
        Assert.Single(result.DiscoveredAssets); // dummy.sco is yield returned before throw
        Assert.Single(result.Errors);
        Assert.Contains("Scan process encountered a fatal error", result.Errors[0]);
        Assert.Contains("Simulated enumeration error", result.Errors[0]);
    }

    private class MockDirectoryScannerThatThrows : IOmsiDirectoryScanner
    {
        public async IAsyncEnumerable<string> FindScoFilesAsync(
            string omsiRootDirectory, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Simulate directory enumeration failure
            yield return Path.Combine(omsiRootDirectory, "Sceneryobjects", "dummy.sco");
            await Task.Yield();
            throw new UnauthorizedAccessException("Simulated enumeration error");
        }
    }
}
