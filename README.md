# Match verify tool for Muziekweb identifiers to Wikidata identifiers

Muziekweb Rotterdam, the Netherlands

## Information and backgrounds

Muziekweb has targeted to link their dataset of musical artists and bands to 
Wikidata articles as part of the Trompa project (https://trompamusic.eu/). The 
Mix'n'match tool has proposed many connections but relies on manual 
verification before publishing the links in articles. Both parties store links 
to multiple online sources, as does Muzicbrainz. Combining the three sources 
and their references to other sources can automate the process of verification.

This software tool retrieves links from the named sources and verifies the 
identifiers. When a link is identified, the references are stored in the 
Muziekweb dataset. The links are publically available at the website 
www.muziekweb.nl when opening the 'external links' overlay on a resource page. 
For example:

https://www.muziekweb.nl/en/Link/M00000000271

![Links example](https://raw.githubusercontent.com/CasperCDR/wikidata-match-verify/main/external-links.png "Example of external links on Muziekweb")

## Running the application

This project has dependencies to internal data structures and libraries from 
Muziekweb and can therefore not be run in a local environment. 

## License

```
Copyright 2020 Muziekweb

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```
