using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Web;
using System.Collections.Generic;
using System.Threading;
using Match_Verify.DataModels;

namespace Match_Verify
{
    public static class Wikidata
    {
        public const string SOURCE_STRING = "WIKIDATA";
        public const string SPARQL_ENDPOINT = "https://query.wikidata.org/sparql";
        public const string URL_FORMAT = "https://www.wikidata.org/wiki/{0}";

        private static readonly Dictionary<string, string> linkNames = new Dictionary<string, string>()
        {
            { "Muziekweb performer ID", "MUZIEKWEB" },
            { "MusicBrainz artist ID", "MUSICBRAINZ" },
            { "Discogs artist ID", "DISCOGS" },
            { "AllMusic artist ID", "ALLMUSIC" },
            { "Last.fm ID", "LASTFM" },
            { "VIAF ID", "VIAF" },
            { "ISNI", "ISNI" },
            { "IMSLP ID", "IMSLP" },
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
                string query = 
                    "PREFIX wikibase: <http://wikiba.se/ontology#> " +
                    "PREFIX wd: <http://www.wikidata.org/entity/> " +
                    "PREFIX bd: <http://www.bigdata.com/rdf#> " +
                    "SELECT ?property ?propertyLabel ?value ?formatterURL ?url " +
                    "WHERE { " +
                    "  wd:" + id + " ?propertyclaim ?value. " +
                    "  ?property wikibase:directClaim ?propertyclaim. " +
                    "  ?property wikibase:propertyType wikibase:ExternalId. " +
                    "  OPTIONAL { " +
                    "    ?property wdt:P1630 ?formatterURL. " +
                    "  } " +
                    "  BIND(IF(BOUND(?formatterURL), IRI(REPLACE(?formatterURL, \"\\\\$1\", ?value)), ?value) AS ?url) " +
                    "  SERVICE wikibase:label { bd:serviceParam wikibase:language \"en\". } " +
                    "}";

                var url = SPARQL_ENDPOINT + "?query=" + HttpUtility.UrlEncode(query) + "&format=json";

                var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Muziekweb external identifiers validator 1.0 (.NET Core 3.1)");
                //client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
                
                var json = await client.GetStringAsync(url);
                lastRequest = DateTime.Now;

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var entities = root.GetProperty("results").GetProperty("bindings");

                var result = new MatchSource
                {
                    Source = SOURCE_STRING,
                    Url = string.Format(URL_FORMAT, id),
                    Identifier = id,
                };

                foreach (var item in entities.EnumerateArray())
                {
                    result.AddLink(GetLinkRecordFromJsonElement(item));
                }
                return result;
            }
            catch (Exception e)
            {
                Serilog.Log.Logger.Error(e.ToString());
                if (e.InnerException != null)
                {
                    Serilog.Log.Logger.Error(e.InnerException.ToString());
                }
            }

            return null;
        }

        private static LinkRecord GetLinkRecordFromJsonElement(JsonElement item)
        {
            var source = string.Empty;
            if (item.TryGetProperty("propertyLabel", out JsonElement itemLabel))
            {
                source = itemLabel.GetProperty("value").ToString();

                if (!linkNames.ContainsKey(source))
                {
                    // Return nothing if the source is not in the list of linknames.
                    return null;
                }
            }

            var ident = string.Empty;
            if (item.TryGetProperty("value", out JsonElement itemDescription))
            {
                ident = itemDescription.GetProperty("value").ToString();
            }

            var url = string.Empty;
            if (item.TryGetProperty("url", out JsonElement article))
            {
                url = article.GetProperty("value").ToString();
            }


            return new LinkRecord
            {
                Source = linkNames[source],
                Identifier = ident.Replace(" ", string.Empty),
                Url = url
            };
        }
    }
}
