using System.CommandLine;
using System.CommandLine.Binding;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json.Linq;

namespace TwitchVodsRescueCS
{
    [Serializable]
    public class UserException : Exception
    {
        public UserException() { }
        public UserException(string message) : base(message) { }
        public UserException(string message, Exception inner) : base(message, inner) { }
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
        bool downloadThumbnails,
        bool downloadChat,
        bool newestFirst,
        int timeLimit,
        int? maxConcurrentFinalization,
        string[]? collections,
        bool nonCollections,
        bool listCollections,
        bool listDuplicateTitles,
        bool listVideos,
        bool listVideosInMultipleCollections,
        DirectoryInfo? outputDir,
        DirectoryInfo? configDir,
        DirectoryInfo? tempDir,
        string? downloaderCli,
        bool dryRun)
    {
        public bool downloadVideo = downloadVideo;
        public bool downloadThumbnails = downloadThumbnails;
        public bool downloadChat = downloadChat;
        public bool newestFirst = newestFirst;
        public int timeLimit = timeLimit;
        public int maxConcurrentFinalization = Math.Max(1, maxConcurrentFinalization ?? 4);
        public string[]? collections = collections!.Length == 0 ? null : collections;
        public bool nonCollections = nonCollections;
        public bool listCollections = listCollections;
        public bool listDuplicateTitles = listDuplicateTitles;
        public bool listVideos = listVideos;
        public bool listVideosInMultipleCollections = listVideosInMultipleCollections;
        public DirectoryInfo outputDir = outputDir ?? new DirectoryInfo("downloads");
        public DirectoryInfo configDir = configDir ?? new DirectoryInfo("configuration");
        public DirectoryInfo? tempDir = tempDir;
        public string downloaderCli = downloaderCli ?? "TwitchDownloaderCLI";
        public bool dryRun = dryRun;
    }

    public class OptionsBinder(
        Option<bool> downloadVideoOpt,
        Option<bool> downloadThumbnailsOpt,
        Option<bool> downloadChatOpt,
        Option<bool> newestFirstOpt,
        Option<int> timeLimitOpt,
        Option<int?> maxConcurrentFinalizationOpt,
        Option<string[]> collectionsOpt,
        Option<bool> nonCollectionsOpt,
        Option<bool> listCollectionsOpt,
        Option<bool> listDuplicateTitlesOpt,
        Option<bool> listVideosOpt,
        Option<bool> listVideosInMultipleCollectionsOpt,
        Option<DirectoryInfo> outputDirOpt,
        Option<DirectoryInfo> configDirOpt,
        Option<DirectoryInfo?> tempDirOpt,
        Option<string> downloaderCliOpt,
        Option<bool> dryRunOpt) : BinderBase<Options>
    {
        private readonly Option<bool> downloadVideoOpt = downloadVideoOpt;
        private readonly Option<bool> downloadThumbnailsOpt = downloadThumbnailsOpt;
        private readonly Option<bool> downloadChatOpt = downloadChatOpt;
        private readonly Option<bool> newestFirstOpt = newestFirstOpt;
        private readonly Option<int> timeLimitOpt = timeLimitOpt;
        private readonly Option<int?> maxConcurrentFinalizationOpt = maxConcurrentFinalizationOpt;
        private readonly Option<string[]> collectionsOpt = collectionsOpt;
        private readonly Option<bool> nonCollectionsOpt = nonCollectionsOpt;
        private readonly Option<bool> listCollectionsOpt = listCollectionsOpt;
        private readonly Option<bool> listDuplicateTitlesOpt = listDuplicateTitlesOpt;
        private readonly Option<bool> listVideosOpt = listVideosOpt;
        private readonly Option<bool> listVideosInMultipleCollectionsOpt = listVideosInMultipleCollectionsOpt;
        private readonly Option<DirectoryInfo> outputDirOpt = outputDirOpt;
        private readonly Option<DirectoryInfo> configDirOpt = configDirOpt;
        private readonly Option<DirectoryInfo?> tempDirOpt = tempDirOpt;
        private readonly Option<string> downloaderCliOpt = downloaderCliOpt;
        private readonly Option<bool> dryRunOpt = dryRunOpt;

        protected override Options GetBoundValue(BindingContext bindingContext)
        {
            return new Options(
                bindingContext.ParseResult.GetValueForOption(downloadVideoOpt),
                bindingContext.ParseResult.GetValueForOption(downloadThumbnailsOpt),
                bindingContext.ParseResult.GetValueForOption(downloadChatOpt),
                bindingContext.ParseResult.GetValueForOption(newestFirstOpt),
                bindingContext.ParseResult.GetValueForOption(timeLimitOpt),
                bindingContext.ParseResult.GetValueForOption(maxConcurrentFinalizationOpt),
                bindingContext.ParseResult.GetValueForOption(collectionsOpt),
                bindingContext.ParseResult.GetValueForOption(nonCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listDuplicateTitlesOpt),
                bindingContext.ParseResult.GetValueForOption(listVideosOpt),
                bindingContext.ParseResult.GetValueForOption(listVideosInMultipleCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(outputDirOpt),
                bindingContext.ParseResult.GetValueForOption(configDirOpt),
                bindingContext.ParseResult.GetValueForOption(tempDirOpt),
                bindingContext.ParseResult.GetValueForOption(downloaderCliOpt),
                bindingContext.ParseResult.GetValueForOption(dryRunOpt));
        }
    }

    public class Program
    {
        static int exitCode = 0;

        public static async Task<int> Main(string[] args)
        {
            RootCommand root = new("Download twitch vods. Press Q to request it to stop "
                + "after finishing currently running processes.");

            var downloadVideoOpt = new Option<bool>("--download-video",
                "Download the highest quality video and audio available.");
            var downloadThumbnailsOpt = new Option<bool>("--download-thumbnails",
                "Download all thumbnails for each video. There can be multiple "
                + "thumbnails, and I am unsure how it choses which is the main one. Also "
                + "they'll be downloaded in 1920x1080, because the resulting images look "
                + "pretty alright. Even though custom thumbnails are limited to "
                + "1280x720. Idk.");
            var downloadChatOpt = new Option<bool>("--download-chat",
                "Download chat history into a json file. The TwitchDownloader CLI and "
                + "GUI can render a video from this, which is a separate video from the "
                + "main one, so that in particular is only useful for you locally.");
            var newestFirstOpt = new Option<bool>("--newest-first",
                "By default videos will be processed/downloaded from oldest to newest. "
                + "When --newest-first is set the order is reversed.");
            var timeLimitOpt = new Option<int>("--time-limit",
                "Automatically stop once more this amount of minutes have passed. "
                + "Zero or less means no limit. Do note however that when closing "
                + "or killing the process while it is running will most likely result in "
                + "unfinished downloads in the output directory. Make sure to delete "
                + "those files.");
            var maxConcurrentFinalizationOpt = new Option<int?>("--max-concurrent-finalization",
                "The TwitchDownloaderCLI first downloads videos by downloading many "
                + "many 10 second snippets, then it uses ffmpeg to concatenate them into "
                + "the final video. It calls this finalizing. This process can take a "
                + "few minutes and if everything was sequential then nothing would be "
                + "downloaded in that time. This wrapper tool can detect this and "
                + "initiate another download while the previous one is still finalizing. "
                + "But if the machine is slow, for example if the drive cannot keep up, "
                + "it may end up stacking more and more concurrent finalization "
                + "processes. This option puts a limit on concurrent finalization "
                + "processes to prevent this, however allowing some concurrency can "
                + "overall be faster so long as the drive is not the bottle neck and the "
                + "CPU has free cores."
                + "Default: 4.");
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
            var listVideosInMultipleCollectionsOpt = new Option<bool>("--list-videos-in-multiple-collections",
                "Lists vods which are part of more than 1 collection.");
            var outputDirOpt = new Option<DirectoryInfo>("--output-dir",
                "The directory to save downloaded files to. Use forward slashes. "
                + "Default: 'downloads'.");
            var configDirOpt = new Option<DirectoryInfo>("--config-dir",
                "The directory containing one vods csv file, and "
                + "optionally a 'collections' folder. "
                + "Default: 'configuration'.");
            var tempDirOpt = new Option<DirectoryInfo?>("--temp-dir",
                "The temp directory the TwitchDownloaderCLI uses.");
            var downloaderCliOpt = new Option<string>("--downloader-cli",
                "Path of the TwitchDownloaderCLI executable. "
                + "Default: 'TwitchDownloaderCLI'.");
            var dryRunOpt = new Option<bool>("--dry-run",
                "Tries to give an idea of what the current command would do, without "
                + "actually downloading anything or writing any files.");

            root.AddOption(downloadVideoOpt);
            root.AddOption(downloadThumbnailsOpt);
            root.AddOption(downloadChatOpt);
            root.AddOption(newestFirstOpt);
            root.AddOption(timeLimitOpt);
            root.AddOption(maxConcurrentFinalizationOpt);
            root.AddOption(collectionsOpt);
            root.AddOption(nonCollectionsOpt);
            root.AddOption(listCollectionsOpt);
            root.AddOption(listDuplicateTitlesOpt);
            root.AddOption(listVideosOpt);
            root.AddOption(listVideosInMultipleCollectionsOpt);
            root.AddOption(outputDirOpt);
            root.AddOption(configDirOpt);
            root.AddOption(tempDirOpt);
            root.AddOption(downloaderCliOpt);
            root.AddOption(dryRunOpt);

            root.SetHandler(RunMain, new OptionsBinder(
                downloadVideoOpt,
                downloadThumbnailsOpt,
                downloadChatOpt,
                newestFirstOpt,
                timeLimitOpt,
                maxConcurrentFinalizationOpt,
                collectionsOpt,
                nonCollectionsOpt,
                listCollectionsOpt,
                listDuplicateTitlesOpt,
                listVideosOpt,
                listVideosInMultipleCollectionsOpt,
                outputDirOpt,
                configDirOpt,
                tempDirOpt,
                downloaderCliOpt,
                dryRunOpt));

            int libExitCode = await root.InvokeAsync(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        private static string CleanUpRandomWhiteSpaceForTitle(string title)
        {
            // Twitch is being very weird with random extra spaces.
            return Regex.Replace(title.Trim(), "  +", " ");
        }

        public class Detail
        {
            [Name("URL")]
            public string URL { get; set; } = null!;
            [Name("title")]
            public string Title { get; set; } = null!;
            [Name("type")]
            public string Type { get; set; } = null!;
            [Name("viewCount")]
            public int ViewCount { get; set; } = 0;
            [Name("duration")]
            public string Duration { get; set; } = null!;
            [Name("createdAt")]
            public string CreatedAt { get; set; } = null!;

            public int seconds;
            public DateTime createdAtDate;
            public List<CollectionEntry> collectionEntries = [];
            public JObject? additionalMetadata;
            public string[] thumbnailURLs = [];

            public async Task GetAdditionalMetadataFromTwitch()
            {
                if (additionalMetadata != null)
                    return;
                additionalMetadata = (JObject)JObject.Parse(await TwitchHelper.GetVideoInfo(GetId()))["data"]!["video"]!;
                GetThumbnailURLsFromJson(additionalMetadata);
            }

            public async Task<bool> ReadThumbnailsFromMetadataFile()
            {
                string contents = File.ReadAllText(Path.Combine(GetOutputPath(this, null), GetMetadataFilename(this, null)));
                JObject obj = JObject.Parse(contents);
                bool fetchFromTwitch = obj["thumbnailURLs"] == null;
                if (fetchFromTwitch)
                {
                    if (options.dryRun)
                    {
                        Console.WriteLine($"Fetch thumbnail urls for: {Title}");
                        return false;
                    }
                    await GetAdditionalMetadataFromTwitch();
                }
                else
                    GetThumbnailURLsFromJson(obj);
                return fetchFromTwitch;
            }

            private void GetThumbnailURLsFromJson(JObject obj)
            {
                thumbnailURLs = obj["thumbnailURLs"]?.Select(t => (string)t!).ToArray() ?? [];
            }

            public void Initialize()
            {
                Title = CleanUpRandomWhiteSpaceForTitle(Title);
                Match match = Regex.Match(Duration, @"(?:(?:(\d+)h)?(\d+)m)?(\d+)s");
                int h = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                int m = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;;
                int s = int.Parse(match.Groups[3].Value);
                seconds = h * 60 * 60 + m * 60 + s;
                createdAtDate = DateTime.Parse(CreatedAt);
            }

            public int GetId()
            {
                return int.Parse(Regex.Match(URL, @"/(\d+)$").Groups[1].Value);
            }
        }

        public class Collection(string collectionTitle)
        {
            public string collectionTitle = collectionTitle;
            public List<CollectionEntry> entries = [];
        }

        public class CollectionEntry
        {
            public Collection collection;
            /// <summary>
            /// <para>One based.</para>
            /// </summary>
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
                detail = null!;
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

        private static void ReadConfigurationCSV()
        {
            FileInfo csvFile = FindCSVFile(options.configDir)
                ?? throw new UserException($"Missing csv file in: {options.configDir}");
            using var reader = new StreamReader(csvFile.FullName);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            details = csv.GetRecords<Detail>().Where(d => d.Type == "highlight" || d.Type == "upload").ToList();
            foreach (Detail detail in details)
                detail.Initialize();
        }

        private static Collection ReadCollectionFile(FileInfo collectionFile)
        {
            Collection collection = new(Path.GetFileNameWithoutExtension(collectionFile.Name));
            string content = File.ReadAllText(collectionFile.FullName);
            var parser = new VeryStupidParser(content);
            int index = 1; // One based.
            while (!parser.EndOfFile)
            {
                parser.ReadLine(discard: true);
                parser.ReadLine(discard: true);
                string title = CleanUpRandomWhiteSpaceForTitle(parser.ReadLine());
                string date = parser.ReadLine();
                string length = parser.ReadLine();
                collection.entries.Add(new(collection, index++, title, date, length));
                parser.ReadLine(discard: true);
                parser.ReadLine(discard: true);
            }
            return collection;
        }

        private static void ReadConfigUrationCollections()
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

        private static Options options = null!;
        private static List<Detail> details = null!;
        private static List<Collection> collections = [];
        private static readonly Stopwatch mainTimer = new();
        private static readonly EventWaitHandle downloadFinishedHandle = new(false, EventResetMode.AutoReset);
        private static readonly EventWaitHandle clearHandle = new(false, EventResetMode.AutoReset);
        private static int concurrentFinalizationTasks = 0;
        private static bool requestedToQuit = false;

        private static async Task RunMain(Options options)
        {
            Program.options = options;
            try
            {
                await Run();
            }
            catch (UserException e)
            {
                WriteLineToStdErr(e.Message);
                exitCode = 1;
            }
        }

        private static async Task Run()
        {
            mainTimer.Start();
            if (!options.configDir.Exists)
                throw new UserException($"No such configuration folder: {options.configDir}");
            ReadConfigurationCSV();
            ReadConfigUrationCollections();
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
                ListVideos();
                return;
            }
            if (options.listVideosInMultipleCollections)
            {
                ListVideosInMultipleCollections();
                return;
            }

            await ProcessAllDownloadsMain();
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

        private static void ListVideos()
        {
            if (options.nonCollections || options.collections == null)
            {
                Console.WriteLine("Videos not in any collections:");
                var list = details.Where(d => d.collectionEntries.Count == 0);
                foreach (Detail detail in options.newestFirst ? list : list.Reverse())
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
                foreach (CollectionEntry entry in options.newestFirst ? collection.entries.AsEnumerable().Reverse() : collection.entries)
                    Console.WriteLine($"  {entry.index,3}  {entry.detail.CreatedAt}  {entry.title}");
            }
        }

        private static void ListVideosInMultipleCollections()
        {
            var list = details.Where(d => d.collectionEntries.Skip(1).Any());
            foreach (Detail detail in options.newestFirst ? list : list.Reverse())
            {
                Console.WriteLine($"{detail.CreatedAt}  {detail.Title}");
                foreach (CollectionEntry entry in detail.collectionEntries)
                    Console.WriteLine($"  (#{entry.index,3})  {entry.collection.collectionTitle}");
            }
        }

        private static string GetOutputPath(Detail detail, CollectionEntry? entry)
        {
            return detail.collectionEntries.Count != 0
                ? Path.Combine(options.outputDir.FullName, (entry ?? detail.collectionEntries.First()).collection.collectionTitle)
                : options.outputDir.FullName;
        }

        private static int GetCollectionIndex(Detail detail, CollectionEntry? entry)
        {
            if (detail.collectionEntries.Count == 0)
                return -1;
            if (entry != null)
                return entry.index;
            return detail.collectionEntries.First().index;
        }

        private static string AddCollectionIndexPrefix(Detail detail, CollectionEntry? entry, string filename)
        {
            if (detail.collectionEntries.Count == 0)
                return filename;
            return $"{GetCollectionIndex(detail, entry):000}  {filename}";
        }

        private static string GetCollectionPrefixForPrinting(Detail detail, CollectionEntry? entry)
        {
            string? title = entry?.collection.collectionTitle
                ?? detail.collectionEntries.FirstOrDefault()?.collection.collectionTitle
                ?? null;
            return title == null ? "" : $"{title}/";
        }

        private static string FormatDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd HH-mm-ss");
        }

        private static bool IsExternal(Detail detail, CollectionEntry? entry)
        {
            return entry != null && detail.collectionEntries.First() != entry;
        }

        private static string GetMetadataFilename(Detail detail, CollectionEntry? entry)
        {
            string name = $"{FormatDate(detail.createdAtDate)}  metadata{(IsExternal(detail, entry) ? " (external)" : "")}.json";
            return AddCollectionIndexPrefix(detail, entry, name);
        }

        private static string GetChatFilename(Detail detail, CollectionEntry? entry)
        {
            string name = $"{FormatDate(detail.createdAtDate)}  chat.json";
            return AddCollectionIndexPrefix(detail, entry, name);
        }

        private static string GetThumbnailFilename(Detail detail, CollectionEntry? entry, string thumbnailUrl)
        {
            string name = $"{FormatDate(detail.createdAtDate)}  thumb {Regex.Match(thumbnailUrl, @"/([^/]+)$").Groups[1].Value}";
            return AddCollectionIndexPrefix(detail, entry, name);
        }

        private static string GetVideoFilename(Detail detail, CollectionEntry? entry)
        {
            string name = $"{FormatDate(detail.createdAtDate)}  {Regex.Replace(detail.Title, @"[\\/:*?""'<>|]", "")}.mp4";
            return AddCollectionIndexPrefix(detail, entry, name);
        }

        private static async Task<string> GetMetadataFileContents(Detail detail, CollectionEntry? entry)
        {
            await detail.GetAdditionalMetadataFromTwitch();
            JObject metadata = new()
            {
                {"title", detail.Title},
                {"broadcastType", detail.Type},
                // No viewable unfortunately
                {"views", detail.ViewCount},
                {"seconds", detail.seconds},
                {"createdAt", detail.additionalMetadata!["createdAt"]},
                {"url", detail.URL},
                {"id", detail.GetId()},
                {"collectionIndex", GetCollectionIndex(detail, entry)},
                {"collectionTitle", (entry ?? detail.collectionEntries.FirstOrDefault())?.collection.collectionTitle ?? ""},
                {"collectionTitleExternal", IsExternal(detail, entry) ? detail.collectionEntries.First().collection.collectionTitle : ""},
                {"thumbnailURLs", detail.additionalMetadata!["thumbnailURLs"]},
                {"description", detail.additionalMetadata!["description"]},
                {"game", detail.additionalMetadata!["game"]},
            };
            return metadata.ToString();
        }

        private static bool ShouldStop()
        {
            return requestedToQuit || (options.timeLimit > 0 && mainTimer.Elapsed.TotalMinutes > options.timeLimit);
        }

        private static void StdInputWatcher()
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Q)
                {
                    requestedToQuit = !requestedToQuit;
                    Console.WriteLine();
                    if (requestedToQuit)
                        Console.WriteLine("Quitting as soon as all currently running processes have finished.");
                    else
                        Console.WriteLine("Undid quit request, continuing normally.");
                }
            }
        }

        private static async Task ProcessAllDownloadsMain()
        {
            _ = Task.Factory.StartNew(StdInputWatcher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            await ProcessAllDownloads();
            while (concurrentFinalizationTasks != 0)
                Thread.Sleep(100);
        }

        private static async Task ProcessAllDownloads()
        {
            if (options.collections != null)
            {
                Dictionary<string, Collection> collectionsByTitle = collections.ToDictionary(c => c.collectionTitle, c => c);
                HashSet<Detail> visited = [];
                foreach (Detail detail in options.collections
                    .SelectMany(ct => {
                        var list = collectionsByTitle[ct].entries.Select(e => e.detail);
                        return options.newestFirst ? list.Reverse() : list;
                    }))
                {
                    if (visited.Contains(detail))
                        continue;
                    visited.Add(detail);
                    await ProcessDownloads(detail);
                    if (ShouldStop())
                        break;
                }
                return;
            }

            foreach (Detail detail in options.newestFirst ? details : details.AsEnumerable().Reverse())
            {
                if (!options.nonCollections || detail.collectionEntries.Count == 0)
                {
                    await ProcessDownloads(detail);
                    if (ShouldStop())
                        break;
                }
            }
        }

        private static async Task ProcessDownloads(Detail detail)
        {
            if (detail.collectionEntries.Count == 0)
            {
                await ProcessSpecificDownloads(detail, null);
                return;
            }
            // Reverse to make it generate all the external metadata files first, downloading the video very last.
            foreach (CollectionEntry entry in detail.collectionEntries.AsEnumerable().Reverse())
                await ProcessSpecificDownloads(detail, entry);
        }

        private static async Task ProcessSpecificDownloads(Detail detail, CollectionEntry? entry)
        {
            string outputPath = GetOutputPath(detail, entry);

            string metadataPath = Path.Combine(outputPath, GetMetadataFilename(detail, entry));
            if (File.Exists(metadataPath))
            {
                if (await detail.ReadThumbnailsFromMetadataFile())
                {
                    Console.WriteLine($"Updating:    {GetCollectionPrefixForPrinting(detail, entry)}{GetMetadataFilename(detail, entry)}");
                    File.WriteAllText(metadataPath, await GetMetadataFileContents(detail, entry));
                }
            }
            else
            {
                Console.WriteLine($"Creating:    {GetCollectionPrefixForPrinting(detail, entry)}{GetMetadataFilename(detail, entry)}");
                if (!options.dryRun)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
                    File.WriteAllText(metadataPath, await GetMetadataFileContents(detail, entry));
                }
            }

            if (IsExternal(detail, entry))
                return;

            string chatPath = Path.Combine(outputPath, GetChatFilename(detail, entry));
            if (options.downloadChat && !File.Exists(chatPath))
                DownloadChat(detail);

            if (options.downloadThumbnails)
                await DownloadThumbnails(detail);

            string videoPath = Path.Combine(outputPath, GetVideoFilename(detail, entry));
            if (options.downloadChat && !File.Exists(videoPath))
                DownloadVideo(detail);
        }

        private static void DownloadChat(Detail detail)
        {
            string filename = GetChatFilename(detail, null);
            Console.WriteLine($"Downloading: {GetCollectionPrefixForPrinting(detail, null)}{filename}");
            if (options.dryRun)
                return;
            ProcessStartInfo startInfo = new(options.downloaderCli)
            {
                UseShellExecute = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("chatdownload");
            startInfo.ArgumentList.Add("--embed-images");
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(detail.GetId().ToString());
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(Path.Combine(GetOutputPath(detail, null), filename));
            if (options.tempDir != null)
            {
                startInfo.ArgumentList.Add("--temp-path");
                startInfo.ArgumentList.Add(options.tempDir.FullName);
            }
            Process process = Process.Start(startInfo)
                ?? throw new UserException("Failed to start TwitchDownloaderCLI process");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new UserException("TwitchDownloaderCLI failed to download chat history.");
        }

        private static async Task DownloadThumbnails(Detail detail)
        {
            foreach (string url in detail.thumbnailURLs)
            {
                string filename = GetThumbnailFilename(detail, null, url);
                if (Path.Exists(Path.Combine(GetOutputPath(detail, null), filename)))
                    continue;
                await DownloadThumbnail(detail, filename, url);
            }
        }

        private static readonly HttpClient httpClient = new();
        private static async Task DownloadThumbnail(Detail detail, string filename, string url)
        {
            Console.WriteLine($"Downloading: {GetCollectionPrefixForPrinting(detail, null)}{filename}");
            if (options.dryRun)
                return;
            await Task.Delay(50); // A little bit of rate limiting.
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed downloading thumbnail: {e.Message}");
                return;
            }
            await Task.Delay(50); // A little bit of rate limiting.
            using Stream stream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = File.OpenWrite(Path.Combine(GetOutputPath(detail, null), filename));
            await stream.CopyToAsync(fileStream);
        }

        private static void DownloadVideo(Detail detail)
        {
            Console.WriteLine($"Downloading: {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
            if (options.dryRun)
                return;
            while (concurrentFinalizationTasks > options.maxConcurrentFinalization)
                Thread.Sleep(100);
            if (ShouldStop()) // Never mind, guess we stop now.
                return;
            Interlocked.Increment(ref concurrentFinalizationTasks);
            Task.Run(() => DownloadVideoTask(detail));
            downloadFinishedHandle.WaitOne();
            clearHandle.Set();
        }

        private static void DownloadVideoTask(Detail detail)
        {
            string outputFilePath = Path.Combine(GetOutputPath(detail, null), GetVideoFilename(detail, null));
            ProcessStartInfo startInfo = new(options.downloaderCli)
            {
                UseShellExecute = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("videodownload");
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(detail.GetId().ToString());
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputFilePath);
            if (options.tempDir != null)
            {
                startInfo.ArgumentList.Add("--temp-path");
                startInfo.ArgumentList.Add(options.tempDir.FullName);
            }
            Process process = Process.Start(startInfo)
                ?? throw new UserException("Failed to start TwitchDownloaderCLI process");

            while (!Path.Exists(outputFilePath))
                Thread.Sleep(10);
            // FileInfo seems to do caching? Not entirely sure but creating a new one each iteration to be safe.
            while (Path.Exists(outputFilePath) && new FileInfo(outputFilePath).Length == 0)
                Thread.Sleep(100);
            Console.WriteLine("Done downloading, finalizing...");
            WaitHandle.SignalAndWait(downloadFinishedHandle, clearHandle);

            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new UserException("TwitchDownloaderCLI failed to download a video.");
            Console.WriteLine(); // TwitchDownloaderCLI does not write a trailing newline to stdout before existing.

            Interlocked.Decrement(ref concurrentFinalizationTasks);
        }
    }

    // Taken from: https://github.com/lay295/TwitchDownloader/blob/192c8bf46ecd7597f220edc95000d1b3105525e0/TwitchDownloaderCore/TwitchHelper.cs#L31-L43
    public static class TwitchHelper
    {
        private static readonly HttpClient httpClient = new();

        public static async Task<string> GetVideoInfo(long videoId)
        {
            await Task.Delay(50); // A little bit of rate limiting.
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{video(id:\\\"" + videoId + "\\\"){thumbnailURLs(height:1080,width:1920),createdAt,game{id,displayName,boxArtURL},description}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            // NOTE: This is using the same client id as the TwitchDownloaderCLI. This is probably the wrong thing to do,
            // however I am not interested in figuring out how twitch's api actually works. Besides me copying this
            // function into the project here is just a shortcut to make myself not have to prase standard output from the
            // TwitchDownloaderCLI. This is just easier and this entire program here is one time use throwaway anyway.
            // cSpell:ignore kimne78kx3ncx6brgo4mv6wki5h1ko
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await Task.Delay(50); // A little bit of rate limiting.
            return await response.Content.ReadAsStringAsync();
        }
    }
}
