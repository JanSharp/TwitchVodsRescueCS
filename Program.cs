using System.CommandLine;
using System.CommandLine.Binding;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace TwitchVodsRescueCS
{
    [Serializable]
    public class UserException : Exception
    {
        public UserException() { }
        public UserException(string message) : base(message) { }
        public UserException(string message, System.Exception inner) : base(message, inner) { }
        protected UserException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public class VeryStupidParser
    {
        private readonly string content;
        private int index = 0;

        public bool EndOfFile => index >= content.Length;

        public VeryStupidParser(string content)
        {
            this.content = content;
            SkipBlank();
        }

        private void SkipBlank()
        {
            while (index < content.Length && (content[index] == '\n' || content[index] == '\r'))
                index++;
        }

        public string ReadLine(bool discard = false)
        {
            int startIndex = index;
            while (index < content.Length && content[index] != '\n' && content[index] != '\r')
                index++;
            string result = discard ? null! : content[startIndex..index];
            SkipBlank();
            return result;
        }
    }

    public class Options(
        bool downloadVideo,
        bool downloadChat,
        int timeLimit,
        string[]? collections,
        bool nonCollections,
        bool listCollections,
        bool listDuplicateTitles,
        bool listVideos,
        DirectoryInfo outputDir,
        DirectoryInfo configDir,
        DirectoryInfo? tempDir,
        bool dryRun)
    {
        public bool downloadVideo = downloadVideo;
        public bool downloadChat = downloadChat;
        public int timeLimit = timeLimit;
        public string[]? collections = collections!.Length == 0 ? null : collections;
        public bool nonCollections = nonCollections;
        public bool listCollections = listCollections;
        public bool listDuplicateTitles = listDuplicateTitles;
        public bool listVideos = listVideos;
        public DirectoryInfo outputDir = outputDir ?? new DirectoryInfo("downloads");
        public DirectoryInfo configDir = configDir ?? new DirectoryInfo("configuration");
        public DirectoryInfo? tempDir = tempDir;
        public bool dryRun = dryRun;
    }

    public class OptionsBinder : BinderBase<Options>
    {
        private readonly Option<bool> downloadVideoOpt;
        private readonly Option<bool> downloadChatOpt;
        private readonly Option<int> timeLimitOpt;
        private readonly Option<string[]> collectionsOpt;
        private readonly Option<bool> nonCollectionsOpt;
        private readonly Option<bool> listCollectionsOpt;
        private readonly Option<bool> listDuplicateTitlesOpt;
        private readonly Option<bool> listVideosOpt;
        private readonly Option<DirectoryInfo> outputDirOpt;
        private readonly Option<DirectoryInfo> configDirOpt;
        private readonly Option<DirectoryInfo?> tempDirOpt;
        private readonly Option<bool> dryRunOpt;

        public OptionsBinder(
            Option<bool> downloadVideoOpt,
            Option<bool> downloadChatOpt,
            Option<int> timeLimitOpt,
            Option<string[]> collectionsOpt,
            Option<bool> nonCollectionsOpt,
            Option<bool> listCollectionsOpt,
            Option<bool> listDuplicateTitlesOpt,
            Option<bool> listVideosOpt,
            Option<DirectoryInfo> outputDirOpt,
            Option<DirectoryInfo> configDirOpt,
            Option<DirectoryInfo?> tempDirOpt,
            Option<bool> dryRunOpt)
        {
            this.downloadVideoOpt = downloadVideoOpt;
            this.downloadChatOpt = downloadChatOpt;
            this.timeLimitOpt = timeLimitOpt;
            this.collectionsOpt = collectionsOpt;
            this.nonCollectionsOpt = nonCollectionsOpt;
            this.listCollectionsOpt = listCollectionsOpt;
            this.listDuplicateTitlesOpt = listDuplicateTitlesOpt;
            this.listVideosOpt = listVideosOpt;
            this.outputDirOpt = outputDirOpt;
            this.configDirOpt = configDirOpt;
            this.tempDirOpt = tempDirOpt;
            this.dryRunOpt = dryRunOpt;
        }

        protected override Options GetBoundValue(BindingContext bindingContext)
        {
            return new Options(
                bindingContext.ParseResult.GetValueForOption(downloadVideoOpt),
                bindingContext.ParseResult.GetValueForOption(downloadChatOpt),
                bindingContext.ParseResult.GetValueForOption(timeLimitOpt),
                bindingContext.ParseResult.GetValueForOption(collectionsOpt),
                bindingContext.ParseResult.GetValueForOption(nonCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listDuplicateTitlesOpt),
                bindingContext.ParseResult.GetValueForOption(listVideosOpt),
                bindingContext.ParseResult.GetValueForOption(outputDirOpt)!,
                bindingContext.ParseResult.GetValueForOption(configDirOpt)!,
                bindingContext.ParseResult.GetValueForOption(tempDirOpt),
                bindingContext.ParseResult.GetValueForOption(dryRunOpt));
        }
    }

    public class Program
    {
        static int exitCode = 0;

        public static int Main(string[] args)
        {
            RootCommand root = new("Download twitch vods.");

            var downloadVideoOpt = new Option<bool>("--download-video",
                "Download the highest quality video and audio available.");
            var downloadChatOpt = new Option<bool>("--download-chat",
                "Download chat history into a json file. The TwitchDownloader CLI and "
                + "GUI can render a video from this, which is a separate video from the "
                + "main one, so that in particular is only useful for you locally.");
            var timeLimitOpt = new Option<int>("--time-limit",
                "Automatically stop once more this amount of minutes have passed. "
                + "Zero or less means no limit. Do note however that when closing "
                + "or killing the process while it is running will most likely result in "
                + "unfinished downloads in the output directory. Make sure to delete "
                + "those files.");
            var collectionsOpt = new Option<string[]>("--collections",
                "Only process videos in the given collections.");
            var nonCollectionsOpt = new Option<bool>("--non-collections",
                "Only process videos which are not part of a collection.");
            var listCollectionsOpt = new Option<bool>("--list-collections",
                "List all names of collections.");
            var listDuplicateTitlesOpt = new Option<bool>("--list-duplicate-titles",
                "List vods with duplicate titles.");
            var listVideosOpt = new Option<bool>("--list-videos",
                "Lists vods in order, respects --collections and "
                + "--non-collections as filters.");
            var outputDirOpt = new Option<DirectoryInfo>("--output-dir",
                "The directory to save downloaded files to. Use forward slashes. "
                + "Default: 'downloads'.");
            var configDirOpt = new Option<DirectoryInfo>("--config-dir",
                "The directory containing one vods csv file, and "
                + "optionally a 'collections' folder."
                + "Default: 'configuration'.");
            var tempDirOpt = new Option<DirectoryInfo?>("--temp-dir",
                "The temp directory the TwitchDownloaderCLI uses.");
            var dryRunOpt = new Option<bool>("--dry-run",
                "Tries to give an idea of what the current command would do, without "
                + "actually downloading anything or writing any files.");

            root.AddOption(downloadVideoOpt);
            root.AddOption(downloadChatOpt);
            root.AddOption(timeLimitOpt);
            root.AddOption(collectionsOpt);
            root.AddOption(nonCollectionsOpt);
            root.AddOption(listCollectionsOpt);
            root.AddOption(listDuplicateTitlesOpt);
            root.AddOption(listVideosOpt);
            root.AddOption(outputDirOpt);
            root.AddOption(configDirOpt);
            root.AddOption(tempDirOpt);
            root.AddOption(dryRunOpt);

            root.SetHandler(RunMain, new OptionsBinder(
                downloadVideoOpt,
                downloadChatOpt,
                timeLimitOpt,
                collectionsOpt,
                nonCollectionsOpt,
                listCollectionsOpt,
                listDuplicateTitlesOpt,
                listVideosOpt,
                outputDirOpt,
                configDirOpt,
                tempDirOpt,
                dryRunOpt));

            int libExitCode = root.Invoke(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        public class Detail
        {
            public string URL { get; set; }
            [Name("title")]
            public string Title { get; set; }
            [Name("type")]
            public string Type { get; set; }
            [Name("viewCount")]
            public int ViewCount { get; set; }
            [Name("duration")]
            public string Duration { get; set; }
            [Name("createdAt")]
            public string CreatedAt { get; set; }

            public int seconds;
            public DateTime createdAtDate;
            public List<CollectionEntry> collectionEntries = new();

            public void Initialize()
            {
                Match match = Regex.Match(Duration, @"(?:(\d+)h)?(\d+)m(\d+)s");
                int h = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                int m = int.Parse(match.Groups[2].Value);
                int s = int.Parse(match.Groups[3].Value);
                seconds = h * 60 * 60 + m * 60 + s;
                createdAtDate = DateTime.Parse(CreatedAt);
            }
        }

        public class Collection(string collectionTitle)
        {
            public string collectionTitle = collectionTitle;
            public List<CollectionEntry> entries = new();
        }

        public class CollectionEntry
        {
            public Collection collection;
            public int index;
            public string title;
            public string date;
            public string length;
            public int seconds;
            public Detail detail;

            public CollectionEntry(
                Collection collection,
                int index,
                string title,
                string date,
                string length)
            {
                this.collection = collection;
                this.index = index;
                this.title = title;
                this.date = date;
                this.length = length;
                ParseSeconds();
            }

            public void ParseSeconds()
            {
                Match match = Regex.Match(length, @"(\d+):(\d+)(?::(\d+))?");
                if (match.Groups[3].Value != "")
                {
                    int h = int.Parse(match.Groups[1].Value);
                    int m = int.Parse(match.Groups[2].Value);
                    int s = int.Parse(match.Groups[3].Value);
                    seconds = h * 60 * 60 + m * 60 + s;
                }
                else
                {
                    int m = int.Parse(match.Groups[1].Value);
                    int s = int.Parse(match.Groups[2].Value);
                    seconds = m * 60 + s;
                }
            }
        }

        private static FileInfo? FindCSVFile(DirectoryInfo dir)
        {
            return dir.EnumerateFiles().FirstOrDefault(f => f.Extension == ".csv");
        }

        private static void ReadConfigurationCSV(Options options)
        {
            FileInfo csvFile = FindCSVFile(options.configDir)
                ?? throw new UserException($"Missing csv file in: {options.configDir}");
            using var reader = new StreamReader(csvFile.FullName);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            details = csv.GetRecords<Detail>().ToList();
            foreach (Detail detail in details)
                detail.Initialize();
        }

        private static Collection ReadCollectionFile(FileInfo collectionFile)
        {
            Collection collection = new(Path.GetFileNameWithoutExtension(collectionFile.Name));
            string content = File.ReadAllText(collectionFile.FullName);
            var parser = new VeryStupidParser(content);
            int index = 0;
            while (!parser.EndOfFile)
            {
                parser.ReadLine(discard: true);
                parser.ReadLine(discard: true);
                string title = parser.ReadLine();
                string date = parser.ReadLine();
                string length = parser.ReadLine();
                collection.entries.Add(new(collection, index++, title, date, length));
                parser.ReadLine(discard: true);
                parser.ReadLine(discard: true);
            }
            return collection;
        }

        private static void ReadConfigUrationCollections(Options options)
        {
            var collectionsDir = new DirectoryInfo(Path.Combine(options.configDir.FullName, "collections"));
            if (!collectionsDir.Exists)
                return;
            collections = collectionsDir.EnumerateFiles().Select(f => ReadCollectionFile(f)).ToList();
        }

        private static void WriteLineToStdErr(string msg)
        {
            Console.Error.WriteLine(msg);
            Console.Error.Flush();
        }

        private static bool ResolveCollectionEntryDetailReferencesForCollection(Collection collection)
        {
            bool success = true;
            foreach (var pair in collection.entries.GroupJoin(
                details,
                e => $"{e.seconds} {e.title}",
                d => $"{d.seconds} {d.Title}",
                (entry, details) => (entry, details: details.ToList())))
            {
                if (pair.details.Count == 0)
                {
                    WriteLineToStdErr($"The collection entry '{pair.entry.title}' in the collection "
                        + $"'{collection.collectionTitle}' has no matching video in the videos csv file.");
                    success = false;
                    continue;
                }
                if (pair.details.Count > 1)
                {
                    WriteLineToStdErr($"The collection entry '{pair.entry.title}' in the collection "
                        + $"'{collection.collectionTitle}' has multiple matching videos in the videos csv file?!?!?!?!?");
                    success = false;
                    continue;
                }
                pair.entry.detail = pair.details.Single();
                pair.details.Single().collectionEntries.Add(pair.entry);
            }
            return success;
        }

        private static bool ResolveCollectionEntryDetailReferences()
        {
            bool success = true;
            foreach (Collection collection in collections)
                success &= ResolveCollectionEntryDetailReferencesForCollection(collection);
            return success;
        }

        private static List<Detail> details;
        private static List<Collection> collections = new();

        private static void RunMain(Options options)
        {
            try
            {
                Run(options);
            }
            catch (UserException e)
            {
                WriteLineToStdErr(e.Message);
                exitCode = 1;
            }
        }

        private static void Run(Options options)
        {
            if (!options.configDir.Exists)
                throw new UserException($"No such configuration folder: {options.configDir}");
            ReadConfigurationCSV(options);
            ReadConfigUrationCollections(options);
            if (!ResolveCollectionEntryDetailReferences())
            {
                exitCode = 1;
                return;
            }

            if (options.listCollections)
            {
                ListCollections();
                return;
            }
            if (options.listDuplicateTitles)
            {
                ListDuplicateTitles();
                return;
            }
            if (options.listVideos)
            {
                ListVideos(options);
                return;
            }

            var foo = details;
            var bar = collections;
            Console.WriteLine("done!");
        }

        private static void ListCollections()
        {
            foreach (Collection collection in collections)
                Console.WriteLine(collection.collectionTitle);
        }

        private static void ListDuplicateTitles()
        {
            var groups = details.GroupBy(d => d.Title).Where(g => g.Count() > 1);
            foreach (var group in groups)
                foreach (Detail detail in group)
                    Console.WriteLine($"{group.Count()}  {detail.CreatedAt}  {detail.URL}  {detail.Title}");
            Console.WriteLine($"unique duplicate title count: {groups.Count()}");
        }

        private static void ListVideos(Options options)
        {
            if (options.nonCollections || options.collections == null)
            {
                Console.WriteLine("Videos not in any collections:");
                foreach (Detail detail in details.Where(d => d.collectionEntries.Count == 0))
                    Console.WriteLine($"  {detail.CreatedAt}  {detail.Title}");
            }
            if (options.nonCollections)
                return;
            HashSet<string>? collectionsLut = options.collections?.ToHashSet();
            foreach (Collection collection in collections)
            {
                if ((!collectionsLut?.Contains(collection.collectionTitle)) ?? false)
                    continue;
                Console.WriteLine($"{collection.collectionTitle}:");
                foreach (CollectionEntry entry in collection.entries)
                    Console.WriteLine($"  {entry.index,3}  {entry.detail.CreatedAt}  {entry.title}");
            }
        }
    }
}
