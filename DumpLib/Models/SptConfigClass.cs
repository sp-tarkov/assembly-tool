namespace DumpLib.Models
{
    public class SptConfigClass
    {
        /// <summary>
        /// Default: Test
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        ///  Default: [ "Interchange", "factory4_day", "laboratory", "bigmap", "Lighthouse", "RezervBase", "Sandbox", "Shoreline", "TarkovStreets", "Woods" ]
        /// </summary>
        public string[] MapNames { get; set; }
        
        /// <summary>
        /// Default: "yyyy-MM-dd_HH-mm-ss"
        /// </summary>
        public string DateTimeFormat { get; set; }

        public bool QuickDumpEnabled { get; set; }

        public SptReflections SptReflections { get; set; }
        
        public SptTimings SptTimings { get; set; }
    }

    public class SptTimings
    {
        /// <summary>
        /// Default: 10s * 1000ms = 10000ms
        /// </summary>
        public int SingleIterationDelayMs { get; set; }
        
        /// <summary>
        /// Default: 5m * 60s * 1000ms = 300000ms
        /// </summary>
        public int AllIterationDelayMs { get; set; }
    }

    public class SptReflections
    {
        /// <summary>
        /// Default: "get_MainURLFull" as of 128476 client
        /// </summary>
        public string MainUrlPropName { get; set; }
        
        /// <summary>
        /// Default: "Params" as of 128476 client
        /// </summary>
        public string ParamFieldName { get; set; }
        
        /// <summary>
        /// Default: "responseText" as of 128476 client
        /// </summary>
        public string RequestFieldName { get; set; }
    }
}