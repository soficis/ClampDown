using ClampDown.Core.Models;
using ClampDown.Core.Services;
using Xunit;

namespace ClampDown.Tests;

/// <summary>
/// Unit tests for ActionLogger - validates logging and export functionality.
/// </summary>
public class ActionLoggerTests
{
    [Fact]
    public void Log_AddsEntryToList()
    {
        // Arrange
        var logger = new ActionLogger();
        
        // Act
        logger.Log(ActionType.FileAnalyze, "C:\\test.txt", ActionResult.Success);
        
        // Assert
        Assert.Single(logger.Entries);
        Assert.Equal(ActionType.FileAnalyze, logger.Entries[0].Type);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        var logger = new ActionLogger();
        logger.Log(ActionType.FileAnalyze, "C:\\test1.txt", ActionResult.Success);
        logger.Log(ActionType.FileDelete, "C:\\test2.txt", ActionResult.Success);
        
        // Act
        logger.Clear();
        
        // Assert
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void ExportToJson_ReturnsValidJson()
    {
        // Arrange
        var logger = new ActionLogger();
        logger.Log(ActionType.ProcessTerminate, "notepad.exe", ActionResult.Success);
        
        // Act
        var json = logger.ExportToJson();
        
        // Assert
        Assert.Contains("ProcessTerminate", json);
        Assert.Contains("notepad.exe", json);
        Assert.StartsWith("[", json.Trim());
    }

    [Fact]
    public void ExportToMarkdown_ContainsTableFormat()
    {
        // Arrange
        var logger = new ActionLogger();
        logger.Log(ActionType.DriveEject, "E:", ActionResult.Success);
        
        // Act
        var markdown = logger.ExportToMarkdown();
        
        // Assert
        Assert.Contains("# ClampDown Activity Log", markdown);
        Assert.Contains("| Time |", markdown);
        Assert.Contains("DriveEject", markdown);
    }

    [Fact]
    public void Entries_ReturnsDefensiveCopy()
    {
        // Arrange
        var logger = new ActionLogger();
        logger.Log(ActionType.FileAnalyze, "test.txt", ActionResult.Success);
        
        // Act
        var entries1 = logger.Entries;
        var entries2 = logger.Entries;
        
        // Assert - should be different list instances
        Assert.NotSame(entries1, entries2);
    }
}
