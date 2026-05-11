using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace JukeboxSpotify
{
    internal static class MediaSourceProcessResolver
    {
        public static int? ResolveProcessId(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                Plugin.LogDebug("Skipping process resolution because the media source id is empty.");
                return null;
            }

            Plugin.LogDebug("Resolving process id for media source '" + sourceId + "'.");

            List<string> candidates = BuildCandidates(sourceId);
            if (candidates.Count == 0)
            {
                Plugin.LogDebug("No process candidates were derived from media source '" + sourceId + "'.");
                return null;
            }

            Plugin.LogDebug("Process resolution candidates for '" + sourceId + "': " + string.Join(", ", candidates));

            Process[] processes = Process.GetProcesses();
            try
            {
                Plugin.LogDebug("Scanning " + processes.Length + " processes for media source '" + sourceId + "'.");

                foreach (string candidate in candidates)
                {
                    Process exactMatch = processes.FirstOrDefault(process =>
                        string.Equals(process.ProcessName, candidate, StringComparison.OrdinalIgnoreCase));

                    if (exactMatch != null)
                    {
                        Plugin.LogDebug("Resolved media source '" + sourceId + "' to process via exact match: " + DescribeProcess(exactMatch));
                        return exactMatch.Id;
                    }
                }

                foreach (string candidate in candidates)
                {
                    Process prefixMatch = processes.FirstOrDefault(process =>
                        process.ProcessName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

                    if (prefixMatch != null)
                    {
                        Plugin.LogDebug("Resolved media source '" + sourceId + "' to process via prefix match: " + DescribeProcess(prefixMatch));
                        return prefixMatch.Id;
                    }
                }

                foreach (string candidate in candidates)
                {
                    Process titleMatch = processes.FirstOrDefault(process =>
                    {
                        try
                        {
                            return !string.IsNullOrWhiteSpace(process.MainWindowTitle) &&
                                process.MainWindowTitle.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    if (titleMatch != null)
                    {
                        Plugin.LogDebug("Resolved media source '" + sourceId + "' to process via window-title match: " + DescribeProcess(titleMatch));
                        return titleMatch.Id;
                    }
                }

                Plugin.LogDebug("Failed to resolve a process id for media source '" + sourceId + "'.");
                return null;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static List<string> BuildCandidates(string sourceId)
        {
            HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string value)
            {
                string normalized = NormalizeToken(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    candidates.Add(normalized);
                }
            }

            AddCandidate(sourceId);

            int bangIndex = sourceId.LastIndexOf('!');
            if (bangIndex >= 0 && bangIndex < sourceId.Length - 1)
            {
                AddCandidate(sourceId[(bangIndex + 1)..]);
            }

            int underscoreIndex = sourceId.IndexOf('_');
            if (underscoreIndex > 0)
            {
                AddCandidate(sourceId[..underscoreIndex]);
            }

            foreach (string token in sourceId.Split(new[] { '!', '.', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                AddCandidate(token);
            }

            return candidates
                .Where(candidate => candidate.Length > 1)
                .OrderByDescending(candidate => candidate.Length)
                .ToList();
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            string normalized = builder.ToString();
            return IsNoiseToken(normalized) ? string.Empty : normalized;
        }

        private static bool IsNoiseToken(string token)
        {
            return token.Equals("Application", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Desktop", StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeProcess(Process process)
        {
            try
            {
                return "id=" + process.Id + ", name='" + process.ProcessName + "', windowTitle='" + process.MainWindowTitle + "'";
            }
            catch (Exception e)
            {
                return "id=" + process.Id + ", descriptionUnavailable=" + e.GetType().Name;
            }
        }
    }
}