using System.CommandLine;
using System.CommandLine.Binding;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json.Linq;
using System.Linq;

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

        public void SkipPastMatch(Regex regex)
        {
            Match match = regex.Match(content, index);
            if (!match.Success)
                return;
            index = match.Index + match.Length;
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
        bool validateVideos,
        int? maxConcurrentFinalization,
        string[]? collections,
        bool nonCollections,
        bool listCollections,
        bool listDuplicateTitles,
        bool listVideos,
        bool listVideosInMultipleCollections,
        bool listYoutubeVsTwitch,
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
        public bool validateVideos = validateVideos;
        public int maxConcurrentFinalization = Math.Max(1, maxConcurrentFinalization ?? 4);
        public string[]? collections = collections!.Length == 0 ? null : collections;
        public bool nonCollections = nonCollections;
        public bool listCollections = listCollections;
        public bool listDuplicateTitles = listDuplicateTitles;
        public bool listVideos = listVideos;
        public bool listVideosInMultipleCollections = listVideosInMultipleCollections;
        public bool listYoutubeVsTwitch = listYoutubeVsTwitch;
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
        Option<bool> validateVideosOpt,
        Option<int?> maxConcurrentFinalizationOpt,
        Option<string[]> collectionsOpt,
        Option<bool> nonCollectionsOpt,
        Option<bool> listCollectionsOpt,
        Option<bool> listDuplicateTitlesOpt,
        Option<bool> listVideosOpt,
        Option<bool> listVideosInMultipleCollectionsOpt,
        Option<bool> listYoutubeVsTwitchOpt,
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
        private readonly Option<bool> validateVideosOpt = validateVideosOpt;
        private readonly Option<int?> maxConcurrentFinalizationOpt = maxConcurrentFinalizationOpt;
        private readonly Option<string[]> collectionsOpt = collectionsOpt;
        private readonly Option<bool> nonCollectionsOpt = nonCollectionsOpt;
        private readonly Option<bool> listCollectionsOpt = listCollectionsOpt;
        private readonly Option<bool> listDuplicateTitlesOpt = listDuplicateTitlesOpt;
        private readonly Option<bool> listVideosOpt = listVideosOpt;
        private readonly Option<bool> listVideosInMultipleCollectionsOpt = listVideosInMultipleCollectionsOpt;
        private readonly Option<bool> listYoutubeVsTwitchOpt = listYoutubeVsTwitchOpt;
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
                bindingContext.ParseResult.GetValueForOption(validateVideosOpt),
                bindingContext.ParseResult.GetValueForOption(maxConcurrentFinalizationOpt),
                bindingContext.ParseResult.GetValueForOption(collectionsOpt),
                bindingContext.ParseResult.GetValueForOption(nonCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listDuplicateTitlesOpt),
                bindingContext.ParseResult.GetValueForOption(listVideosOpt),
                bindingContext.ParseResult.GetValueForOption(listVideosInMultipleCollectionsOpt),
                bindingContext.ParseResult.GetValueForOption(listYoutubeVsTwitchOpt),
                bindingContext.ParseResult.GetValueForOption(outputDirOpt),
                bindingContext.ParseResult.GetValueForOption(configDirOpt),
                bindingContext.ParseResult.GetValueForOption(tempDirOpt),
                bindingContext.ParseResult.GetValueForOption(downloaderCliOpt),
                bindingContext.ParseResult.GetValueForOption(dryRunOpt));
        }
    }

    public partial class Program
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
            var validateVideosOpt = new Option<bool>("--validate-videos",
                "Validates all downloaded videos to make sure they actually fully "
                + "downloaded. Requires the ffprobe tool. On linux install it through "
                + "your package manager, it maybe probably already comes with ffmpeg, on "
                + "windows either somehow install it system wide or go to "
                + "https://ffbinaries.com/downloads, download latest ffprobe, extract it "
                + "and put it in the same folder as this executable. Ignores the options "
                + "--collections and --non-collections.");
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
            var listYoutubeVsTwitchOpt = new Option<bool>("--list-youtube-vs-twitch",
                "Lists the symmetric difference between videos on youtube and twitch. "
                + "Reads text files from a 'youtube' folder from the configuration folder "
                + "where each file is a control A C of a page from youtube's video "
                + "manager.");
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
            root.AddOption(validateVideosOpt);
            root.AddOption(maxConcurrentFinalizationOpt);
            root.AddOption(collectionsOpt);
            root.AddOption(nonCollectionsOpt);
            root.AddOption(listCollectionsOpt);
            root.AddOption(listDuplicateTitlesOpt);
            root.AddOption(listVideosOpt);
            root.AddOption(listVideosInMultipleCollectionsOpt);
            root.AddOption(listYoutubeVsTwitchOpt);
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
                validateVideosOpt,
                maxConcurrentFinalizationOpt,
                collectionsOpt,
                nonCollectionsOpt,
                listCollectionsOpt,
                listDuplicateTitlesOpt,
                listVideosOpt,
                listVideosInMultipleCollectionsOpt,
                listYoutubeVsTwitchOpt,
                outputDirOpt,
                configDirOpt,
                tempDirOpt,
                downloaderCliOpt,
                dryRunOpt));

            {
                Command splitCommand = new("split",
                    "Extract parts of videos without re-encoding them. Output files are "
                    + "placed next to the input file with the time frame which was specified "
                    + "for --parts as a postfix. Uses both ffprobe and ffmpeg.");

                var fileOpt = new Option<FileInfo>("--file",
                    "Path to file to split.");
                var partsOpt = new Option<string[]>("--parts",
                    "Format: [hh:]mm:ss[.mmm]-[hh:]mm:ss[.mmm]\n"
                    + "Example: 0:00-1:32:05.400 to create a video starting at the very beginning "
                    + "and stopping a bit after one and a half hours in.\n"
                    + "Leading zeros are optional, even 10:5 would be valid, though 10:05 is "
                    + "more readable.\n"
                    + "Can specify multiple --parts in one command.\n"
                    + "May not actually start at the exact time specified as it has to find "
                    + "the nearest key frame before the given start time.\n"
                    + "To specify the end of the video, setting it to past the end works, "
                    + "though for readability of the output files only adding like 1 second "
                    + "to the video's length is appropriate.");
                var splitDryRunOpt = new Option<bool>("--dry-run",
                    "Tries to give an idea of what the current command would do, without "
                    + "actually creating any files.");

                splitCommand.AddOption(fileOpt);
                splitCommand.AddOption(partsOpt);
                splitCommand.AddOption(splitDryRunOpt);

                splitCommand.SetHandler(SplitMain,
                    fileOpt,
                    partsOpt,
                    splitDryRunOpt);
                root.AddCommand(splitCommand);
            }

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
            public YoutubeVideoEntry? associatedYoutubeVideo;
            public JObject? additionalMetadata;
            public string[] thumbnailURLs = [];

            public async Task GetAdditionalMetadataFromTwitch()
            {
                if (additionalMetadata != null)
                    return;
                additionalMetadata = (JObject)JObject.Parse(await TwitchHelper.GetVideoInfo(GetId()))["data"]!["video"]!;
                GetThumbnailURLsFromJson(additionalMetadata);
            }

            public async Task<bool> ReadThumbnailsFromMetadataFile(CollectionEntry? entry)
            {
                string contents = File.ReadAllText(Path.Combine(GetOutputPath(this, entry), GetMetadataFilename(this, entry)));
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

            public long GetId()
            {
                return long.Parse(Regex.Match(URL, @"/(\d+)$").Groups[1].Value);
            }
        }

        public class Collection(string collectionTitle)
        {
            public string collectionTitle = collectionTitle;
            public List<CollectionEntry> entries = [];
        }

        public static int ParseSeconds(string duration)
        {
            Match match = Regex.Match(duration, @"(?:(?:(\d+):)?(\d+):)?(\d+)");
            int h = match.Groups[1].Value == "" ? 0 : int.Parse(match.Groups[1].Value);
            int m = match.Groups[2].Value == "" ? 0 : int.Parse(match.Groups[2].Value);
            int s = match.Groups[3].Value == "" ? 0 : int.Parse(match.Groups[3].Value);
            return h * 60 * 60 + m * 60 + s;
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
                seconds = ParseSeconds(length);
            }
        }

        public class YoutubeVideoEntry(
            string duration,
            string title,
            string description,
            string visibility,
            string restrictions,
            string uploadDate,
            string videoType,
            string views,
            string commentCount,
            string likeDislikeRatio,
            string likes)
        {
            public string duration = duration;
            public string title = title;
            public string description = description;
            public string visibility = visibility;
            public string restrictions = restrictions;
            public string uploadDate = uploadDate;
            public string videoType = videoType;
            public string views = views;
            public string commentCount = commentCount;
            public string likeDislikeRatio = likeDislikeRatio;
            public string likes = likes;
            public int seconds = ParseSeconds(duration); // This is some C# syntax sugar magic.

            public Detail? associatedDetail;
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
            parser.SkipPastMatch(new Regex(@"of 100 videos added to collection\s*"));
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
            collections = collectionsDir.EnumerateFiles().Select(f => ReadCollectionFile(f)).OrderBy(c => c.collectionTitle.ToLower()).ToList();
        }

        private static void ReadYoutubePage(FileInfo pageFile)
        {
            int initialCount = youtubeVideos.Count;
            string content = File.ReadAllText(pageFile.FullName);
            youtubeVideos.AddRange(YoutubeVideoEntryRegex().Matches(content)
                .Cast<Match>()
                .Select(match => new YoutubeVideoEntry(
                    match.Groups["duration"].Value,
                    match.Groups["title"].Value,
                    match.Groups["description"].Value,
                    match.Groups["visibility"].Value,
                    match.Groups["restrictions"].Value,
                    match.Groups["uploadDate"].Value,
                    match.Groups["videoType"].Value,
                    match.Groups["views"].Value,
                    match.Groups["commentCount"].Value,
                    match.Groups["likeDislikeRatio"].Value,
                    match.Groups["likes"].Value)));
            int finalCount = youtubeVideos.Count;
            if ((finalCount - initialCount) != 50)
                Console.WriteLine($"Only read {finalCount - initialCount} from {pageFile.Name}");
        }

        [GeneratedRegex(@"
            Video\ thumbnail:\ ?[^\r\n]*[\r\n]+
            (?<duration>[^\r\n]*)[\r\n]+
            (?<title>[^\r\n]*)[\r\n]+
            (?<description>[^\r\n]*)[\r\n]+
            (?<visibility>[^\r\n]*)[\r\n]+
            (?<restrictions>[^\r\n]*)[\r\n]+
            (?<uploadDate>[^\r\n]*)[\r\n]+
            (?<videoType>[^\r\n]*)[\r\n]+
            (?<views>[^\r\n]*)[\r\n]+
            (?<commentCount>[^\r\n]*)[\r\n]+
            (?:
                (?=\d)
                (?<likeDislikeRatio>[^\r\n]*)[\r\n]+
                (?<likes>[^\r\n]*)
            )?
            ", RegexOptions.IgnorePatternWhitespace)]
        private static partial Regex YoutubeVideoEntryRegex();

        private static void ReadYoutubePages()
        {
            var youtubeDir = new DirectoryInfo(Path.Combine(options.configDir.FullName, "youtube"));
            if (!youtubeDir.Exists)
                return;
            foreach (FileInfo pageFile in youtubeDir.EnumerateFiles().OrderBy(f => f.Name))
                ReadYoutubePage(pageFile);
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

        private static void LinkYoutubeVideosToTwitchDetails()
        {
            foreach (var group in youtubeVideos.GroupJoin(
                details,
                y => Regex.Replace(y.title.ToLower(), @"\s", ""),
                d => Regex.Replace(d.Title.ToLower(), @"\s", ""),
                (youtubeVideo, details) => (youtubeVideo, details)))
            {
                // if (group.details.Skip(1).Any())
                //     throw new UserException($"2 Videos with the same title and duration, "
                //         + $"cannot resolve references: {group.youtubeVideo.duration} {group.youtubeVideo.title}");
                group.youtubeVideo.associatedDetail = group.details.FirstOrDefault(d => Math.Abs(d.seconds - group.youtubeVideo.seconds) < 300);
                if (group.youtubeVideo.associatedDetail != null)
                    group.youtubeVideo.associatedDetail.associatedYoutubeVideo = group.youtubeVideo;
            }
        }

        private static Options options = null!;
        private static List<Detail> details = null!;
        private static List<Collection> collections = [];
        private static List<YoutubeVideoEntry> youtubeVideos = [];
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

        private static void SplitMain(FileInfo fileInfo, string[] parts, bool dryRun)
        {
            try
            {
                Split(fileInfo, parts, dryRun);
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
            ReadYoutubePages();
            LinkYoutubeVideosToTwitchDetails();

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
            if (options.listYoutubeVsTwitch)
            {
                ListYoutubeVsTwitch();
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
                    Console.WriteLine($"  {detail.CreatedAt}  {detail.GetId(),10}  {detail.Title}");
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
                    Console.WriteLine($"  {entry.index,3}  {entry.detail.CreatedAt}  {entry.detail.GetId(),10}  {entry.title}");
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

        private static void ListYoutubeVsTwitch()
        {
            List<YoutubeVideoEntry> unassociatedYoutubeVideos = youtubeVideos.Where(y => y.associatedDetail == null).ToList();
            Console.WriteLine($"Unassociated youtube videos: {unassociatedYoutubeVideos.Count}");
            foreach (var video in unassociatedYoutubeVideos)
                Console.WriteLine($"  {video.title}");
            var unassociatedTwitchVideos = details.Where(d => d.associatedYoutubeVideo == null).ToList();
            Console.WriteLine($"Unassociated twitch videos: {unassociatedTwitchVideos.Count}");
            foreach (var video in unassociatedTwitchVideos)
                Console.WriteLine($"  {video.Type,-9}  {video.Title}");
            Console.WriteLine($"Successful links: {youtubeVideos.Count - unassociatedYoutubeVideos.Count}");
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
            return !options.validateVideos
                && (requestedToQuit || (options.timeLimit > 0 && mainTimer.Elapsed.TotalMinutes > options.timeLimit));
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
            if (!options.validateVideos)
                _ = Task.Factory.StartNew(StdInputWatcher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            await ProcessAllDownloads();
            while (concurrentFinalizationTasks != 0)
                Thread.Sleep(100);
        }

        private static async Task ProcessAllDownloads()
        {
            if (!options.validateVideos && options.collections != null)
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
                if (options.validateVideos || !options.nonCollections || detail.collectionEntries.Count == 0)
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

        private static string RunProcess(string programName, string[] args)
        {
            ProcessStartInfo startInfo = new(programName)
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true, // You shall not pass (inputs through to my sub processes).
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8,
            };
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);
            Process process = Process.Start(startInfo) ?? throw new UserException($"Failed to start process '{programName}'.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            List<string> lines = [];
            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { };
            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) { lines.Add(e.Data); } };
            process.WaitForExit();
            return string.Join("\n", lines);
        }

        private static async Task ProcessSpecificDownloads(Detail detail, CollectionEntry? entry)
        {
            string outputPath = GetOutputPath(detail, entry);

            string metadataPath = Path.Combine(outputPath, GetMetadataFilename(detail, entry));

            if (options.validateVideos && (IsExternal(detail, entry) || !File.Exists(metadataPath)))
                return;

            string videoPath;
            if (options.validateVideos)
            {
                videoPath = Path.Combine(outputPath, GetVideoFilename(detail, entry));
                if (!File.Exists(videoPath))
                    return;
                FileInfo videoFile = new(videoPath);
                if (videoFile == null)
                {
                    Console.WriteLine($"videoFile was null, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                long actualLength = videoFile.Length;
                if (actualLength == 0L)
                {
                    Console.WriteLine($"Empty video file, download likely got cancelled: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                // Documentation: https://ffmpeg.org/ffprobe.html
                string bitrateJsonStr = RunProcess("ffprobe", ["-output_format", "json", "-show_entries", "format=bit_rate", videoPath]);
                if (bitrateJsonStr == null)
                {
                    Console.WriteLine($"bitrateJsonStr was null, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                JObject bitrateJson = JObject.Parse(bitrateJsonStr);
                if (bitrateJson == null)
                {
                    Console.WriteLine($"bitrateJson was null, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                if (bitrateJson["format"] == null)
                {
                    Console.WriteLine($"bitrateJson[\"format\"] was null, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                if (bitrateJson["format"]!["bit_rate"] == null)
                {
                    Console.WriteLine($"bitrateJson[\"format\"]![\"bit_rate\"] was null, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                if (bitrateJson["format"]!["bit_rate"]!.Type != JTokenType.String)
                {
                    Console.WriteLine($"bitrateJson[\"format\"]![\"bit_rate\"]!.Type != JTokenType.String, I guess: {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                    return;
                }
                long bitrate = long.Parse(bitrateJson["format"]!.Value<string>("bit_rate")!);
                long expectedLength = detail.seconds * (bitrate / 8L);
                long diff = actualLength - expectedLength;
                if (diff < 0L) // Most tend to be slightly larger, 20 KB on average
                    Console.WriteLine($"Deviation from expected size: {diff / 1_000_000d,13:f6} MB : {detail.URL}  {GetCollectionPrefixForPrinting(detail, null)}{GetVideoFilename(detail, null)}");
                return;
            }

            if (File.Exists(metadataPath))
            {
                if (await detail.ReadThumbnailsFromMetadataFile(entry))
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

            videoPath = Path.Combine(outputPath, GetVideoFilename(detail, entry));
            if (options.downloadVideo && !File.Exists(videoPath))
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

        private partial struct Timestamp
        {
            public readonly int Hours => (int)((totalMs - (Minutes * 60L * 1000L) - (Seconds * 1000L) - Milliseconds) / (60L * 60L * 1000L));
            public readonly int Minutes => (int)((totalMs - (Seconds * 1000L) - Milliseconds) / (60L * 1000L) % 60L);
            public readonly int Seconds => (int)((totalMs - Milliseconds) / 1000L % 60L);
            public readonly int Milliseconds => (int)(totalMs % 1000L);
            public long totalMs;

            public Timestamp(long totalMs)
            {
                this.totalMs = totalMs;
            }

            public static bool TryParse(string input, out Timestamp timestamp)
            {
                timestamp = new();
                Match match = TimestampRegex().Match(input);
                if (!match.Success)
                    return false;
                long hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
                long minutes = int.Parse(match.Groups["minutes"].Value);
                long seconds = int.Parse(match.Groups["seconds"].Value);
                long milliseconds = match.Groups["milliseconds"].Success ? int.Parse(match.Groups["milliseconds"].Value) : 0;
                timestamp.totalMs = hours * 60L * 60L * 1000L
                    + minutes * 60L * 1000L
                    + seconds * 1000L
                    + milliseconds;
                return true;
            }

            [GeneratedRegex(@"^
                (?:
                    (?<hours>\d{1,2})
                    :
                )?
                (?<minutes>\d{1,2})
                :
                (?<seconds>\d{1,2})
                (?:
                    \.
                    (?<milliseconds>\d{1,3})
                )?
                $", RegexOptions.IgnorePatternWhitespace)]
            private static partial Regex TimestampRegex();

            public override readonly string ToString()
            {
                int hours = Hours;
                int ms = Milliseconds;
                string msStr = ms == 0 ? "" : $".{ms:d03}";
                return hours != 0
                    ? $"{hours}:{Minutes:d02}:{Seconds:d02}{msStr}"
                    : $"{Minutes}:{Seconds:d02}{msStr}";
            }
        }

        private struct Timeframe
        {
            public Timestamp start;
            public Timestamp stop;

            public static bool TryParse(string input, out Timeframe timeframe)
            {
                timeframe = new();
                string[] parts = input.Split('-');
                return parts.Length == 2
                    && Timestamp.TryParse(parts[0], out timeframe.start)
                    && Timestamp.TryParse(parts[1], out timeframe.stop);
            }

            public static Timeframe Parse(string input) => TryParse(input, out Timeframe timeframe)
                ? timeframe
                : throw new UserException($"Invalid timeframe '{input}'.");

            public override readonly string ToString() => $"{start}-{stop}";
        }

        private static void Split(FileInfo fileInfo, string[] parts, bool dryRun)
        {
            if (!fileInfo.Exists)
                throw new UserException($"No such file: {fileInfo}");
            if (parts.Length == 0)
                throw new UserException("No --parts specified.");
            foreach (Timeframe timeframe in parts.Select(Timeframe.Parse))
                GenerateSplitFile(fileInfo, timeframe, dryRun);
        }

        private static void GenerateSplitFile(FileInfo fileInfo, Timeframe timeframe, bool dryRun)
        {
            string inputFilename = fileInfo.FullName;
            string outputFilename = Path.Combine(
                Path.GetDirectoryName(inputFilename)!,
                $"{Path.GetFileNameWithoutExtension(inputFilename)} {timeframe.ToString().Replace("-", " - ").Replace(':', '-')}{Path.GetExtension(inputFilename)}");
            if (File.Exists(outputFilename))
            {
                Console.WriteLine($"Skipping: {outputFilename}");
                return;
            }
            Console.WriteLine($"Creating: {outputFilename}");
            if (dryRun)
                return;

            Timestamp lowerBound = new(Math.Max(0L, timeframe.start.totalMs - 10_000L));
            string framesStr = RunProcess("ffprobe",
            [
                "-output_format", "json",
                "-select_streams", "v:0",
                "-show_frames",
                "-read_intervals", $"{lowerBound}%+0:10",
                "-show_entries", "frame=key_frame,pkt_dts_time",
                inputFilename,
            ]);
            JArray frames = (JArray)JObject.Parse(framesStr)["frames"]!;
            var time = frames
                .Select(f =>
                {
                    int keyFrame = f.Value<int>("key_frame");
                    string? time = f.Value<string>("pkt_dts_time");
                    return (keyFrame: keyFrame != 0, time);
                })
                .Where(f => f.keyFrame && f.time != null)
                .Select(f => (timeStr: f.time!, time: decimal.Parse(f.time!)))
                .Where(f => f.time < timeframe.start.totalMs / 1000m)
                .Append((timeStr: "0:00", time: 0m))
                .OrderByDescending(f => f.time)
                .First();
            long ms = (long)(time.time * 1000m);

            RunProcess("ffmpeg",
            [
                // I would have preferred to seek on the output rather than input because this is
                // resulting in videos with the first frame having negative pkt_dts_time (pts_time
                // starts at 0 I think, or at least a positive value), but for  some reason even when
                // using time stamps for key frames it just does not work when putting -ss post -i.
                "-ss", time.timeStr,
                "-i", inputFilename,
                "-t", new Timestamp(timeframe.stop.totalMs - ms).ToString(),
                "-c", "copy",
                outputFilename
            ]);
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
            // function into the project here is just a shortcut to make myself not have to parse standard output from the
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
