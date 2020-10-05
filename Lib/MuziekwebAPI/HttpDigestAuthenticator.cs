using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Linq;
using System.Text;

namespace Match_Verify.Lib.MuziekwebAPI
{
    internal class HttpDigestAuthenticator : IAuthenticator
    {
		private readonly string _username;
		private readonly string _password;

		public HttpDigestAuthenticator(string username, string password)
		{
			_password = password;
			_username = username;
		}

		public void Authenticate(IRestClient client, IRestRequest request)
		{
			// NetworkCredentials always makes two trips, even if with PreAuthenticate,
			// it is also unsafe for many partial trust scenarios
			// request.Credentials = Credentials;
			// thanks TweetSharp!
			request.Credentials = new System.Net.NetworkCredential(_username, _password);

			// only add the Authorization parameter if it hasn't been added by a previous Execute
			if (!request.Parameters.Any(p => p.Name.Equals("Authorization", StringComparison.InvariantCultureIgnoreCase)))
			{
				var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", _username, _password)));
				var authHeader = string.Format("Basic {0}", token);
				request.AddParameter("Authorization", authHeader, ParameterType.HttpHeader);
			}
		}
	}
}