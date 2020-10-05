using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Match_Verify.DataModels;
using System.Linq;
using System.Data.SqlClient;
using CDR;

namespace Match_Verify
{
    class Worker
    {
        private readonly ILogger logger;
        private readonly char[] logTrimChars = new char[] { '\r', '\n' };

        private readonly string[] saveSources = new string[] { "WIKIDATA", "VIAF", "LASTFM", "ALLMUSIC", "IMSLP" };

        private static bool consoleInput = true;
        private static bool userIntercept = false;

        public Worker(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> Verify(string inputFile)
        {
            try
            {
                DataTable data = await GetDataTableFromFile(inputFile);

                if (IsMixNMatchData(data))
                {
                    Console.WriteLine("Starting verify for Wikidata matches");
                    Console.WriteLine();

                    SqlConnection conn = DB_Helper.MSSQLConnection;

                    int itemsChecked = 0;
                    int itemsMatched = 0;

                    foreach (DataRow row in data.Rows)
                    {
                        string mwLink = row["external_id"].ToString();
                        string wdIdentifier = row["q"].ToString();
                        string matchedBy = row["matched_by_username"].ToString();
                        itemsChecked++;

                        if (string.IsNullOrEmpty(wdIdentifier))
                        {
                            LogOnScreen($"No Wikidata link for {mwLink}.");
                        }
                        else
                        {
                            LogOnScreen($"Checking matched {mwLink} to {wdIdentifier}");

                            // Get all the links to other sources so we can match the identifiers from all the datasets.
                            var matchings = await GetMatchData(mwLink, wdIdentifier);

                            if (CheckWikidataMatch(matchedBy, matchings))
                            {
                                itemsMatched++;
                                LogOnScreen($"Wikidata link {mwLink} to {wdIdentifier} approved.");
                                // First get the performerId
                                MatchSource mwSource = matchings[Muziekweb.SOURCE_STRING];
                                if (DOC5.DataModule.ExecSP_THESAURUS_PERFORMER_S(conn, mwSource.Identifier, out DataSet ds))
                                {
                                    long performerId = (long)ds.Tables[0].Rows[0]["PERFORMER_ID"];
                                    int savedLinks = 0;

                                    // And save links from the matched sources.
                                    foreach (MatchSource source in matchings.Values)
                                    {
                                        if (!source.Source.Equals(Muziekweb.SOURCE_STRING))
                                        {
                                            savedLinks += SaveLinks(conn, performerId, mwSource, source);
                                        }
                                    }

                                    LogOnScreen($" > {savedLinks} new links saved for {mwLink}.");
                                }
                            }
                            else
                            {
                                // Log that there was a mismatch
                                LogOnScreen($"Wikidata link {mwLink} to {wdIdentifier} declined.");
                            }
                        }

                        if (UserIntercept())
                        {
                            LogOnScreen("User has aborted the process.");
                            break;
                        }
                    }

                    Console.WriteLine();
                    LogOnScreen($"Process finished. {itemsChecked} items checked, {itemsMatched} records matched.");


                    return true;
                }
                else
                {
                    LogOnScreen($"File is not a valid Mix'n'Match export format.");
                }
            }
            catch (Exception e)
            {
                Console.Write("An exception occured. Please check the log for more information.");
                logger.Error(e.Message);
            }

            return false;
        }

        public async Task<Dictionary<string, MatchSource>> GetMatchData(string mwLink, string wdIdentifier)
        {
            Dictionary<string, MatchSource> matchings = new Dictionary<string, MatchSource>
            {
                { Muziekweb.SOURCE_STRING, await Muziekweb.GetExternalLinks(mwLink) },
                { Wikidata.SOURCE_STRING, await Wikidata.GetExternalLinks(wdIdentifier) }
            };

            foreach (var item in matchings)
            {
                LinkRecord link = item.Value?.Links.FirstOrDefault(l => l.Source.Equals(MusicBrainz.SOURCE_STRING));

                if (link != null)
                {
                    matchings.Add(link.Source, await MusicBrainz.GetExternalLinks(link.Identifier));
                    // And leave the loop when the MusicBrainz match source is filled.
                    break;
                }
            }

            return matchings;
        }

        public bool CheckWikidataMatch(string matchedBy, Dictionary<string, MatchSource> matchings)
        {
            // Test the links in the set by counting the number of occurences for each link.
            Dictionary<string, int> links = new Dictionary<string, int>();
            foreach (MatchSource matchSource in matchings.Values)
            {
                if (matchSource != null)
                {
                    CountLink(links, matchSource.ToString());

                    foreach (LinkRecord link in matchSource.Links)
                    {
                        CountLink(links, link.ToString());
                    }
                }
            }

            // Now we should have some counters matching the number of sources. Otherwise the links
            // point to different entities in the selected source and the match should be declined.
            bool result = false;
            // A minimum of 3 similar links are required. When more sources are available, the number 
            // of sources should be matched.
            int sourceCount = Math.Max(3, matchings.Count);
            foreach (var item in links)
            {
                // Print the matches
                LogOnScreen($"  {item.Key}: {item.Value}");

                if (item.Value >= sourceCount)
                {
                    result = true;
                }
            }

            // When the match is already verified in Wikidata, the return value can be true.
            if (!string.IsNullOrWhiteSpace(matchedBy) && !matchedBy.Equals("automatic"))
            {
                // Match is verified by a community member or the 
                LogOnScreen($"  The match is verified by: {matchedBy}");
                result = true;
            }

            // Special case for Wikidata > ISNI > Muziekweb. When the ISNI identifier is equal, the
            // match can be considered valid because ISNI has validated the entity on both sides.
            LinkRecord mwISNI = matchings[Muziekweb.SOURCE_STRING]?.Links.FirstOrDefault(l => l.Source.Equals("ISNI"));
            LinkRecord wdISNI = matchings[Wikidata.SOURCE_STRING]?.Links.FirstOrDefault(l => l.Source.Equals("ISNI"));
            result |= (mwISNI != null && wdISNI != null && mwISNI.Identifier.Equals(wdISNI.Identifier));

            return result;
        }

        private void CountLink(Dictionary<string, int> links, string key)
        {
            if (links.ContainsKey(key))
            {
                links[key] += 1;
            }
            else
            {
                links.Add(key, 1);
            }
        }

        /// <summary>
        /// Reads the text bases data from file and returns a datatable with the data and columns.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        private async Task<DataTable> GetDataTableFromFile(string inputFile)
        {
            char[] seperators = new char[3] { '\t', ';', ',' };

            if (!File.Exists(inputFile))
            {
                throw new Exception($"Input file \"{inputFile}\" not found.");
            }

            using StreamReader reader = File.OpenText(inputFile);

            DataTable result = new DataTable();
            string line = await reader.ReadLineAsync();

            // Test the separator in the file.
            char separator = (char)0;
            foreach (char s in seperators)
            {
                if (line.Contains(s))
                {
                    separator = s;
                    break;
                }
            }

            if (separator.Equals((char)0))
            {
                throw new Exception($"No column separator found in file \"{inputFile}\".");
            }

            // First line should be the columns
            foreach (string colName in line.Split(separator))
            {
                result.Columns.Add(new DataColumn(colName));
            }

            // The rest is data
            while (!reader.EndOfStream)
            {
                line = await reader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Rows.Add(line.Split(separator));
                }
            }

            return result;
        }

        /// <summary>
        /// Test if the data provided is matching the Mix'n'Match columns we expect to be available.
        /// The mix'n'match data should have the columns:
        ///     #entry_id
        ///     catalog
        ///     external_id
        ///     external_url
        ///     name
        ///     description
        ///     entry_type
        ///     mnm_user_id
        ///     q
        ///     matched_on
        ///     matched_by_username
        ///     multi_match_candidates
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool IsMixNMatchData(DataTable data)
        {
            return data.Columns.Contains("external_id")
                && data.Columns.Contains("q");
        }

        /// <summary>
        /// Log message in logfile and on the console.
        /// </summary>
        /// <param name="message"></param>
        private void LogOnScreen(string message)
        {
            Console.WriteLine(message);
            logger.Information(message.Trim(logTrimChars));
        }

        /// <summary>
        /// Returns true when the user wants to break the application. The user
        /// can do so by pressing the Escape key.
        /// This function works when running in the console or as service. When
        /// the application is started as service, the userinput test generates
        /// an error because no userinteraction is possible.
        /// </summary>
        /// <returns>Returns True when the user hits the Escape key.</returns>
        private bool UserIntercept()
        {
            try
            {
                if (consoleInput && !userIntercept && Console.KeyAvailable)
                {
                    userIntercept = Console.ReadKey().Key.Equals(ConsoleKey.Escape);
                    if (userIntercept)
                    {
                        Console.WriteLine(" > User interrupted the run.");
                    }
                }
            }
            catch
            {
                // This happens when the application is started as cron-job
                consoleInput = false;
            }
            return userIntercept;
        }

        private int SaveLinks(SqlConnection conn, long performerId, MatchSource mwSource, MatchSource matchSource)
        {
            int savedLinks = 0;
            if (SaveLinkForSource(matchSource.Source, mwSource) &&
                DOC5.DataModule.ExecSP_PERFORMER_LOOKUP_IDENTIFIER_IU(conn, performerId, matchSource))
            {
                // Add the link so links will not be saved twice.
                mwSource.Links.Add(matchSource);
                savedLinks++;
            }

            foreach (LinkRecord link in matchSource.Links)
            {
                if (SaveLinkForSource(link.Source, mwSource) &&
                    DOC5.DataModule.ExecSP_PERFORMER_LOOKUP_IDENTIFIER_IU(conn, performerId, link))
                {
                    // Add the link so links will not be saved twice.
                    mwSource.Links.Add(link);
                    savedLinks++;
                }
            }

            return savedLinks;
        }

        private bool SaveLinkForSource(string source, MatchSource mwSource)
        {
            return saveSources.Contains(source) && !mwSource.Links.Exists(l => l.Source.Equals(source) && !string.IsNullOrEmpty(l.Identifier));
        }
    }
}
