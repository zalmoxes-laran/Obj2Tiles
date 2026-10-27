using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>--progress end to end through the real CLI: JSON lines on stdout, absent without the flag.</summary>
[TestFixture]
[Category("QualityGate")]
public class CliProgressTests
{
    [Test]
    public void Progress_writesJsonLinesEndingWithDone()
    {
        using var dataset = QualityDataset.CubeTextured();
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(Progress_writesJsonLinesEndingWithDone)), "out");

        var run = CliHarness.RunCli(new[] { "--local", "--lods", "3", "-d", "1", "--progress", dataset.InputObj, dir });
        run.ExitCode.ShouldBe(0, run.Tail);

        var lines = run.Stdout.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var json = lines.Where(l => l.StartsWith('{')).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

        json.ShouldNotBeEmpty();
        json.Select(j => j.GetProperty("stage").GetString()).Distinct()
            .ShouldBe(new[] { "decimation", "splitting", "tiling", "done" });

        var last = json[^1];
        last.GetProperty("stage").GetString().ShouldBe("done");
        last.GetProperty("percent").GetDouble().ShouldBe(100);
        last.GetProperty("elapsed").GetDouble().ShouldBeGreaterThan(0);
        lines.Last(l => l.Length > 0).ShouldStartWith("{\"stage\":\"done\"");

        // The human log is still there, unchanged in shape: every other non-empty line starts with a space.
        lines.Where(l => l.Length > 0 && !l.StartsWith('{')).ShouldAllBe(l => l.StartsWith(' '));
        run.Stdout.ShouldContain(" => Decimation stage with 3 LODs");
    }

    [Test]
    public void WithoutProgress_noJsonLines()
    {
        using var dataset = QualityDataset.CubeTextured();
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(WithoutProgress_noJsonLines)), "out");

        var run = CliHarness.RunCli(new[] { "--local", "--lods", "2", "-d", "1", dataset.InputObj, dir });
        run.ExitCode.ShouldBe(0, run.Tail);
        run.Stdout.Split('\n').ShouldNotContain(l => l.StartsWith('{'));
    }

    [Test]
    public void Progress_failure_endsWithErrorLine()
    {
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(Progress_failure_endsWithErrorLine)), "out");
        var missing = Path.Combine(dir, "does-not-exist.obj");

        var run = CliHarness.RunCli(new[] { "--progress", missing, dir });
        run.ExitCode.ShouldBe(1, run.Tail);

        var last = run.Stdout.Split('\n').Select(l => l.TrimEnd('\r')).Last(l => l.StartsWith('{'));
        var j = JsonDocument.Parse(last).RootElement;
        j.GetProperty("stage").GetString().ShouldBe("error");
        j.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
