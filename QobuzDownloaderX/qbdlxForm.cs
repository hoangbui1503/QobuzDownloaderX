﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using QobuzDownloaderX;
using QopenAPI;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using QobuzDownloaderX.Properties;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using QobuzDownloaderX.Download;
using ZetaLongPaths;
using System.Globalization;

namespace QobuzDownloaderX
{
    public partial class qbdlxForm : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // width of ellipse
            int nHeightEllipse // height of ellipse
        );

        public Service QoService = new Service();
        public User QoUser = new User();
        public Artist QoArtist = new Artist();
        public Album QoAlbum = new Album();
        public Item QoItem = new Item();
        public SearchAlbumResult QoAlbumSearch = new SearchAlbumResult();
        public SearchTrackResult QoTrackSearch = new SearchTrackResult();
        public Favorites QoFavorites = new Favorites();
        public Playlist QoPlaylist = new Playlist();
        public QopenAPI.Label QoLabel = new QopenAPI.Label();
        public QopenAPI.Stream QoStream = new QopenAPI.Stream();

        public bool downloadPanelActive = false;
        public bool aboutPanelActive = false;
        public bool settingsPanelActive = false;

        //Create logger for this form
        public Logger logger { get; set; }

        public string downloadLocation { get; set; }
        public string artistTemplate { get; set; }
        public string albumTemplate { get; set; }
        public string trackTemplate { get; set; }
        public string playlistTemplate { get; set; }
        public string favoritesTemplate { get; set; }

        public string app_id { get; set; }
        public string app_secret { get; set; }
        public string user_id { get; set; }
        public string user_auth_token { get; set; }
        public string user_display_name { get; set; }
        public string user_label { get; set; }
        public string user_avatar { get; set; }

        public string qobuz_id { get; set; }
        public string format_id { get; set; }
        public string audio_format { get; set; }

        public string embeddedArtSize { get; set; }
        public string savedArtSize { get; set; }
        public string themeName { get; set; }
        public Theme theme { get; set; }

        public string latestWebResponse { get; set; }

        GetInfo getInfo = new GetInfo();
        DownloadAlbum downloadAlbum = new DownloadAlbum();
        DownloadTrack downloadTrack = new DownloadTrack();
        SearchPanelHelper searchPanelHelper = new SearchPanelHelper();

        public qbdlxForm()
        {
            // Create new log file
            Directory.CreateDirectory("logs");
            logger = new Logger("logs\\qbdlxForm_log-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            logger.Debug("Logger started, QBDLX form initialized!");

            InitializeComponent();
            _qbdlxForm = this;
        }

        public static qbdlxForm _qbdlxForm;
        public readonly Theming _themeManager = new Theming();

        public void update(string text)
        {
            downloadOutput.Invoke(new Action(() => downloadOutput.Text = text));
        }

        public void updateTemplates()
        {
            logger.Debug("Updating templates");
            artistTemplate = artistTemplateTextbox.Text;
            albumTemplate = albumTemplateTextbox.Text;
            trackTemplate = trackTemplateTextbox.Text;
            playlistTemplate = playlistTemplateTextbox.Text;
            favoritesTemplate = favoritesTemplateTextbox.Text;
        }

        private void LoadSavedTemplates()
        {
            artistTemplateTextbox.Text = Settings.Default.savedArtistTemplate;
            albumTemplateTextbox.Text = Settings.Default.savedAlbumTemplate;
            trackTemplateTextbox.Text = Settings.Default.savedTrackTemplate;
            playlistTemplateTextbox.Text = Settings.Default.savedPlaylistTemplate;
            favoritesTemplateTextbox.Text = Settings.Default.savedFavoritesTemplate;
            updateTemplates();
        }

        private void LoadQualitySettings()
        {
            mp3Button2.Checked = Settings.Default.quality1;
            flacLowButton2.Checked = Settings.Default.quality2;
            flacMidButton2.Checked = Settings.Default.quality3;
            flacHighButton2.Checked = Settings.Default.quality4;
            format_id = Settings.Default.qualityFormat;
            audio_format = Settings.Default.audioType;
        }

        private void LoadTaggingSettings()
        {
            streamableCheckbox.Checked = Settings.Default.streamableCheck;
            downloadSpeedCheckbox.Checked = Settings.Default.showDownloadSpeed;
            albumTitleCheckbox.Checked = Settings.Default.albumTag;
            albumArtistCheckbox.Checked = Settings.Default.albumArtistTag;
            trackArtistCheckbox.Checked = Settings.Default.artistTag;
            composerCheckbox.Checked = Settings.Default.composerTag;
            copyrightCheckbox.Checked = Settings.Default.copyrightTag;
            labelCheckbox.Checked = Settings.Default.labelTag;
            discNumberCheckbox.Checked = Settings.Default.discTag;
            discTotalCheckbox.Checked = Settings.Default.totalDiscsTag;
            genreCheckbox.Checked = Settings.Default.genreTag;
            isrcCheckbox.Checked = Settings.Default.isrcTag;
            releaseTypeCheckbox.Checked = Settings.Default.typeTag;
            explicitCheckbox.Checked = Settings.Default.explicitTag;
            trackTitleCheckbox.Checked = Settings.Default.trackTitleTag;
            trackNumberCheckbox.Checked = Settings.Default.trackTag;
            trackTotalCheckbox.Checked = Settings.Default.totalTracksTag;
            upcCheckbox.Checked = Settings.Default.upcTag;
            releaseDateCheckbox.Checked = Settings.Default.yearTag;
            coverArtCheckbox.Checked = Settings.Default.imageTag;
            commentCheckbox.Checked = Settings.Default.commentTag;
            commentTextbox.Text = Settings.Default.commentText;
            embeddedArtSizeSelect.SelectedIndex = Settings.Default.savedEmbeddedArtSize;
            savedArtSizeSelect.SelectedIndex = Settings.Default.savedSavedArtSize;
        }

        private void SetDownloadPath()
        {
            downloadLocation = Settings.Default.savedFolder;
            downloadFolderTextbox.Text = !string.IsNullOrEmpty(downloadLocation) ? downloadLocation : "no folder selected";
            folderBrowser.SelectedPath = downloadLocation;
            logger.Info("Saved download path: " + folderBrowser.SelectedPath);
        }

        private void InitializePanels()
        {
            // Set all panels to specific point
            var panelPosition = new Point(179, 0);
            downloaderPanel.Location = panelPosition;
            aboutPanel.Location = panelPosition;
            settingsPanel.Location = panelPosition;
            extraSettingsPanel.Location = panelPosition;
            searchPanel.Location = panelPosition;

            // Startup with downloadPanel active and visable, all others not visable
            downloaderPanel.Visible = true;
            aboutPanel.Visible = false;
            settingsPanel.Visible = false;
            downloadPanelActive = true;
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }
        private void InitializeTheme()
        {
            // Populate theme options in settings
            _themeManager.PopulateThemeOptions(this);

            // Set and load theme
            themeName = Settings.Default.currentTheme;
            if (!string.IsNullOrEmpty(themeName)) { themeComboBox.SelectedItem = themeName; }
            theme = _themeManager._currentTheme;
        }

        private void qbdlxForm_Load(object sender, EventArgs e)
        {
            logger.Debug("QBDLX form loaded!");

            // Round corners of form
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // Load settings / download location / theme / panels
            SetDownloadPath();
            LoadSavedTemplates();
            LoadQualitySettings();
            LoadTaggingSettings();
            InitializeTheme();
            InitializePanels();

            // Get and display version number.
            versionNumber.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            
            // Set display_name to welcomeLabel
            welcomeLabel.Text = welcomeLabel.Text.Replace("{username}", user_display_name);

            // Get user account + subscription information for about panel
            downloadOutput.Text = "Welcome " + user_display_name + "!";

            TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
            var endDate = QoUser?.UserInfo?.Subscription?.EndDate ?? "N/A - Family account";
            var subscription = !string.IsNullOrEmpty(QoUser?.UserInfo?.Credential?.label?.ToString())
                ? textInfo.ToTitleCase(QoUser?.UserInfo?.Credential?.label?.ToString().ToLower().Replace("-", " ")).Replace("Hifi", "HiFi")
                : "N/A - Expired";

            userInfoTextbox.Text = userInfoTextbox.Text
                .Replace("{user_id}", user_id)
                .Replace("{user_email}", QoUser?.UserInfo?.Email)
                .Replace("{user_country}", QoUser?.UserInfo?.Country)
                .Replace("{user_subscription}", subscription)
                .Replace("{user_subscription_expiration}", endDate);


            downloadOutput.AppendText(QoUser.UserInfo.Credential.label == null
                ? $"\r\n\r\nYOUR SUBSCRIPTION HAS EXPIRED, DOWNLOADS WILL BE LIMITED TO 30 SECOND SNIPPETS!\r\n\r\nDownload Path\r\n{folderBrowser.SelectedPath}"
                : $"\r\n\r\nDownload Path\r\n{folderBrowser.SelectedPath}");
        }

        private void qualitySelectButton_Click(object sender, EventArgs e)
        {
            if (qualitySelectPanel.Visible == true)
            {
                qualitySelectPanel.Visible = false;
            }
            else
            {
                qualitySelectPanel.Visible = true;
            }
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Exiting");
            System.Windows.Forms.Application.Exit();
        }

        private void minimizeButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Minimizing");
            this.WindowState = FormWindowState.Minimized;
        }

        private void searchTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode.Equals(Keys.Enter))
            {
                e.SuppressKeyPress = true;
            }
        }

        private void inputTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                getLinkType();
            }
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            getLinkType();
        }

        public async void getLinkType()
        {
            downloadOutput.Focus();
            progressLabel.Invoke(new Action(() => progressLabel.Text = "Checking link..."));

            // Check if there's no selected path.
            if (downloadLocation == null | downloadLocation == "" | downloadLocation == "no folder selected")
            {
                // If there is NOT a saved path.
                logger.Warning("No path has been set! Remember to Choose a Folder!");
                downloadOutput.Invoke(new Action(() => downloadOutput.Text = String.Empty));
                downloadOutput.Invoke(new Action(() => downloadOutput.AppendText("No path has been set! Remember to Choose a Folder!\r\n")));
                progressLabel.Invoke(new Action(() => progressLabel.Text = "No download active"));
                return;
            }

            string albumLink = inputTextbox.Text;

            var qobuzStoreLinkGrab = Regex.Match(albumLink, @"https:\/\/(?:.*?).qobuz.com\/(?<region>.*?)\/(?<type>.*?)\/(?<name>.*?)\/(?<id>.*?)$").Groups;
            var linkRegion = qobuzStoreLinkGrab[1].Value;
            var storeLinkType = qobuzStoreLinkGrab[2].Value;
            var linkName = qobuzStoreLinkGrab[3].Value;
            var qobuzStoreLinkId = qobuzStoreLinkGrab[4].Value;

            if (linkRegion != null)
            {
                if (storeLinkType == "album")
                {
                    albumLink = "https://play.qobuz.com/album/" + qobuzStoreLinkId;
                }
                else if (storeLinkType == "interpreter")
                {
                    albumLink = "https://play.qobuz.com/artist/" + qobuzStoreLinkId;
                }
            }

            var qobuzLinkIdGrab = Regex.Match(albumLink, @"https:\/\/(?:.*?).qobuz.com\/(?<type>.*?)\/(?<id>.*?)$").Groups;
            var linkType = qobuzLinkIdGrab[1].Value;
            var qobuzLinkId = qobuzLinkIdGrab[2].Value;

            qobuz_id = qobuzLinkId;

            downloadTrack.clearOutputText();
            getInfo.outputText = null;
            getInfo.updateDownloadOutput("Checking Link...");

            switch (linkType)
            {
                case "album":
                    await Task.Run(() => getInfo.getAlbumInfoLabels(app_id, qobuz_id, user_auth_token));
                    QoAlbum = getInfo.QoAlbum;
                    if (QoAlbum == null)
                    {
                        getInfo.updateDownloadOutput("Qobuz API error. Maybe release isn't available in this account region?");
                        progressLabel.Invoke(new Action(() => progressLabel.Text = "No download active"));
                        break;
                    }
                    await updateAlbumInfoLabels(QoAlbum);
                    await Task.Run(() => downloadAlbum.downloadAlbum(app_id, qobuz_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                    break;
                case "track":
                    await Task.Run(() => getInfo.getTrackInfoLabels(app_id, qobuz_id, user_auth_token));
                    QoItem = getInfo.QoItem;
                    QoAlbum = getInfo.QoAlbum;
                    await updateAlbumInfoLabels(QoAlbum);
                    await Task.Run(() => downloadTrack.downloadIndividualTrack(app_id, qobuz_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                    break;
                case "playlist":
                    await Task.Run(() => getInfo.getPlaylistInfoLabels(app_id, qobuz_id, user_auth_token));
                    QoPlaylist = getInfo.QoPlaylist;
                    updatePlaylistInfoLabels(QoPlaylist);
                    foreach (var item in QoPlaylist.Tracks.Items)
                    {
                        try
                        {
                            string track_id = item.Id.ToString();
                            await Task.Run(() => getInfo.getTrackInfoLabels(app_id, track_id, user_auth_token));
                            QoItem = item;
                            QoAlbum = getInfo.QoAlbum;
                            await Task.Run(() => downloadTrack.downloadPlaylistTrack(app_id, track_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, playlistTemplate, QoAlbum, QoItem, QoPlaylist));
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    // Say the downloading is finished when it's completed.
                    getInfo.outputText = qbdlxForm._qbdlxForm.downloadOutput.Text;
                    getInfo.updateDownloadOutput("\r\n" + "DOWNLOAD COMPLETE");
                    progressLabel.Invoke(new Action(() => progressLabel.Text = "No download active"));
                    break;
                case "artist":
                    await Task.Run(() => getInfo.getArtistInfo(app_id, qobuz_id, user_auth_token));
                    QoArtist = getInfo.QoArtist;
                    foreach (var item in QoArtist.Albums.Items)
                    {
                        try
                        {
                            string album_id = item.Id.ToString();
                            await Task.Run(() => getInfo.getAlbumInfoLabels(app_id, album_id, user_auth_token));
                            QoAlbum = getInfo.QoAlbum;
                            await updateAlbumInfoLabels(QoAlbum);
                            await Task.Run(() => downloadAlbum.downloadAlbum(app_id, qobuz_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    break;
                case "label":
                    await Task.Run(() => getInfo.getLabelInfo(app_id, qobuz_id, user_auth_token));
                    QoLabel = getInfo.QoLabel;
                    foreach (var item in QoLabel.Albums.Items)
                    {
                        try
                        {
                            string album_id = item.Id.ToString();
                            await Task.Run(() => getInfo.getAlbumInfoLabels(app_id, album_id, user_auth_token));
                            QoAlbum = getInfo.QoAlbum;
                            await updateAlbumInfoLabels(QoAlbum);
                            await Task.Run(() => downloadAlbum.downloadAlbum(app_id, qobuz_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    break;
                case "user":
                    if (qobuzLinkId.Contains("albums"))
                    {
                        await Task.Run(() => getInfo.getFavoritesInfo(app_id, user_id, "albums", user_auth_token));
                        QoFavorites = getInfo.QoFavorites;
                        foreach (var item in QoFavorites.Albums.Items)
                        {
                            try
                            {
                                string album_id = item.Id.ToString();
                                await Task.Run(() => getInfo.getAlbumInfoLabels(app_id, album_id, user_auth_token));
                                QoAlbum = getInfo.QoAlbum;
                                await updateAlbumInfoLabels(QoAlbum);
                                await Task.Run(() => downloadAlbum.downloadAlbum(app_id, album_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                    else if (qobuzLinkId.Contains("tracks"))
                    {
                        await Task.Run(() => getInfo.getFavoritesInfo(app_id, user_id, "tracks", user_auth_token));
                        QoFavorites = getInfo.QoFavorites;
                        foreach (var item in QoFavorites.Tracks.Items)
                        {
                            try
                            {
                                string track_id = item.Id.ToString();
                                await Task.Run(() => getInfo.getTrackInfoLabels(app_id, track_id, user_auth_token));
                                QoItem = getInfo.QoItem;
                                QoAlbum = getInfo.QoAlbum;
                                await updateAlbumInfoLabels(QoAlbum);
                                await Task.Run(() => downloadTrack.downloadIndividualTrack(app_id, track_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                    else if (qobuzLinkId.Contains("artists"))
                    {
                        await Task.Run(() => getInfo.getFavoritesInfo(app_id, user_id, "artists", user_auth_token));
                        QoFavorites = getInfo.QoFavorites;
                        foreach (var item in QoFavorites.Artists.Items)
                        {
                            try
                            {
                                string artist_id = item.Id.ToString();
                                await Task.Run(() => getInfo.getArtistInfo(app_id, artist_id, user_auth_token));
                                QoArtist = getInfo.QoArtist;
                                foreach (var artistItem in QoArtist.Albums.Items)
                                {
                                    try
                                    {
                                        string album_id = artistItem.Id.ToString();
                                        await Task.Run(() => getInfo.getAlbumInfoLabels(app_id, album_id, user_auth_token));
                                        QoAlbum = getInfo.QoAlbum;
                                        await updateAlbumInfoLabels(QoAlbum);
                                        await Task.Run(() => downloadAlbum.downloadAlbum(app_id, album_id, format_id, audio_format, user_auth_token, app_secret, downloadLocation, artistTemplate, albumTemplate, trackTemplate, QoAlbum));
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // Say what isn't available at the moment.
                        downloadOutput.Invoke(new Action(() => downloadOutput.Text = String.Empty));
                        downloadOutput.Invoke(new Action(() => downloadOutput.AppendText("Not implemented yet or the URL is not understood. Is there a typo?")));
                        progressLabel.Invoke(new Action(() => progressLabel.Text = "No download active"));
                        return;
                    }
                    break;
                default:
                    // Say what isn't available at the moment.
                    downloadOutput.Invoke(new Action(() => downloadOutput.Text = String.Empty));
                    downloadOutput.Invoke(new Action(() => downloadOutput.AppendText("Not implemented yet or the URL is not understood. Is there a typo?")));
                    progressLabel.Invoke(new Action(() => progressLabel.Text = "No download active"));
                    return;
            }
        }

        public async Task updateAlbumInfoLabels(Album QoAlbum)
        {
            // For albums with multiple main artists listed
            if (QoAlbum.Artists.Count > 1)
            {
                var mainArtists = QoAlbum.Artists.Where(a => a.Roles.Contains("main-artist")).ToList();
                string allButLastArtist = string.Join(", ", mainArtists.Take(mainArtists.Count - 1).Select(a => a.Name));
                string lastArtist = mainArtists.Last().Name;

                if (mainArtists.Count > 1)
                {
                    artistLabel.Text = allButLastArtist + " && " + lastArtist;
                }
                else
                {
                    artistLabel.Text = lastArtist;
                }
            }
            else
            {
                artistLabel.Text = QoAlbum.Artist.Name.Replace(@"&", @"&&");
            }

            if (QoAlbum.Version == null) { albumLabel.Text = QoAlbum.Title.Replace(@"&", @"&&"); } else { albumLabel.Text = QoAlbum.Title.Replace(@"&", @"&&").TrimEnd() + " (" + QoAlbum.Version + ")"; }
            dateLabel.Text = QoAlbum.ReleaseDateOriginal;
            try { albumPictureBox.ImageLocation = QoAlbum.Image.Small; } catch { }
        }

        public void updatePlaylistInfoLabels(Playlist QoPlaylist)
        {
            artistLabel.Text = QoPlaylist.Owner.Name.Replace(@"&", @"&&") + "'s Playlist";
            albumLabel.Text = QoPlaylist.Name.Replace(@"&", @"&&");
            dateLabel.Text = "N/A";
            try { albumPictureBox.ImageLocation = QoPlaylist.Images300[0]; } catch { }
        }

        private void SetPlaceholder(TextBox textBox, string placeholderText, bool isFocused)
        {
            if (isFocused)
            {
                if (textBox.Text == placeholderText)
                {
                    textBox.Text = null;
                    textBox.ForeColor = ColorTranslator.FromHtml(theme.TextBoxText);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.ForeColor = ColorTranslator.FromHtml(theme.PlaceholderTextBoxText);
                    textBox.Text = placeholderText;
                }
            }
        }

        private void inputTextbox_Click(object sender, EventArgs e)
        {
            SetPlaceholder(inputTextbox, "Paste a Qobuz URL...", true);
        }

        private void inputTextbox_Leave(object sender, EventArgs e)
        {
            SetPlaceholder(inputTextbox, "Paste a Qobuz URL...", false);
        }

        private void searchTextbox_Click(object sender, EventArgs e)
        {
            SetPlaceholder(searchTextbox, "Input your search...", true);
        }

        private void searchTextbox_Leave(object sender, EventArgs e)
        {
            SetPlaceholder(searchTextbox, "Input your search...", false);
        }

        private void openFolderButton_Click(object sender, EventArgs e)
        {
            // Open selcted folder
            if (folderBrowser.SelectedPath == null | folderBrowser.SelectedPath == "")
            {
                // If there's no selected path.
                MessageBox.Show("No path selected!", "ERROR",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
            {
                // If selected path doesn't exist, create it. (Will be ignored if it does)
                System.IO.Directory.CreateDirectory(folderBrowser.SelectedPath);
                // Open selcted folder
                Process.Start(folderBrowser.SelectedPath);
            }
        }

        private void selectFolderButton_Click(object sender, EventArgs e)
        {
            Thread t = new Thread((ThreadStart)(() =>
            {
                // Open Folder Browser to select path & Save the selection
                folderBrowser.ShowDialog();
                Settings.Default.savedFolder = folderBrowser.SelectedPath + @"\";
                Settings.Default.Save();
            }));

            // Run your code from a thread that joins the STA Thread
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            
            downloadFolderTextbox.Text = folderBrowser.SelectedPath + @"\";
            downloadLocation = folderBrowser.SelectedPath + @"\";
        }

        private void saveTemplatesButton_Click(object sender, EventArgs e)
        {
            Settings.Default.savedArtistTemplate = artistTemplateTextbox.Text;
            Settings.Default.savedAlbumTemplate = albumTemplateTextbox.Text;
            Settings.Default.savedTrackTemplate = trackTemplateTextbox.Text;
            Settings.Default.savedPlaylistTemplate = playlistTemplateTextbox.Text;
            Settings.Default.savedFavoritesTemplate = favoritesTemplateTextbox.Text;
            Settings.Default.Save();
            updateTemplates();
        }

        #region Quality Selection

        private void flacHighButton2_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality4 = flacHighButton2.Checked;
            Settings.Default.Save();

            if (flacHighButton2.Checked == true)
            {
                logger.Debug("Setting format ID to 27");
                format_id = "27";
                audio_format = ".flac";
                Settings.Default.qualityFormat = format_id;
                Settings.Default.audioType = audio_format;
            }
        }

        private void flacMidButton2_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality3 = flacMidButton2.Checked;
            Settings.Default.Save();

            if (flacMidButton2.Checked == true)
            {
                logger.Debug("Setting format ID to 7");
                format_id = "7";
                audio_format = ".flac";
                Settings.Default.qualityFormat = format_id;
                Settings.Default.audioType = audio_format;
            }
        }

        private void flacLowButton2_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality2 = flacLowButton2.Checked;
            Settings.Default.Save();

            if (flacLowButton2.Checked == true)
            {
                logger.Debug("Setting format ID to 6");
                format_id = "6";
                audio_format = ".flac";
                Settings.Default.qualityFormat = format_id;
                Settings.Default.audioType = audio_format;
            }
        }

        private void mp3Button2_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality1 = mp3Button2.Checked;
            Settings.Default.Save();

            if (mp3Button2.Checked == true)
            {
                logger.Debug("Setting format ID to 5");
                format_id = "5";
                audio_format = ".mp3";
                Settings.Default.qualityFormat = format_id;
                Settings.Default.audioType = audio_format;
            }
        }

        private void flacHighLabel2_Click(object sender, EventArgs e)
        {
            flacHighButton2.Checked = true;
        }

        private void flacMidLabel2_Click(object sender, EventArgs e)
        {
            flacMidButton2.Checked = true;
        }

        private void flacLowLabel2_Click(object sender, EventArgs e)
        {
            flacLowButton2.Checked = true;
        }

        private void mp3Label2_Click(object sender, EventArgs e)
        {
            mp3Button2.Checked = true;
        }

        #endregion

        #region Tagging Options
        private void albumTitleCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumTag = albumTitleCheckbox.Checked;
            Settings.Default.Save();
        }

        private void albumArtistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumArtistTag = albumArtistCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackTitleCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTitleTag = trackTitleCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackArtistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.artistTag = trackArtistCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTag = trackTitleCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalTracksTag = trackTotalCheckbox.Checked;
            Settings.Default.Save();
        }

        private void discNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.discTag = discNumberCheckbox.Checked;
            Settings.Default.Save();
        }

        private void discTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalDiscsTag = discTotalCheckbox.Checked;
            Settings.Default.Save();
        }

        private void releaseDateCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.yearTag = releaseDateCheckbox.Checked;
            Settings.Default.Save();
        }

        private void genreCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.genreTag = genreCheckbox.Checked;
            Settings.Default.Save();
        }

        private void composerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.composerTag = composerCheckbox.Checked;
            Settings.Default.Save();
        }

        private void copyrightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.copyrightTag = copyrightCheckbox.Checked;
            Settings.Default.Save();
        }

        private void labelCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.labelTag = labelCheckbox.Checked;
            Settings.Default.Save();
        }

        private void isrcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.isrcTag = isrcCheckbox.Checked;
            Settings.Default.Save();
        }

        private void releaseTypeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.typeTag = releaseTypeCheckbox.Checked;
            Settings.Default.Save();
        }

        private void upcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.upcTag = upcCheckbox.Checked;
            Settings.Default.Save();
        }

        private void explicitCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.explicitTag = explicitCheckbox.Checked;
            Settings.Default.Save();
        }

        private void coverArtCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.imageTag = coverArtCheckbox.Checked;
            Settings.Default.Save();
        }
        
        private void commentCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.commentTag = commentCheckbox.Checked;
            Settings.Default.Save();
        }

        private void commentTextbox_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.commentText = commentTextbox.Text;
            Settings.Default.Save();
        }

        private void embeddedArtSizeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set artSize to selected value, and save selected option to settings.
            embeddedArtSize = embeddedArtSizeSelect.Text;
            Settings.Default.savedEmbeddedArtSize = embeddedArtSizeSelect.SelectedIndex;
            Settings.Default.Save();
        }

        private void savedArtSizeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set artSize to selected value, and save selected option to settings.
            savedArtSize = savedArtSizeSelect.Text;
            Settings.Default.savedSavedArtSize = savedArtSizeSelect.SelectedIndex;
            Settings.Default.Save();
        }

        private void themeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Save the selected theme name to settings
            string selectedTheme = themeComboBox.SelectedItem.ToString();
            Settings.Default.currentTheme = selectedTheme; // Assuming you have this property in settings
            Settings.Default.Save();

            // Load and apply the selected theme
            _themeManager.LoadTheme(selectedTheme);
            _themeManager.ApplyTheme(this);
        }
        #endregion

        #region Navigation Buttons
        private void logoutButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Restarting program to logout");
            // Could use some work, but this works.
            Process.Start("QobuzDownloaderX.exe");
            System.Windows.Forms.Application.Exit();
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Opening about panel");
            // Make other panels invisable, make about panel visible
            downloaderPanel.Visible = false;
            searchPanel.Visible = false;
            settingsPanel.Visible = false;
            extraSettingsPanel.Visible = false;
            aboutPanel.Visible = true;

            // Make this the active panel
            downloadPanelActive = false;
            settingsPanelActive = false;
            aboutPanelActive = true;

            // Change button colors
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Opening settings panel");
            // Make other panels invisable, make settings panel visible
            downloaderPanel.Visible = false;
            searchPanel.Visible = false;
            aboutPanel.Visible = false;
            settingsPanel.Visible = true;

            // Make this the active panel
            downloadPanelActive = false;
            aboutPanelActive = false;
            extraSettingsPanel.Visible = false;
            settingsPanelActive = true;

            // Change button colors
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        public void downloaderButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Opening download panel");
            // Make other panels invisable, make settings panel visible
            aboutPanel.Visible = false;
            searchPanel.Visible = false;
            settingsPanel.Visible = false;
            extraSettingsPanel.Visible = false;
            downloaderPanel.Visible = true;

            // Make this the active panel
            aboutPanelActive = false;
            settingsPanelActive = false;
            downloadPanelActive = true;

            // Change button colors
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Opening search panel");
            // Make other panels invisable, make settings panel visible
            aboutPanel.Visible = false;
            settingsPanel.Visible = false;
            extraSettingsPanel.Visible = false;
            downloaderPanel.Visible = false;
            searchPanel.Visible = true;

            // Make this the active panel
            aboutPanelActive = false;
            settingsPanelActive = false;
            downloadPanelActive = true;

            // Change button colors
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        private void additionalSettingsButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Opening extra settings panel");
            // Make other panels invisable, make settings panel visible
            aboutPanel.Visible = false;
            searchPanel.Visible = false;
            downloaderPanel.Visible = false;
            settingsPanel.Visible = false;
            extraSettingsPanel.Visible = true;

            // Make settings the active panel
            aboutPanelActive = false;
            downloadPanelActive = false;
            settingsPanelActive = true;

            // Change button colors
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        private void closeAdditionalButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Closing extra settings panel");
            // Make other panels invisable, make settings panel visible
            aboutPanel.Visible = false;
            searchPanel.Visible = false;
            downloaderPanel.Visible = false;
            extraSettingsPanel.Visible = false;
            settingsPanel.Visible = true;

            // Make settings the active panel
            aboutPanelActive = false;
            downloadPanelActive = false;
            settingsPanelActive = true;

            // Change button colors
            aboutButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            downloaderButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            searchButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.ButtonBackground);
            settingsButton.BackColor = ColorTranslator.FromHtml(_themeManager._currentTheme.HighlightedButtonBackground);
        }

        #endregion

        private void streamableCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.streamableCheck = streamableCheckbox.Checked;
            Settings.Default.Save();
        }

        private void downloadSpeedCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.showDownloadSpeed = downloadSpeedCheckbox.Checked;
            Settings.Default.Save();
        }

        private void fixMD5sCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.fixMD5s = fixMD5sCheckbox.Checked;
            Settings.Default.Save();
        }

        private void downloadOutput_TextChanged(object sender, EventArgs e)
        {
            downloadOutput.SelectionStart = downloadOutput.Text.Length;
            downloadOutput.ScrollToCaret();
        }

        #region Window Moving

        // For moving form with click and drag
        private void logoPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void movingLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void downloadLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void settingsLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void extraSettingsLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void aboutLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void searchLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        #endregion

        private async void searchAlbumsButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Hiding search buttons");
            searchAlbumsButton.Visible = false;
            searchTracksButton.Visible = false;
            searchingLabel.Visible = true;
            searchResultsPanel.Hide();

            string searchQuery = searchTextbox.Text;

            if (string.IsNullOrEmpty(searchQuery) | searchQuery == "Input your search...")
            {
                logger.Debug("Search query was null, canceling");
                searchResultsPanel.Show();
                searchAlbumsButton.Visible = true;
                searchTracksButton.Visible = true;
                searchingLabel.Visible = false;
                return;
            }

            try
            {
                logger.Debug("Search for releases started");
                await Task.Run(() => searchPanelHelper.SearchInitiate("releases", app_id, searchQuery, user_auth_token));
            }
            catch (Exception ex)
            {
                logger.Error("Error occured during searchAlbumsButton_Click, error below:\r\n" + ex);
                searchResultsPanel.Show();
                searchAlbumsButton.Visible = true;
                searchTracksButton.Visible = true;
                searchingLabel.Visible = false;
                return;
            }
            logger.Debug("Search completed!");
            searchResultsPanel.Show();
            searchAlbumsButton.Visible = true;
            searchTracksButton.Visible = true;
            searchingLabel.Visible = false;
            return;
        }

        private async void searchTracksButton_Click(object sender, EventArgs e)
        {
            logger.Debug("Hiding search buttons");
            searchAlbumsButton.Visible = false;
            searchTracksButton.Visible = false;
            searchingLabel.Visible = true;
            searchResultsPanel.Hide();

            string searchQuery = searchTextbox.Text;

            if (string.IsNullOrEmpty(searchQuery) | searchQuery == "Input your search...")
            {
                logger.Debug("Search query was null, canceling");
                searchResultsPanel.Show();
                searchAlbumsButton.Visible = true;
                searchTracksButton.Visible = true;
                searchingLabel.Visible = false;
                return;
            }

            try
            {
                logger.Debug("Search for tracks started");
                await Task.Run(() => searchPanelHelper.SearchInitiate("tracks", app_id, searchQuery, user_auth_token));
            }
            catch (Exception ex)
            {
                logger.Error("Error occured during searchTracksButton_Click, error below:\r\n" + ex);
                searchResultsPanel.Show();
                searchAlbumsButton.Visible = true;
                searchTracksButton.Visible = true;
                searchingLabel.Visible = false;
                return;
            }
            logger.Debug("Search completed!");
            searchResultsPanel.Show();
            searchAlbumsButton.Visible = true;
            searchTracksButton.Visible = true;
            searchingLabel.Visible = false;
            return;
        }
    }
    public class Logger
    {
        private readonly string _filePath;

        public Logger(string filePath)
        {
            _filePath = filePath;
        }

        public void Log(string message, string level)
        {
            var logMessage = $"[{DateTime.Now}] [{level}] {message}";

            try
            {
                using (var writer = File.AppendText(_filePath))
                {
                    writer.WriteLine(logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Somehow, the log failed to write, lol");
                Console.WriteLine(logMessage);
                Console.WriteLine(ex);
            }
        }

        public void Debug(string message)
        {
            Log(message, "DEBUG");
        }

        public void Info(string message)
        {
            Log(message, "INFO");
        }

        public void Warning(string message)
        {
            Log(message, "WARNING");
        }

        public void Error(string message)
        {
            Log(message, "ERROR");
        }
    }
}
