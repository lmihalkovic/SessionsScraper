using System;
using System.Runtime.Serialization;

using Newtonsoft.Json;

namespace DataModel
{

    public class Event
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Index { get; set; }

        public Event() { }
    }

    [DataContract]
    public class SessionsDesc {
        [JsonProperty(Order = 1)]
        [DataMember(Name="tracks")]
        public string[] Tracks { get; set; } = new string[]{};

        [JsonProperty(Order = 2)]
        [DataMember(Name="updated")]
        public string Updated { get; set; }

        [JsonProperty(Order = 3)]
        [DataMember(Name="sessions")]
        public SessionDesc[] Sessions { get; set; } = new SessionDesc[]{};

    }

    [DataContract(Name="session")]
    public class SessionDesc {
        [DataMember(Name="uniqueId")]
        public string UniqueId { get; set; } = "";

        [DataMember(Name="title")]
        public string Title { get; set; } = "";

        [DataMember(Name="description")]
        public string Description { get; set; } = "";

        [DataMember(Name="year")]
        public int Year { get; set; } = 0;

        [DataMember(Name="date")]
        public String Date { get; set; } = "";

        [DataMember(Name="track")]
        public string Track { get; set; } = "";

        [DataMember(Name="url")]
        public string Url { get; set; } = "";

    }

    public class Session
    {
        public string UniqueId { get; set; }

        public long Id { get; set; } = 0;

        public int Year { get; set; } = 0;

        public string Date { get; set; } = "";

        public string Track  { get; set; } = "";

        public string Focus  { get; set; } = "";

        public string Title  { get; set; } = "";

        public string Summary  { get; set; } = "";

        public string VideoURL  { get; set; } = "";

        public string HdVideoURL  { get; set; } = "";

        public string SlidesURL  { get; set; } = "";

        public string ShelfImageURL  { get; set; } = "";

        public float Progress  { get; set; } = 0.0f;

        public double CurrentPosition  { get; set; } = 0.0;

        public bool Favorite  { get; set; } = false;

//        public Transcript transcript { get; set; }

        //dynamic var slidesPDFData = NSData()

        public bool Downloaded  { get; set; } = false;

        public Session()
        {
        }
    }

    public class Transcript
    {
        public string fullText { get; set; } = "";
    }
}
