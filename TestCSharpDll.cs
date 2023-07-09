using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using HtmlAgilityPack;

using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.IO;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Rate Your Genres";
            about.Description = "A plugin that syncs your album genres with the genres listed on Rate Your Music. build check";
            about.Author = "Slashscreen";
            about.TargetApplication = "";   //  the name of a Plugin Storage device or panel header for a dockable panel
            about.Type = PluginType.General;
            about.VersionMajor = 1;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            Console.WriteLine("Initializing!");
            CreateMenuItem();
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "prompt:";
                TextBox textBox = new TextBox();
                textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
                configPanel.Controls.AddRange(new Control[] { prompt, textBox });
            }
            return false;
        }
       
        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
            // TODO: get rid of log
        }

        private void CreateMenuItem() 
        {
            mbApiInterface.MB_AddMenuItem("mnuTools/Fetch genres for selected songs", "Begin", MenuClicked);
            Console.WriteLine("Menu Item");
        }

        private void MenuClicked(object sender, EventArgs args) 
        {
            Console.WriteLine("Clicked menu");
            ClearLog();
            string[] allFiles = { };
            mbApiInterface.Library_QueryFilesEx("domain=SelectedFiles", out allFiles);
            MetaDataType[] fields = {
                    MetaDataType.Album,
                    MetaDataType.AlbumArtist,
                    MetaDataType.Artist,
                    MetaDataType.TrackNo,
                    MetaDataType.Rating,
                    MetaDataType.TrackTitle,
                    MetaDataType.RatingLove,
                };

            // Group all songs by albums because we only need to get genres per album, not songs. Lots of wasted requests.
            Console.WriteLine($"number of selected items: {allFiles.Length}");
            var albumGroups = allFiles.GroupBy(file =>
            {
                mbApiInterface.Library_GetFileTags(file, fields, out var fileTags);
                return fileTags[0];
            });

            foreach (var albumGroup in albumGroups)
            {
                mbApiInterface.Library_GetFileTags(albumGroup.ElementAt(0), fields, out var fileTags);

                string artist = fileTags[1];
                string album = fileTags[0];

                string genres = GetAlbumGenres(artist, album);

                if (genres == "")
                {
                    ReportLogError($"Could not find album {album} by {artist} - skipping.");
                    continue;
                }

                foreach(var file in albumGroup)
                {
                    mbApiInterface.Library_SetFileTag(file, MetaDataType.Genre, genres);
                    mbApiInterface.Library_CommitTagsToFile(file);
                }
            }
        }

        public static List<string> ParseHTMLDoc(HtmlAgilityPack.HtmlDocument htmlDoc)
        {
            Console.WriteLine("Parsing document...");
            var programmerLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(concat(' ', @class, ' '), ' genre ')]"); // Looking for a.genre
            if (programmerLinks == null)
            {
                return new List<string>();
            }
            Console.WriteLine($"found {programmerLinks.Count} genres");
            return programmerLinks.Select(link => link.InnerText).ToList();
        }

        private static HtmlAgilityPack.HtmlDocument CallUrl(string fullUrl)
        {
	        HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(fullUrl);
            return doc;
        }

        string GetAlbumGenres(string artist, string album)
        {
            Console.WriteLine($"Getting genres for {artist} - {album}");
            // All possible URLs for albums, singles, EPs
            string[] urls = { 
                $"https://rateyourmusic.com/release/album/{Sanitize(artist)}/{Sanitize(album)}/",
                $"https://rateyourmusic.com/release/album/{Sanitize(artist)}/{Sanitize(album)}-1/", // for multiple versions
                $"https://rateyourmusic.com/release/single/{Sanitize(artist)}/{Sanitize(album)}/",
                $"https://rateyourmusic.com/release/single/{Sanitize(artist)}/{Sanitize(album)}-1/",
                $"https://rateyourmusic.com/release/ep/{Sanitize(artist)}/{Sanitize(album)}/",
                $"https://rateyourmusic.com/release/ep/{Sanitize(artist)}/{Sanitize(album)}-1/",
            };

            HtmlAgilityPack.HtmlDocument pageData;

            // If we get data, act on it
            foreach (string url in urls)
            {
                Console.WriteLine(url);
                pageData = CallUrl(url);
                if (pageData != null)
                {
                    List<string> genres = ParseHTMLDoc(pageData);
                    if (genres.Count > 0)
                    {
                        genres.ForEach(s => Console.WriteLine(s));
                        return genres.Aggregate((sum, next) => sum + ";" + next.Trim(' '));
                    }
                }
            }

            Console.WriteLine("No page loaded.");
            return "";
        }

        private static string Sanitize(string input) => input.Replace(" ", "-").Replace(".", "_").ToLower(); // could use a regex and may need to.
        

        void ReportLogError(string text) { }

        void ClearLog() { }
    }
}
