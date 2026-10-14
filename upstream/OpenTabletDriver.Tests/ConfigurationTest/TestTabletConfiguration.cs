using System;
using System.Collections.Generic;
using System.IO;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Tests.ConfigurationTest
{
    public record TestTabletConfiguration
    {
        public required Lazy<TabletConfiguration> Configuration { get; init; }
        public required FileInfo File { get; init; }
        public required Lazy<string> FileContents { get; init; }

        public string FileShortName => $"{this.File.Directory?.Name ?? "unknown"}/{this.File.Name}";

        public override string ToString() => this.FileShortName;

        public IEnumerable<TestTypes> SkippedTestTypes
        {
            get
            {
                if (this.Configuration.Value.Attributes?.ContainsKey(Consts.SKIP_TESTS_ATTRIBUTE_KEY) ?? false)
                {
                    foreach (string testType in this.Configuration.Value.Attributes[Consts.SKIP_TESTS_ATTRIBUTE_KEY]
                                 .Split(','))
                    {
                        if (!Enum.TryParse<TestTypes>(testType, out var result))
                            throw new ArgumentException($"Invalid value type {testType}");
                        yield return result;
                    }
                }
            }
        }

        public IEnumerable<int> ValidLPIsForTablet
        {
            get
            {
                if (this.Configuration.Value.Attributes?.ContainsKey(Consts.VERIFIED_LPI_KEY) ?? false)
                {
                    string val = this.Configuration.Value.Attributes[Consts.VERIFIED_LPI_KEY];
                    if (!int.TryParse(val, out var result))
                        throw new ArgumentException($"Invalid LPI integer '{val}'");
                    yield return result;
                }

                // loosely ordered by regularity
                yield return 5080; // 200 LPMM
                yield return 2540; // 100 LPMM
                yield return 4000; // seen on some older Huion tablets
                yield return 2000; // seen on e.g. FlooGoo FMA100, Genius G-Pen 560
                yield return 1016; // 40 LPMM, e.g. Wacom CTF-430 / Wacom FT-0405-U
                yield return 2032; // 80 LPMM, older Wacoms (or XP-Pen Artist 22HD)
                yield return 10160; // 400 LPMM, seen on XP-Pen Star G540 Pro
                yield return 508; // 20 LPMM, e.g. Wacom PL-800
                // these aren't seen yet but are known to be correct as well:
                // yield return 1270; // 50 LPMM
            }
        }
    }
}
