using System;
using System.Collections.Generic;
using System.Text;

namespace StringLocalizationGenerator;

//internal class StringLocalizationJson
//{
//    public GeneratorOption Generator { get; set; } = new GeneratorOption();
//    public StringsOption Strings { get; set; } = new StringsOption();

//    public class GeneratorOption
//    {

//        public string OutputNamespace { get; set; } = string.Empty;
//        public string OutputClass { get; set; } = string.Empty;
//        public DefaultModeType NotExistsDefaultMode { get; set; } = DefaultModeType.Exception;
//        public string NotExistsDefaultString { get; set; } = "[NotSet]";
//        public enum DefaultModeType
//        {
//            Exception,
//            String,
//        }
//    }

//    public class StringsOption
//    {
//        [JsonExtensionData]
//        public IDictionary<string, LanguageOption> IdDict { get; set; } = new Dictionary<string, LanguageOption>();

//        public class LanguageOption
//        {
//            [JsonExtensionData]
//            public IDictionary<string, string> LanguageDict { get; set; } = new Dictionary<string, string>();
//        }
//    }
//}
