using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

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
    using ATATimeUtil;

     class CBuild {
        public const string BASE = "https://channel9.msdn.com";
        public const string BUILD2016 = "Events/Build/2016";
        public const string BUILD2016_SESSIONS = "Events/Build/2016?sort=sequential&direction=desc&page={0}";
        public const int BUILD2016_COUNT = 50;

        public const string BUILD2015 = "Events/Build/2015";                
        public const string BUILD2015_SESSIONS = "Events/Build/2015?sort=sequential&direction=desc&page={0}";                
        public const int BUILD2015_COUNT = 50;
    }

    public class Build2016 : IExtractor {

        Dictionary<string, Session> sessions;

        public String GetId() {
            return "Build 2016";
        }

        public int EventYear {
            get { return 2016; }
        }

        public Dictionary<string, Session> GetSessions(IProgress<ExtractorProgress> updater) {
            extract(updater).Wait();
            return sessions;
        }

        public String SerialiseToJson(IProgress<ExtractorProgress> updater, Dictionary<string, Session> sessions) {
            var list = new List<SessionDesc>();
            foreach(Session session in sessions.Values) {
                if (session.VideoURL != null && session.VideoURL != "") {
                    SessionDesc desc = new SessionDesc();
                    desc.UniqueId = session.UniqueId;
                    desc.Title = session.Title;
                    desc.Description = session.Summary ?? "";
                    desc.Year = session.Year;
                    desc.Date = session.Date;
                    desc.Url = session.VideoURL;
                    desc.Track = session.Track ?? "General";

                    list.Add(desc);
                }
            }
            var all = new SessionsDesc();
            all.Sessions = list.ToArray();
            all.Updated = Parser.FormatDate(Parser.ComputeUpdateDate());

            // save to JSON
            string json = JsonConvert.SerializeObject(all, Formatting.Indented);
            return json;
        }

        #region Internal helpers
        async Task extract(IProgress<ExtractorProgress> updater) {
            try {
                Parser parser = new Parser(this);
                Task task = Task.Run(async delegate {
                    Task<Dictionary<string, Session>> tsk = parseSimpleList(updater, parser);
                    Dictionary<string, Session> sessions = await tsk;
                    this.sessions = sessions;
                    Console.WriteLine("LIST-DONE");
                });
                task.Wait();

                task = Task.Run(async delegate {
                    IEnumerable<Task<Session>> asyncOps = from session in sessions.Values select parseSessionDetails(updater, parser, session);
                    await Task.WhenAll(asyncOps);
                    Console.WriteLine("DETAILS-DONE");
                });
                task.Wait();

            } catch (Exception ex) {
                Console.WriteLine($"PROBLEM: {ex}");
            }
        }

        async Task<Dictionary<string, Session>> parseSimpleList(IProgress<ExtractorProgress> updater, Parser parser) {
            try {
                var urls = new List<Tuple<String, String>>() {
                    new Tuple<String, String>("Page 1", "https://channel9.msdn.com/Events/Build/2016?sort=status&page=1&direction=asc#tab_sortBy_status") 
                    , new Tuple<String, String>("Page 2", "https://channel9.msdn.com/Events/Build/2016?sort=status&page=2&direction=asc#tab_sortBy_status") 
                    , new Tuple<String, String>("Page 3", "https://channel9.msdn.com/Events/Build/2016?sort=status&page=3&direction=asc#tab_sortBy_status") 
                    , new Tuple<String, String>("Page 4", "https://channel9.msdn.com/Events/Build/2016?sort=status&page=4&direction=asc#tab_sortBy_status") 
                };

                Dictionary<string, Session> sessions = new Dictionary<string, Session>();

                var step = new ExtractorProgress();
                foreach( var url in urls) {
                    step.Message = string.Format("{0} : {1}", GetId(),  url.Item1);
                    updater.Report(step);
                    var data = await Loader.LoadPageSource(url.Item2, Constants.AsJSON);
                    var source = data.ToString();
                    parser.ProcessList(source, sessions);
                }

                return sessions;
            } catch (Exception ex) {
                Console.WriteLine($"PROBLEM: {ex}");
                return null;
            }
        }

        async Task<Session> parseSessionDetails(IProgress<ExtractorProgress> updater, Parser parser, Session session) {
            var url = CBuild.BASE + session.UniqueId;
            var data = await Loader.LoadPageSource(url, null);
            updater.Report(new ExtractorProgress(string.Format(" + processing: {0}", session.UniqueId)));
            var source = data?.ToString();
            if(source != null) {                    
                parser.ProcessSession(source, session);
            }
            return session;
        }
        #endregion
    }
        
    class Parser
    {
        private IExtractor extractor;
        public Parser(IExtractor extractor)
        {
            this.extractor = extractor;
        }
            
        public void ProcessList(string str, Dictionary<String, Session> sessions)
        {
            var source = WebUtility.HtmlDecode(str);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(source);

            // selection must be narrow to avoid dups
            IEnumerable<HtmlNode> nodes = doc.DocumentNode.SelectNodes("//ul[contains(@class,'sessionList')]/descendant::div[@class='entry-meta']");  
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
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "grouping level";
                    });
                    session.Track = n?.InnerText;

                    // slides
                    n = ul.FirstDescendantMatching(x => { 
                        return x.Name == "a" && 
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "slides presentation";
                    });
                    session.SlidesURL = n?.Attributes["href"].Value ?? "";

                    // date
                    n = ul.FirstDescendantMatching(x => { 
                        return x.Name == "time" && 
                            (x.ParentNode?.Attributes["class"].Value ?? "") == "timing date";
                    });
                    var tm = n?.Attributes["datetime"].Value ?? "";
                    var tz = n?.Attributes["data-timezonename"].Value ?? "";
                    if (n != null) {
                        var t = FormatDate(GetLocalDate(tm, tz));
                        session.Date = t;
                    }

                    // Year
                    session.Year = extractor.EventYear;

                    // Add to collection (events should be unique - collision was caused
                    // by wider-than-required XPath selection in source doc)
                    sessions.Add(session.UniqueId, session);

                } catch (Exception ex) {
                    Console.WriteLine($"PROBLEM: {ex}");
                }
            }
        }

        public void ProcessSession(string str, Session session) {
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


        public static String FormatDate(DateTime date) {
            return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static DateTime GetLocalDate(String zuluDate, String tz) {
            var date = DateTime.ParseExact(zuluDate,
                "yyyy-MM-dd'T'HH:mm:sszzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal);
            
            var zone = ATATimeUtil.TimeUtil.WindowTimeZoneToTimeZoneInfo(tz);

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(date, zone);
            return localTime;        
        }

        public static DateTime ComputeUpdateDate() {
            var referenceDate = DateTime.UtcNow;
            return referenceDate;
        }

    }

}

namespace SessionsFinder
{
    public static class Constants {
        public const string Accept = "Accept=text/html,application/xhtml+xml,application/xml,application/json;q=0.9,*/*;q=0.8";
        public const string AsJSON = "application/json";
    }

    public class ExtractorProgress {
        public int Steps { get; set; }
        public string Message { get; set; }
        public int Step { get; set; }

        public ExtractorProgress() {
        }

        public ExtractorProgress(String message) {
            this.Message = message;
        }
    }

    public interface IExtractor {
        Dictionary<String, Session> GetSessions(IProgress<ExtractorProgress> updater);
        String SerialiseToJson(IProgress<ExtractorProgress> updater, Dictionary<string, Session> sessions);
        String GetId();
        int EventYear { get; } 
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

            // TODO convert to straight .NET
            NSUrl target = NSUrl.FromString(URL);
            NSMutableUrlRequest request = new NSMutableUrlRequest(target);
            request.HttpMethod = "GET";    
            request["Content-Type"] = Type ?? "text/html";

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
