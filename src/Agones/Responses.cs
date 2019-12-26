using System;
using System.Collections.Generic;
using System.Text;

namespace Agones
{
    public class NullResponse { }

    public class GameServerResponse
    {
        public Object_Meta object_meta { get; set; }
        public Status status { get; set; }

        public class Object_Meta
        {
            public string name { get; set; }
            public string _namespace { get; set; }
            public string uid { get; set; }
            public string resource_version { get; set; }
            public string generation { get; set; }
            public string creation_timestamp { get; set; }
            public Annotations annotations { get; set; }
            public Labels labels { get; set; }
        }

        public class Annotations
        {
            public string annotation { get; set; }
        }

        public class Labels
        {
            public string islocal { get; set; }
        }

        public class Status
        {
            public string state { get; set; }
            public string address { get; set; }
            public Port[] ports { get; set; }
        }

        public class Port
        {
            public string name { get; set; }
            public int port { get; set; }
        }
    }
}
