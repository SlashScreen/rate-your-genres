using System;
using System.Linq;
using System.Runtime.InteropServices;
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

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    break;
            }
        }

        private void CreateMenuItem() 
        {
            mbApiInterface.MB_AddMenuItem("mnuTools/Fetch genres for selected songs", "Begin", MenuClicked);
            Console.WriteLine("Menu Item");
        }

        private void MenuClicked(object sender, EventArgs args) 
        {
            Console.WriteLine("Clicked menu");
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
            string[] urls = { 
                $"https://rateyourmusic.com/release/album/{Sanitize(artist)}/{Sanitize(album)}/",
                $"https://rateyourmusic.com/release/album/{Sanitize(artist)}/{Sanitize(album)}-1/", // for multiple versions
                $"https://rateyourmusic.com/release/ep/{Sanitize(artist)}/{Sanitize(album)}/",
                $"https://rateyourmusic.com/release/ep/{Sanitize(artist)}/{Sanitize(album)}-1/" // for multiple versions
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
                        return genres.Aggregate((sum, next) => sum + ";" + next);
                    }
                }
            }

            Console.WriteLine("No page loaded.");
            return "";
        }

        private static string Sanitize(string input)
        {
            return input.Replace(" ", "-").Replace(".", "_").ToLower(); // could use a regex but who cares. Maybe I do need a regex.
        }

        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        //public string[] GetProviders()
        //{
        //    return null;
        //}

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        //public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        //{
        //    return null;
        //}

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        //public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        //{
        //    //Return Convert.ToBase64String(artworkBinaryData)
        //    return null;
        //}

        //  presence of this function indicates to MusicBee that this plugin has a dockable panel. MusicBee will create the control and pass it as the panel parameter
        //  you can add your own controls to the panel if needed
        //  you can control the scrollable area of the panel using the mbApiInterface.MB_SetPanelScrollableArea function
        //  to set a MusicBee header for the panel, set about.TargetApplication in the Initialise function above to the panel header text
        //public int OnDockablePanelCreated(Control panel)
        //{
        //  //    return the height of the panel and perform any initialisation here
        //  //    MusicBee will call panel.Dispose() when the user removes this panel from the layout configuration
        //  //    < 0 indicates to MusicBee this control is resizable and should be sized to fill the panel it is docked to in MusicBee
        //  //    = 0 indicates to MusicBee this control resizeable
        //  //    > 0 indicates to MusicBee the fixed height for the control.Note it is recommended you scale the height for high DPI screens(create a graphics object and get the DpiY value)
        //    float dpiScaling = 0;
        //    using (Graphics g = panel.CreateGraphics())
        //    {
        //        dpiScaling = g.DpiY / 96f;
        //    }
        //    panel.Paint += panel_Paint;
        //    return Convert.ToInt32(100 * dpiScaling);
        //}

        // presence of this function indicates to MusicBee that the dockable panel created above will show menu items when the panel header is clicked
        // return the list of ToolStripMenuItems that will be displayed
        //public List<ToolStripItem> GetHeaderMenuItems()
        //{
        //    List<ToolStripItem> list = new List<ToolStripItem>();
        //    list.Add(new ToolStripMenuItem("A menu item"));
        //    return list;
        //}

        //private void panel_Paint(object sender, PaintEventArgs e)
        //{
        //    e.Graphics.Clear(Color.Red);
        //    TextRenderer.DrawText(e.Graphics, "hello", SystemFonts.CaptionFont, new Point(10, 10), Color.Blue);
        //}

    }
}