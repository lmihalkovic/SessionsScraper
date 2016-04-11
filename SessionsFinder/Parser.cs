using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

using HtmlAgilityPack;
using HtmlAgilityPackPlus;

using Foundation;

using DataModel;

using Newtonsoft.Json;

namespace MicrosoftBuildExtractor 
{
    using SessionsFinder;

     class CBuild {
        public const string BASE = "https://channel9.msdn.com/";
        public const string BUILD2016 = "Events/Build/2016";
        public const string BUILD2016_SESSIONS = "Events/Build/2016?sort=sequential&direction=desc&page={0}";
        public const int BUILD2016_COUNT = 50;

        public const string BUILD2015 = "Events/Build/2015";                
        public const string BUILD2015_SESSIONS = "Events/Build/2015?sort=sequential&direction=desc&page={0}";                
        public const int BUILD2015_COUNT = 50;
    }

    public class Build2016 : IExtractor {

        Dictionary<string, Session> sessions;

        public Dictionary<string, Session> GetSessions() {
            extract().Wait();
            return sessions;
        }

        public String GetId() {
            return "Build 2016";
        }

        public String SerialiseToJson(Dictionary<string, Session> sessions) {
            var list = new List<SessionDesc>();
            foreach(Session session in sessions.Values) {
                if (session.VideoURL != null && session.VideoURL != "") {
                    SessionDesc desc = new SessionDesc();
                    desc.Id = session.UniqueId;
                    desc.Year = session.Year;
                    desc.Url = session.VideoURL;
                    desc.Title = session.Title;
                    desc.Description = session.Summary;

                    list.Add(desc);
                }
            }
            var all = new SessionsDesc();
            all.Sessions = list.ToArray();
            all.Updated = "11/11/11";

            // save to JSON
            string json = JsonConvert.SerializeObject(all, Formatting.Indented);
            return json;
//            MemoryStream stream1 = new MemoryStream();
//            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(SessionsDesc));
//            ser.WriteObject(stream1, all);
//
//            stream1.Position = 0;
//            StreamReader sr = new StreamReader(stream1);
//
//            return sr.ReadToEnd();
        }

        async Task extract() {
            try {
                Task task = Task.Run(async delegate {
                    Task<Dictionary<string, Session>> tsk = parseSimpleList();
                    Dictionary<string, Session> sessions = await tsk;
                    this.sessions = sessions;
                    Console.WriteLine("LIST-DONE");
                });
                task.Wait();

                task = Task.Run(async delegate {
                    IEnumerable<Task<Session>> asyncOps = from session in sessions.Values select parseSessionDetails(session);
                    await Task.WhenAll(asyncOps);
                    Console.WriteLine("DETAILS-DONE");
                });
                task.Wait();

            } catch (Exception ex) {
                Console.WriteLine($"PROBLEM: {ex}");
            }
        }

        async Task<Dictionary<string, Session>> parseSimpleList() {
            try {
                List<String> urls = new List<String>() {
                    "https://channel9.msdn.com/Events/Build/2016?sort=status&page=1&direction=asc#tab_sortBy_status" 
                    , "https://channel9.msdn.com/Events/Build/2016?sort=status&page=2&direction=asc#tab_sortBy_status" 
                    , "https://channel9.msdn.com/Events/Build/2016?sort=status&page=3&direction=asc#tab_sortBy_status" 
                    , "https://channel9.msdn.com/Events/Build/2016?sort=status&page=4&direction=asc#tab_sortBy_status" 
                };

                Dictionary<string, Session> sessions = new Dictionary<string, Session>();
                foreach( var url in urls) {
                    var data = await Loader.LoadPageSource(url, Constants.AsJSON);
                    var source = data.ToString();
                    Parser.ProcessList(source, sessions);
                }

                return sessions;
            } catch (Exception ex) {
                Console.WriteLine($"PROBLEM: {ex}");
                return null;
            }
        }

        async Task<Session> parseSessionDetails(Session session) {
            var url = CBuild.BASE + session.UniqueId;
            var data = await Loader.LoadPageSource(url, null);
            var source = data?.ToString();
            if(source != null) {                    
                Parser.ProcessSession(source, session);
            }
            return session;
        }

    }
        
    class Parser
    {
        public Parser()
        {
        }
            
        public static void ProcessList(string str, Dictionary<String, Session> sessions)
        {
            var source = WebUtility.HtmlDecode(str);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(source);

            // div[class="entry-meta"]
            IEnumerable<HtmlNode> nodes = doc.DocumentNode.SelectNodes("//div[@class='entry-meta']");  
            foreach (HtmlNode node in nodes) {
                var session = new Session();
                HtmlNode n = null;
                try {
                    // id
                    n = node.FirstLink();
                    session.UniqueId = n?.Attributes["href"].Value ?? "";
                    session.Title = n?.InnerText;

                    // List 
                    var ul = node.Descendants().Where( x=>x.Name == "ul").First();

                    // track
                    n = ul.FirstDescendantMatching(x => { 
                        return x.Name == "a" && 
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "grouping sessionType";
                    });
                    session.Track = n?.InnerText;

                    // slides
                    n = ul.FirstDescendantMatching(x => { 
                        return x.Name == "a" && 
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "slides presentation";
                    });
                    session.SlidesURL = n?.Attributes["href"].Value ?? "";

                    // time
                    n = ul.FirstDescendantMatching(x => { 
                        return x.Name == "time" && 
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "timing date";
                    });
                    session.Date = n?.FirstChild.InnerText;

                    // 
                    session.Year = 2016;
                        
                    if (sessions.ContainsKey(session.UniqueId)) {
                        Console.WriteLine($"Duplicate entry: {session.UniqueId}");
                    } else {
                        sessions.Add(session.UniqueId, session);
                    }

                } catch (Exception ex) {
                    Console.WriteLine($"PROBLEM: {ex}");
                }
            }
        }

        public static void ProcessSession(string str, Session session) {
            var source = WebUtility.HtmlDecode(str);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(source);

            IEnumerable<HtmlNode> nodes = doc.DocumentNode.SelectNodes("//div[@class='playerContainer' or @class='entry-content']");
            if (nodes == null)
                return;

            foreach (HtmlNode div in nodes) {

                var cls = div.Attributes["class"].Value;
                if (cls == "playerContainer") {
                    HtmlNode player = div.FirstDescendantMatching(x => x.Name == "a");
                    session.VideoURL = player.Attributes["href"]?.Value;
                } else if (cls == "entry-content") {
                    HtmlNode n = div.FirstDescendantMatching(x => x.Name == "div" && x.Attributes["id"]?.Value == "entry-body");
                    session.Summary = n?.InnerText;
                }

            }

        }

    }

}

namespace SessionsFinder
{
    public static class Constants {
        public const string Accept = "Accept=text/html,application/xhtml+xml,application/xml,application/json;q=0.9,*/*;q=0.8";
        public const string AsJSON = "application/json";
    }

    public interface IExtractor {
        Dictionary<String, Session> GetSessions();
        String SerialiseToJson(Dictionary<string, Session> sessions);
        String GetId();
    }

    public class Loader
    {
        private static NSUrlSessionDataTask Task;

        public Loader()
        {
        }

        public static async Task<NSData> LoadPageSource(string URL, string Type)
        {
            var session = NSUrlSession.SharedSession;       

            NSUrl target = NSUrl.FromString(URL);
            NSMutableUrlRequest request = new NSMutableUrlRequest(target);
            request.HttpMethod = "GET";    
            request["Content-Type"] = Type ?? "text/html";

            //  string json = JsonConvert.SerializeObject(m);    
            //  var body = NSData.FromString(json);
            var TaskRequest = session.CreateDataTaskAsync(request, out Task);       
            Task.Resume();

            var taskResponse = await TaskRequest;
            if (taskResponse == null || taskResponse.Response == null) {
                Console.WriteLine(Task.Error);
                return null;
            } else {
                return taskResponse.Data;
            }
        }

    }
}

namespace HtmlAgilityPackPlus {
    using HtmlAgilityPack;

    public static class Extender {

        public static HtmlNode ChildOfType(this HtmlNode node, string name) {
            var n = node.ChildNodes.Where( x => x.Name == name).First();
            return n;
        }
            
        public static HtmlNode FirstLink(this HtmlNode node) {
            var child = node.ChildOfType("a");
            return child;
        }

        public static HtmlNode FirstDescendantMatching(this HtmlNode node, Func<HtmlNode, bool> predicate) {
            var seq = node.Descendants().Where( x => predicate(x) );
            return ((seq?.Count() ?? 0) > 0) ? seq.First() : null;
        }

    }

}
