using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
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
}
