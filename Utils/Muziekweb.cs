using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using RestSharp;
using Match_Verify.DataModels;
using Match_Verify.Lib.MuziekwebAPI;
using CDR;
using System.Data.SqlClient;
using System.Data;

namespace Match_Verify
{
    public static class Muziekweb
    {
        public const string SOURCE_STRING = "MUZIEKWEB";
        public const string URL_FORMAT = "https://www.muziekweb.nl/Link/{0}";

#if USE_API
        public const string API_ENDPOINT = "http://api.cdr.nl:8080";
        public const string API_USERNAME = "";
        public const string API_PASSWORD = "";

        public async static Task<MatchSource> GetExternalLinks(string id)
        {
            try
            {
                RestClient http = new RestClient(API_ENDPOINT)
                {
                    UserAgent = "Muziekweb external identifiers validator 1.0 (.NET Core 3.1)"
                };
                http.ClearHandlers(); // Remove all available handlers
                http.Authenticator = new HttpDigestAuthenticator(API_USERNAME, API_PASSWORD);
                http.Timeout = 6000; // niet te lang wachten op antwoord

                RestRequest request = new RestRequest("/link/performerInfo.xml", Method.GET);
                request.AddParameter("performerLink", id);
                request.AddParameter("website", "MUZIEKWEB");
                request.AddParameter("html", 1);
                request.AddParameter("TOKEN", 0);
                
                // execute the request
                IRestResponse response = await http.ExecuteGetAsync(request);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(response.Content);

                var result = new MatchSource
                {
                    Source = SOURCE_STRING,
                    Url = string.Format(URL_FORMAT, id),
                    Identifier = id,
                };

                var relations = doc.SelectNodes("//ExternalLink");
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
            string url = item.SelectSingleNode("./Link").InnerText;
            string id = url.Split('/').Last(); // Most url's use the last part as identifier.

            return new LinkRecord
            {
                Source = item.Attributes["Provider"].Value,
                Identifier = id,
                Url = url
            };
        }
#else
        public async static Task<MatchSource> GetExternalLinks(string id)
        {
            return await Task.Run(() =>
            {
                var result = new MatchSource
                {
                    Source = SOURCE_STRING,
                    Url = string.Format(URL_FORMAT, id),
                    Identifier = id,
                };

                if (DOC5.DataModule.ExecSP_THESAURUS_PERFORMER_S(DB_Helper.MSSQLConnection, id, out DataSet ds))
                {
                    foreach (DataRow item in ds.Tables[2].Rows)
                    {
                        result.AddLink(GetLinkRecordFromXmlElement(item));
                    }
                }
                return result;
            });
        }

        private static LinkRecord GetLinkRecordFromXmlElement(DataRow item)
        {
            string url = item["VALUE"].ToString();
            string id = url.Split('/').Last(); // Most url's use the last part as identifier.

            return new LinkRecord
            {
                Source = item["DATALABEL_CODE"].ToString(),
                Identifier = id,
                Url = url
            };
        }
#endif
    }
}
