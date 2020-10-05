using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using RestSharp;
using Match_Verify.DataModels;

namespace Match_Verify
{
    public static class MusicBrainz
    {
        public const string SOURCE_STRING = "MUSICBRAINZ";
        public const string URL_FORMAT = "https://musicbrainz.org/artist/{0}";

        public const string API_ENDPOINT = "http://musicbrainz.org";

        private static readonly Dictionary<string, string> linkNames = new Dictionary<string, string>()
        {
            { "wikidata", "WIKIDATA" },
            { "discogs", "DISCOGS" },
            { "allmusic", "ALLMUSIC" },
            { "last.fm", "LASTFM" },
            { "VIAF", "VIAF" },
            { "ISNI", "ISNI" },
        };

        private const int MAX_REQUESTS_PER_MINUTE = 60;

        private static DateTime lastRequest = DateTime.MinValue;

        public async static Task<MatchSource> GetExternalLinks(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            while (DateTime.Now.Subtract(lastRequest).TotalMilliseconds < 60000 / MAX_REQUESTS_PER_MINUTE)
            {
                Thread.Sleep(30000 / MAX_REQUESTS_PER_MINUTE);
            }

            try
            {
                RestClient http = new RestClient(API_ENDPOINT)
                {
                    UserAgent = "Muziekweb external identifiers validator 1.0 (.NET Core 3.1)"
                };
                RestRequest request = new RestRequest($"/ws/2/artist/{id}", Method.GET);
                request.AddParameter("inc", "url-rels");
                request.AddParameter("fmt", "xml");

                // execute the request
                IRestResponse response = await http.ExecuteGetAsync(request);

                // Load the Xml in the XmlDocument
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(response.Content.Replace("xmlns=\"http://musicbrainz.org/ns/mmd-2.0#\"", ""));
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                lastRequest = DateTime.Now;

                var result = new MatchSource
                {
                    Source = SOURCE_STRING,
                    Url = string.Format(URL_FORMAT, id),
                    Identifier = id,
                };

                var isniList = doc.SelectNodes("//isni", nsmgr);
                foreach (XmlNode isni in isniList)
                {
                    result.AddLink(new LinkRecord
                    {
                        Source = "ISNI",
                        Identifier = isni.InnerText,
                        Url = $"https://isni.oclc.org/xslt/DB=1.2//CMD?ACT=SRCH&IKT=8006&TRM=ISN%3A{isni.InnerText}"
                    });
                }

                var relations = doc.SelectNodes("//relation", nsmgr);
                foreach (XmlNode item in relations)
                {
                    result.AddLink(GetLinkRecordFromXmlElement(item));
                }

                return result;
            }
            catch (Exception e)
            {
                Serilog.Log.Logger.Error(e.ToString());
            }

            return null;
        }

        private static LinkRecord GetLinkRecordFromXmlElement(XmlNode item)
        {
            string source = item.Attributes["type"].Value;
            if (!linkNames.ContainsKey(source))
            {
                return null;
            }
            string url = item.SelectSingleNode("./target").InnerText;
            string id = url.Split('/').Last(); // Most url's use the last part as identifier.

            return new LinkRecord
            {
                Source = linkNames[source],
                Identifier = id,
                Url = url
            };
        }
    }
}
