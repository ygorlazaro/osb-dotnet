using Osb.Shell.Kernel;
using Xunit;

namespace Osb.Shell.Tests;

public class TabCompleterTests
{
    private readonly OsbEnvironment _env;
    private readonly TabCompleter _completer;

    public TabCompleterTests()
    {
        _env = new OsbEnvironment();
        _completer = new TabCompleter(_env);
    }

    [Theory]
    [InlineData("APL", 3, "APLIC ")]
    [InlineData("CL", 2, "CLEAR ", "CLS ")]
    [InlineData("H", 1, "HISTORY ", "HOSTNAME ")]
    public void CompletesCommands(string input, int cursor, params string[] expected)
    {
        var candidates = _completer.GetCandidates(input, cursor);
        foreach (var exp in expected)
        {
            Assert.Contains(exp, candidates);
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void CompletesUserSubcommands()
    {
        var candidates = _completer.GetCandidates("USER ", 5);
        Assert.Equal(["ADD ", "CHANGE ", "DEL ", "LIST "], candidates);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void CompletesUserAddSubcommand()
    {
        var candidates = _completer.GetCandidates("USER AD", 7);
        Assert.Contains("ADD ", candidates);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void CompletesHelpTopics()
    {
        var candidates = _completer.GetCandidates("HELP C", 6);
        Assert.Contains("CAL ", candidates);
        Assert.Contains("CD ", candidates);
        Assert.Contains("CLS ", candidates);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void CompletesHistoryTopics()
    {
        var candidates = _completer.GetCandidates("HISTORY D", 9);
        Assert.Contains("DATE ", candidates);
        Assert.Contains("DEL ", candidates);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void CalculatesLongestCommonPrefix()
    {
        var lcp = TabCompleter.GetLongestCommonPrefix(["CLEAR ", "CLEAN "]);
        Assert.Equal("CLEA", lcp);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void ParsesContextWithSemicolonChain()
    {
        var context = TabCompleter.ParseContext("DIR /W ; CD CON", 15);
        Assert.Equal("CD", context.Verb);
        Assert.Equal("CON", context.CurrentToken);
        Assert.Equal(1, context.WordIndex);
        Assert.Equal(12, context.TokenStart);
    }
}
