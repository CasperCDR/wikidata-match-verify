using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Match_Verify.DataModels
{
    public class LinkRecord
    {
        public string Source;

        public string Identifier;

        public string Url;

        public override string ToString()
        {
            return $"{Source}:{Identifier}";
        }
    }

    public class MatchSource : LinkRecord
    {
        public List<LinkRecord> Links = new List<LinkRecord>();

        public void AddLink(LinkRecord link)
        {
            if (link != null && 
                !string.IsNullOrEmpty(link.Identifier) &&
                !Links.Any(l => l.Source.Equals(link.Source) && l.Identifier.Equals(link.Identifier)))
            {
                Links.Add(link);
            }
        }
    }
}
