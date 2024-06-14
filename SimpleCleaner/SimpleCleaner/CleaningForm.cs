using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security;
using System.Security.Permissions;


namespace SimpleCleaner
{
    public partial class CleaningForm : Form
    {
        ///FOR RECYCLE BIN CLEANING:///
        enum RecycleFlags : uint
        {
            SHRB_NOCONFIRMATION = 0x00000001, // Don't ask confirmation
            SHRB_NOPROGRESSUI = 0x00000002, // Don't show any windows dialog
            SHRB_NOSOUND = 0x00000004 // Don't make sound, ninja mode enabled :v
        }
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);
        ///////////////////////////////

        Logger logger = new Logger("CFLog.txt");

        public CleaningForm()
        {
            InitializeComponent();
            //logger.Log("[CleaningForm] was loaded successfully.");
        }

        //GOTO ANOTHER WINDOWS:
        private void GoToDuplicatesButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Find duplicates] button was pressed.");
            this.Visible = false;
            DuplicateForm newForm = new DuplicateForm();
            newForm.Show();
            logger.Log("[Find duplicates] button was worked successfully.");
        }

        private bool FAQFormOpened = false;
        private void FaqButton_Click(object sender, EventArgs e)
        {
            logger.Log("[FАQ] button was pressed.");
            if (!FAQFormOpened)
            {
                FAQForm newForm = new FAQForm();
                newForm.FormClosed += (s, args) => FAQFormOpened = false; // Set FAQFormOpened to false when the form is closed
                newForm.Show();
                FAQFormOpened = true;
                logger.Log("[FАQ] button worked successfully.");
            }
            else
            {
                logger.Log("[FАQ] button was already pressed once.");
            }
        }
        /// ///////////

        //DEFAULT OPTIONS:
        private async void SearchButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Search All] button was pressed.");
            ProgressBar.Value = 0;
            await SimulateLoadingAsync(1500);

            ResultGridView.Rows.Clear();
            ResultGridView.ColumnCount = 2;
            ResultGridView.Columns[0].Name = "Cache type:";
            ResultGridView.Columns[1].Name = "Cache Size (MB):";
            ResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            //ResultGridView.Rows.Add("->RECYCLE BIN:", "");
            DisplayRecycleBinSizeInfo();
            string tempFolderPath = Path.GetTempPath();
            //ResultGridView.Rows.Add("->TEMPORARY FILES:", "");
            DisplayTempFolderSizeInfo(tempFolderPath);
            //ResultGridView.Rows.Add("->BROWSER CACHE:", "");
            DisplayCacheInfo();

            double totalCacheSizeMB = CalculateTotalCacheSize();
            ResultGridView.Rows.Add("", "");
            ResultGridView.Rows.Add("Total Cache Size:", totalCacheSizeMB.ToString("0.##"));

            logger.Log("[Search All] button was worked successfully.");
        }
        private async void BackupButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Create backup] button was pressed.");
            if (ResultGridView.RowCount == 0)
            {
                MessageBox.Show("There is nothing to backup!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    string selectedFolderPath = folderBrowserDialog.SelectedPath;
                    MessageBox.Show($"Selected folder: {selectedFolderPath}", "Backup information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult backupTypeResult = MessageBox.Show("Do you want to create a backup as a text document?\n\n[YES] --> Backup ONLY files information\n[NO] --> Backup ALL files", "Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    logger.Log("Backup folder was chosen.");
                    ProgressBar.Value = 0;
                    await SimulateLoadingAsync(1000);

                    try
                    {
                        switch (backupTypeResult)
                        {
                            case DialogResult.Yes:
                                if (IsBrowserInstalled("Chrome"))
                                    BrowserCacheInfo(selectedFolderPath, BrowserType.Chrome);
                                if (IsBrowserInstalled("Firefox"))
                                    BrowserCacheInfo(selectedFolderPath, BrowserType.Firefox);
                                //if (IsBrowserInstalled("Edge"))
                                //    BackupBrowserCacheInfo(selectedFolderPath, BrowserType.Edge);
                                //if (IsBrowserInstalled("InternetExplorer"))
                                //    BackupBrowserCacheInfo(selectedFolderPath, BrowserType.InternetExplorer);
                                if (IsBrowserInstalled("Opera"))
                                    BrowserCacheInfo(selectedFolderPath, BrowserType.Opera);
                                RecycleBinInfo(selectedFolderPath);
                                TempFilesInfo(selectedFolderPath);
                                MessageBox.Show($"Information backup completed successfully!", "Backup Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                break;

                            case DialogResult.No:
                                if (IsBrowserInstalled("Chrome"))
                                    BackupBrowserCache(selectedFolderPath, BrowserType.Chrome);
                                if (IsBrowserInstalled("Firefox"))
                                    BackupBrowserCache(selectedFolderPath, BrowserType.Firefox);
                                //if (IsBrowserInstalled("Edge"))
                                //    BackupBrowserCache(selectedFolderPath, BrowserType.Edge);
                                //if (IsBrowserInstalled("InternetExplorer"))
                                //    BackupBrowserCache(selectedFolderPath, BrowserType.InternetExplorer);
                                  if (IsBrowserInstalled("Opera"))
                                    BackupBrowserCache(selectedFolderPath, BrowserType.Opera);
                                BackupRecycleBin(selectedFolderPath);
                                BackupTempFiles(selectedFolderPath);
                                MessageBox.Show($" Total backup completed successfully!", "Backup Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                break;

                            case DialogResult.Cancel:
                                this.Close();
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Backup failed: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            logger.Log("[Create backup] button was worked successfully.");
        }
        private async void ClearButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Clear!] button was pressed.");
            if (ResultGridView.RowCount == 0)
            {
                MessageBox.Show("There is nothing to clean!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Do you want to create a backup before cleaning?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                BackupButton_Click(null, EventArgs.Empty);
                ProgressBar.Value = 0;
                await SimulateLoadingAsync(2500);
                ClearBrowserCache();
                ClearTempFolder();
                EmptyRecycleBin();
            }
            if (result == DialogResult.No)
            {
                ProgressBar.Value = 0;
                await SimulateLoadingAsync(1100);
                ClearBrowserCache();
                ClearTempFolder();
                EmptyRecycleBin();
            }

            logger.Log("[Clear!] button was worked successfully.");
        }
        /// ///////////

        //CUSTOM OPTIONS:
        private async void RecycleBinButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Empty Recycle Bin] button was pressed.");
            ProgressBar.Value = 0;
            await SimulateLoadingAsync(1100);

            ResultGridView.Rows.Clear();
            ResultGridView.ColumnCount = 2;
            ResultGridView.Columns[0].Name = "Drive name:";
            ResultGridView.Columns[1].Name = "Recycle Bin Size (MB):";
            ResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            DisplayRecycleBinSizeInfo();
            EmptyRecycleBin();

            logger.Log("[Empty Recycle Bin] button was worked successfully.");
        }
        private async void TemporaryFilesButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Delete Temporary Files] button was pressed.");
            ProgressBar.Value = 0;
            await SimulateLoadingAsync(1500);

            ResultGridView.Rows.Clear();
            ResultGridView.ColumnCount = 2;
            ResultGridView.Columns[0].Name = "Temporary files:";
            ResultGridView.Columns[1].Name = "Size (MB):";
            ResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            string tempFolderPath = Path.GetTempPath();
            DisplayTempFolderSizeInfo(tempFolderPath);
            ClearTempFolder();

            logger.Log("[Delete Temporary Files] button was worked successfully.");
        }
        private async void BrowserCacheButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Delete browser Cache] button was pressed.");
            ProgressBar.Value = 0;
            await SimulateLoadingAsync(1500);

            ResultGridView.Rows.Clear();
            ResultGridView.ColumnCount = 2;
            ResultGridView.Columns[0].Name = "Browser name:";
            ResultGridView.Columns[1].Name = "Cache Size (MB):";
            ResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            DisplayCacheInfo();
            ClearBrowserCache();

            logger.Log("[Delete browser Cache] button was worked successfully.");
        }
        /// ///////////

        private async Task SimulateLoadingAsync(int durationMilliseconds)
        {
            const int updateInterval = 10;
            int numUpdates = durationMilliseconds / updateInterval;
            int percentIncrement = 10 / numUpdates;

            for (int i = 0; i < numUpdates; i++)
            {
                int percentComplete = (i + 1) * percentIncrement;
                ProgressBar.PerformStep();

                await Task.Delay(updateInterval);
            }
        }
        private bool IsBrowserInstalled(string browserName)
        {
            switch (browserName.ToLower())
            {
                case "chrome":
                    string chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                      @"Google\Chrome");
                    return Directory.Exists(chromePath);

                case "firefox":
                    string firefoxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                       @"Mozilla\Firefox");
                    return Directory.Exists(firefoxPath);

                case "edge":
                    string edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                    @"Microsoft\Edge");
                    return Directory.Exists(edgePath);

                case "internetexplorer":
                    string iePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                                 @"Internet Explorer");
                    return Directory.Exists(iePath);

                case "opera":
                    string operaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                    @"Opera Software\Opera Stable");
                    return Directory.Exists(operaPath);

                default:
                    logger.Log("[IsBrowserInstalled] function was called successfully.");
                    return false;

            }
        }

        //INFO OUTPUT:
        //BROWSERS:
        private void DisplayCacheInfo()
        {
            //ResultGridView.Rows.Clear();

            if (IsBrowserInstalled("Chrome"))
                DisplayCacheInfoForChrome();

            if (IsBrowserInstalled("Firefox"))
                DisplayCacheInfoForFirefox();

            if (IsBrowserInstalled("Edge"))
                DisplayCacheInfoForEdge();

            if (IsBrowserInstalled("InternetExplorer"))
                DisplayCacheInfoForIE();

            if (IsBrowserInstalled("Opera"))
                DisplayCacheInfoForOpera();

            logger.Log("[DisplayCacheInfo] function was called successfully.");
        }
        private void DisplayCacheInfoForAllBrowser(string browserName, string cachePath)
        {
            long cacheSizeBytes = GetFolderSize(cachePath);
            double cacheSizeMB = (double)cacheSizeBytes / (1024 * 1024);
            ResultGridView.Rows.Add(browserName, cacheSizeMB.ToString("0.##"));
            logger.Log("[DisplayCacheInfoForAllBrowser] function was called successfully.");
        }
        private void DisplayCacheInfoForChrome()
        {
            //ResultGridView.Rows.Clear();

            DisplayCacheInfoForAllBrowser("Google Chrome",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                + @"\Google\Chrome\User Data\Default\Cache");
            logger.Log("[DisplayCacheInfoForChrome] function was called successfully.");
        }
        private void DisplayCacheInfoForEdge()
        {
            string edgeCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                @"Microsoft\Edge\User Data\Default\Cache");
            long cacheSizeBytes = GetFolderSize(edgeCachePath);
            double cacheSizeMB = (double)cacheSizeBytes / (1024 * 1024);
            ResultGridView.Rows.Add("Microsoft Edge", cacheSizeMB.ToString("0.##"));
            logger.Log("[DisplayCacheInfoForEdge] function was called successfully.");
        }
        private void DisplayCacheInfoForIE()
        {
            string ieCachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
            long cacheSizeBytes = GetFolderSize(ieCachePath);
            double cacheSizeMB = (double)cacheSizeBytes / (1024 * 1024);
            ResultGridView.Rows.Add("Internet Explorer", cacheSizeMB.ToString("0.##"));
            logger.Log("[DisplayCacheInfoForIE] function was called successfully.");
        }
        private void DisplayCacheInfoForFirefox()
        {
            string firefoxCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                   @"Mozilla\Firefox\Profiles");
            string[] profiles = Directory.GetDirectories(firefoxCachePath, "*.default*");

            foreach (string profile in profiles)
            {
                string cachePath = Path.Combine(profile, "cache2");
                long cacheSizeBytes = GetFolderSize(cachePath);
                double cacheSizeMB = (double)cacheSizeBytes / (1024 * 1024);
                string profileName = Path.GetFileName(profile);
                ResultGridView.Rows.Add($"Firefox ({profileName})", cacheSizeMB.ToString("0.##"));
            }
            logger.Log("[DisplayCacheInfoForFirefox] function was called successfully.");
        }
        private void DisplayCacheInfoForOpera()
        {
            string operaCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                  @"Opera Software\Opera Stable\Cache");
            long cacheSizeBytes = GetFolderSize(operaCachePath);
            double cacheSizeMB = (double)cacheSizeBytes / (1024 * 1024);
            ResultGridView.Rows.Add("Opera", cacheSizeMB.ToString("0.##"));
            logger.Log("[DisplayCacheInfoForOpera] function was called successfully.");
        }
        //RECYCLE BIN:
        private void DisplayRecycleBinSizeInfo()
        {
            //ResultGridView.Rows.Clear();

            try
            {

                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    if (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                    {
                        string recycleBinPath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                        long recycleBinSizeBytes = GetDirectorySize(recycleBinPath);
                        double recycleBinSizeMB = (double)recycleBinSizeBytes / (1024 * 1024);
                        ResultGridView.Rows.Add(drive.Name, recycleBinSizeMB.ToString("0.##"));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error retrieving Recycle Bin size information: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            logger.Log("[DisplayRecycleBinSizeInfo] function was called successfully.");
        }
        //TEMPORARY FILES:
        private void DisplayTempFolderSizeInfo(string tempFolderPath)
        {
            //ResultGridView.Rows.Clear();
            try
            {
                string[] tempFiles = Directory.GetFiles(tempFolderPath);
                long totalSizeBytes = 0;

                foreach (string tempFile in tempFiles)
                {
                    FileInfo fileInfo = new FileInfo(tempFile);
                    totalSizeBytes += fileInfo.Length;
                }

                double totalSizeMB = (double)totalSizeBytes / (1024 * 1024);
                ResultGridView.Rows.Add(tempFolderPath, totalSizeMB.ToString("0.##"));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error retrieving temporary files size information: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            logger.Log("[DisplayTempFolderSizeInfo] function was called successfully.");
        }
        private double CalculateTotalCacheSize()
        {
            double totalSizeMB = 0.0;

            foreach (DataGridViewRow row in ResultGridView.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    string cacheType = row.Cells[0].Value.ToString();
                    string sizeString = row.Cells[1].Value.ToString();

                    if (double.TryParse(sizeString.Replace(" MB", ""), out double sizeMB))
                    {
                        totalSizeMB += sizeMB;
                    }
                }
            }

            logger.Log("[CalculateTotalCacheSize] function was called successfully.");
            return totalSizeMB;
        }
        /// ///////////

        //CLEANING:
        private void ClearBrowserCache()
        {
            if (MessageBox.Show("Are you sure you want to clear all browser caches?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (IsBrowserInstalled("Chrome"))
                {
                    ClearChromeCache();
                }

                if (IsBrowserInstalled("Firefox"))
                {
                    ClearFirefoxCache();
                }

                if (IsBrowserInstalled("Edge"))
                {
                    ClearEdgeCache();
                }

                if (IsBrowserInstalled("InternetExplorer"))
                {
                    ClearIECache();
                }

                if (IsBrowserInstalled("Opera"))
                {
                    ClearOperaCache();
                }
                logger.Log("[ClearBrowserCache] function was called successfully.");

                MessageBox.Show("Browser caches have been cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DisplayCacheInfo();
            }
        }
        private void ClearCacheFolder(string folderPath)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(folderPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing cache: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            logger.Log("[ClearCacheFolder] function was called successfully.");
        }
        private void ClearChromeCache()
        {
            string chromeCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                  @"Google\Chrome\User Data\Default\Cache");
            ClearCacheFolder(chromeCachePath);
            logger.Log("[ClearChromeCache] function was called successfully.");
        }
        private void ClearFirefoxCache()
        {
            string firefoxCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                   @"Mozilla\Firefox\Profiles");
            string[] profiles = Directory.GetDirectories(firefoxCachePath, "*.default*");

            foreach (string profile in profiles)
            {
                string cachePath = Path.Combine(profile, "cache2");
                ClearCacheFolder(cachePath);
            }

            logger.Log("[ClearFirefoxCache] function was called successfully.");
        }
        private void ClearEdgeCache()
        {
            string edgeCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                @"Microsoft\Edge\User Data\Default\Cache");
            ClearCacheFolder(edgeCachePath);

            logger.Log("[ClearEdgeCache] function was called successfully.");
        }
        private void ClearIECache()
        {
            string ieCachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
            ClearCacheFolder(ieCachePath);

            logger.Log("[ClearIECache] function was called successfully.");
        }
        private void ClearOperaCache()
        {
            string operaCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                  @"Opera Software\Opera Stable\Cache");
            ClearCacheFolder(operaCachePath);
            logger.Log("[ClearOperaCache] function was called successfully.");
        }

        private void EmptyRecycleBin()
        {
            DialogResult result = MessageBox.Show("Are you sure you want to delete all the items in the Recycle Bin?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // If accepted, continue with the cleaning
            if (result == DialogResult.Yes)
            {
                try
                {
                    // Execute the method with the required parameters
                    uint isSuccess = SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHRB_NOCONFIRMATION);

                    if (isSuccess == 0)
                    {
                        MessageBox.Show("The Recycle Bin has been successfully emptied!", "Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to empty the Recycle Bin.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions
                    MessageBox.Show("The Recycle Bin couldn't be emptied: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            logger.Log("[EmptyRecycleBin] function was called successfully.");
        }
        private void ClearTempFolder()
        {
            DialogResult result = MessageBox.Show("Are you sure you want to delete all temporary files?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                try
                {

                    string tempFolderPath = Path.GetTempPath();
                    string[] tempFiles = Directory.GetFiles(tempFolderPath);


                    foreach (string tempFile in tempFiles)
                    {
                        File.Delete(tempFile);
                    }

                    MessageBox.Show("Temporary files have been cleaned up successfully.", "Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error cleaning up temporary files: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            logger.Log("[ClearTempFolder] function was called successfully.");
        }
        /// ///////////

        private long GetFolderSize(string folderPath)
        {
            long size = 0;

            try
            {
                DirectoryInfo di = new DirectoryInfo(folderPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    size += file.Length;
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    size += GetFolderSize(dir.FullName);
                }
            }
            catch (Exception)
            {
                // Ignore exceptions for folder size calculation
            }


            logger.Log("[GetFolderSize] function was called successfully.");
            return size;
        }
        private long GetDirectorySize(string directoryPath)
        {
            long directorySize = 0;

            try
            {

                DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
                foreach (FileInfo fileInfo in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    directorySize += fileInfo.Length;
                }
            }
            catch (Exception)
            {
                // Ignore exceptions for folder size calculation
            }

            logger.Log("[GetDirectorySize] function was called successfully.");
            return directorySize;
        }
        private void CopyDirectory(string sourceDirPath, string destDirPath)
        {

            if (!Directory.Exists(destDirPath))
            {
                Directory.CreateDirectory(destDirPath);
            }

            foreach (string filePath in Directory.GetFiles(sourceDirPath))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDirPath, fileName);
                File.Copy(filePath, destFilePath, true);
            }

            foreach (string subDirPath in Directory.GetDirectories(sourceDirPath))
            {
                string subDirName = new DirectoryInfo(subDirPath).Name;
                string destSubDirPath = Path.Combine(destDirPath, subDirName);
                CopyDirectory(subDirPath, destSubDirPath);
                logger.Log("[CopyDirectory] function was called successfully.");
            }

            logger.Log("[CopyDirectory] function was called successfully.");
        }

        //MAKING BACKUPS:
        private enum BrowserType
        {
            Chrome,
            Firefox,
            Edge,
            InternetExplorer,
            Opera
        }
        private void BackupBrowserCache(string backupFolderPath, BrowserType browserType)
        {
            string cachePath = "";

            switch (browserType)
            {
                case BrowserType.Chrome:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data\Default\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Firefox:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Mozilla\Firefox\Profiles";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Edge:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Edge\User Data\Default\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.InternetExplorer:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Opera:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Opera Software\Opera Stable\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(cachePath))
            {
                string destinationPath = Path.Combine(backupFolderPath, browserType.ToString() + "Cache");
                CopyDirectory(cachePath, destinationPath);
                logger.Log("[CopyDirectory] function was called successfully.");
            }

            logger.Log("[BackupBrowserCache] function was called successfully.");
        }
        private void BackupTempFiles(string backupFolderPath)
        {
            string tempFolderPath = Path.GetTempPath();
            string tempBackupPath = Path.Combine(backupFolderPath, "TempFiles");

            CopyDirectory(tempFolderPath, tempBackupPath);
            logger.Log("[CopyDirectory] function was called successfully.");
            logger.Log("[BackupTempFiles] function was called successfully.");
        }
        private void BackupRecycleBin(string backupFolderPath)
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                {
                    string recycleBinPath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                    string backupRecycleBinPath = Path.Combine(backupFolderPath, "RecycleBin");
                    CopyDirectory(recycleBinPath, backupRecycleBinPath);
                }
            }
            logger.Log("[BackupRecycleBin] function was called successfully.");
        }
        ///////////////////

        //MAKING TXT INFO OF FILES:
        private void BrowserCacheInfo(string backupFolderPath, BrowserType browserType)
        {
            string cachePath = "";

            switch (browserType)
            {
                case BrowserType.Chrome:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data\Default\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Firefox:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Mozilla\Firefox\Profiles";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Edge:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Edge\User Data\Default\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.InternetExplorer:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                case BrowserType.Opera:
                    cachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Opera Software\Opera Stable\Cache";
                    logger.Log("[GetFolderPath] function was called successfully.");
                    break;
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(cachePath))
            {
                string cacheInfoFilePath = Path.Combine(backupFolderPath, browserType.ToString() + "CacheInfo.txt");
                WriteFileInfoToText(cachePath, cacheInfoFilePath);
                logger.Log("[WriteFileInfoToText] function was called successfully.");
            }

            logger.Log("[BrowserCacheInfo] function was called successfully.");
        }
        private void TempFilesInfo(string backupFolderPath)
        {
            string tempFolderPath = Path.GetTempPath();
            string tempInfoFilePath = Path.Combine(backupFolderPath, "TempFilesInfo.txt");

            WriteFileInfoToText(tempFolderPath, tempInfoFilePath);
            logger.Log("[WriteFileInfoToText] function was called successfully.");

            logger.Log("[TempFilesInfo] function was called successfully.");
        }
        private void RecycleBinInfo(string backupFolderPath)
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                {
                    string recycleBinPath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                    string recycleBinInfoFilePath = Path.Combine(backupFolderPath, "RecycleBinInfo.txt");
                    WriteFileInfoToText(recycleBinPath, recycleBinInfoFilePath);
                }
            }

            logger.Log("[RecycleBinInfo] function was called successfully.");
        }
        private void WriteFileInfoToText(string directoryPath, string outputFilePath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(outputFilePath))
                {
                    writer.WriteLine($"Files in directory: {directoryPath}");
                    WriteFilesInDirectoryToText(directoryPath, writer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write file info to {outputFilePath}: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            logger.Log("[WriteFileInfoToText] function was called successfully.");
        }
        private void WriteFilesInDirectoryToText(string directoryPath, StreamWriter writer)
        {
            foreach (string file in Directory.GetFiles(directoryPath))
            {
                FileInfo fileInfo = new FileInfo(file);
                writer.WriteLine($"Name: |{fileInfo.Name}| Size: |{fileInfo.Length}| bytes");
            }

            foreach (string subDirectory in Directory.GetDirectories(directoryPath))
            {
                WriteFilesInDirectoryToText(subDirectory, writer);
            }

            logger.Log("[WriteFilesInDirectoryToText] function was called successfully.");
        }
        ///////////////////
    }
}