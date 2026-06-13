using System.Diagnostics;
using System.Globalization;
using DotNext.Runtime;
using TheOmenDen.VRMParser.Glb;

namespace TheOmenDen.VRMParser.Benchmarks.Diagnostics;

/// <summary>
/// A runtime GC-pause / memory-pressure harness — the diagnostic BenchmarkDotNet's
/// <c>MemoryDiagnoser</c> can't give you. It subscribes to DotNext's <see cref="GCNotification"/>
/// (<c>GC.WhenTriggered()</c>), parses a full-size real-world VRM in a loop, and reads each
/// collection's <see cref="GCMemoryInfo"/> to report total/max/average pause time, collection counts
/// by generation, peak heap size, and peak fragmentation.
/// </summary>
internal static class GcPauseHarness
{
    private const int DefaultIterations = 200;

    public static int Run(string[] args)
    {
        BenchmarkInput? input = BenchmarkInputs.TryLoadRealModel();
        if (input is null)
        {
            Console.WriteLine($"Set {BenchmarkInputs.RealVrmPathEnvVar} to a large .vrm/.glb file to run the GC-pause harness.");
            Console.WriteLine("The bundled fixtures are ~1.5 KB and trigger too little GC to produce meaningful pause numbers.");
            return 0;
        }

        int iterations = ParseIterations(args);
        Console.WriteLine(
            $"GC-pause harness: {input.Name} ({input.Bytes.Length:N0} bytes) x {iterations} parse iterations.");
        Console.WriteLine();

        var stats = new GcStats();

        // GC.WhenTriggered() fires after every collection; the callback reads the supplied
        // GCMemoryInfo synchronously and copies the numbers out (the span/struct is not retained).
        using GCNotification.Registration registration =
            GC.WhenTriggered().Register(static (GcStats s, GCMemoryInfo info) => s.Record(info), stats);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            GlbDocument document = GlbDocument.Parse(input.Bytes).Value;
            using var parsed = document.ParseGltf();
            _ = parsed.RootElement.Asset.Version.GetString();
        }
        stopwatch.Stop();

        // Notification delivery is async (post-collection, via the thread pool); give in-flight
        // callbacks a moment to drain before reporting so the last collections are counted.
        Thread.Sleep(250);

        stats.Report(stopwatch.Elapsed);
        return 0;
    }

    private static int ParseIterations(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--iterations", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                && value > 0)
            {
                return value;
            }
        }

        return DefaultIterations;
    }

    /// <summary>Thread-safe accumulator for the GC notifications, which arrive on the thread pool.</summary>
    private sealed class GcStats
    {
        private readonly Lock _gate = new();
        private readonly long[] _collectionsByGeneration = new long[GC.MaxGeneration + 1];
        private long _collections;
        private long _pauseSamples;
        private TimeSpan _totalPause;
        private TimeSpan _maxPause;
        private double _maxPauseTimePercentage;
        private long _peakHeapBytes;
        private long _peakFragmentedBytes;

        public void Record(GCMemoryInfo info)
        {
            lock (_gate)
            {
                _collections++;

                int generation = info.Generation;
                if ((uint)generation < (uint)_collectionsByGeneration.Length)
                {
                    _collectionsByGeneration[generation]++;
                }

                foreach (TimeSpan pause in info.PauseDurations)
                {
                    _totalPause += pause;
                    _pauseSamples++;
                    if (pause > _maxPause)
                    {
                        _maxPause = pause;
                    }
                }

                if (info.PauseTimePercentage > _maxPauseTimePercentage)
                {
                    _maxPauseTimePercentage = info.PauseTimePercentage;
                }

                if (info.HeapSizeBytes > _peakHeapBytes)
                {
                    _peakHeapBytes = info.HeapSizeBytes;
                }

                if (info.FragmentedBytes > _peakFragmentedBytes)
                {
                    _peakFragmentedBytes = info.FragmentedBytes;
                }
            }
        }

        public void Report(TimeSpan wallClock)
        {
            lock (_gate)
            {
                TimeSpan averagePause = _pauseSamples == 0
                    ? TimeSpan.Zero
                    : _totalPause / _pauseSamples;

                Console.WriteLine($"Wall clock           : {wallClock.TotalMilliseconds:N1} ms");
                Console.WriteLine($"Collections observed : {_collections:N0}");
                for (int generation = 0; generation < _collectionsByGeneration.Length; generation++)
                {
                    Console.WriteLine($"  gen{generation} collections    : {_collectionsByGeneration[generation]:N0}");
                }

                Console.WriteLine($"Pause samples        : {_pauseSamples:N0}");
                Console.WriteLine($"Total GC pause       : {_totalPause.TotalMilliseconds:N2} ms");
                Console.WriteLine($"Max single pause     : {_maxPause.TotalMilliseconds:N3} ms");
                Console.WriteLine($"Avg pause            : {averagePause.TotalMilliseconds:N3} ms");
                Console.WriteLine($"Max pause-time %     : {_maxPauseTimePercentage:N2} %");
                Console.WriteLine($"Peak heap size       : {_peakHeapBytes:N0} bytes");
                Console.WriteLine($"Peak fragmentation   : {_peakFragmentedBytes:N0} bytes");
            }
        }
    }
}
