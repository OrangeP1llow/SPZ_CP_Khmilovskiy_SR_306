using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

namespace SimpleCleaner
{
    public partial class DuplicateForm : Form
    {
        private string selectedFolderPath;
        private List<string> duplicateFilePaths = new List<string>();

        Logger logger = new Logger("DFLog.txt");

        public DuplicateForm()
        {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(FormClosingFunction);
            //logger.Log("[DuplicateForm] was loaded successfully.");
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Go Back] button was pressed.");
            this.Visible = false;
            CleaningForm newForm = new CleaningForm();
            newForm.Show();
            logger.Log("[Go Back] button was worked successfully.");
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

        private void ChooseDirectoryButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Choose directory] button was pressed.");
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    selectedFolderPath = folderBrowserDialog.SelectedPath;
                    logger.Log("Directory was chosen.");
                    MessageBox.Show($"Selected folder: {selectedFolderPath}", "Directory information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DirectoryPathInfo.Text = selectedFolderPath;
                }
            }
            logger.Log("[Choose directory] button was worked successfully.");
        }
        private async void ChooseDriveButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Check for file types] button was pressed.");
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    selectedFolderPath = folderBrowserDialog.SelectedPath;
                    logger.Log("Directory was chosen.");
                    MessageBox.Show($"Selected folder: {selectedFolderPath}", "Directory information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DrivePathInfo.Text = selectedFolderPath;

                    DFProgressBar.Value = 0;
                    await SimulateLoadingAsync(300);

                    DFResultGridView.Rows.Clear();
                    DFResultGridView.ColumnCount = 3;
                    DFResultGridView.Columns[0].Name = "File type:";
                    DFResultGridView.Columns[1].Name = "Files count:";
                    DFResultGridView.Columns[2].Name = "Files total size (MB):";
                    DFResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                    Dictionary<string, (int count, double totalSize)> fileCountByCategory = GetFileCountByCategory(selectedFolderPath);
                    logger.Log("[GetCategoryByExtension] function was called successfully.");
                    DisplayResultsInDataGridView(fileCountByCategory);

                    DFResultGridView.Rows.Add("", "", "");
                    double totalSize = CalculateTotalFiles();
                    DFResultGridView.Rows.Add("", "Amount of files:", totalSize.ToString());
                }
            }

            logger.Log("[Check for file types] button was worked successfully.");
        }
        private async void SearchButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Search] button was pressed.");
            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                MessageBox.Show("Choose directory at first!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DFProgressBar.Value = 0;
            await SimulateLoadingAsync(400);

            DFResultGridView.Rows.Clear();
            DFResultGridView.ColumnCount = 3;
            DFResultGridView.Columns[0].Name = "File name:";
            DFResultGridView.Columns[1].Name = "File extension:";
            DFResultGridView.Columns[2].Name = "File size (MB):";
            DFResultGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            
            SearchForDuplicates(selectedFolderPath);
            DisplayDuplicates(duplicateFilePaths);
            int totalDuplicates = (DFResultGridView.RowCount - 1);
            DFResultGridView.Rows.Add("", "", "");
            DFResultGridView.Rows.Add("", "Amount of duplicates:", totalDuplicates);

            logger.Log("[Search] button was worked successfully.");
        }
        private async void ClearButton_Click(object sender, EventArgs e)
        {
            logger.Log("[Clear!] button was pressed.");
            if (DFResultGridView.RowCount == 0 || DFResultGridView.RowCount <= 3)
            {
                MessageBox.Show("There is nothing to clean!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (CheckForAccessibility(DFResultGridView))
            {
                return;
            }

            DialogResult result = MessageBox.Show("Do you want to backup duplicates before cleaning?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                BackupDuplicateFiles(duplicateFilePaths);
                DFProgressBar.Value = 0;
                await SimulateLoadingAsync(350);
                ClearDuplicateFiles(duplicateFilePaths);
                int totalDuplicates = (DFResultGridView.RowCount - 1);
                DFResultGridView.Rows.Add("", "", "");
                DFResultGridView.Rows.Add("", "Amount of duplicates:", totalDuplicates);
            }
            if (result == DialogResult.No)
            {
                DFProgressBar.Value = 0;
                await SimulateLoadingAsync(350);
                ClearDuplicateFiles(duplicateFilePaths);
                int totalDuplicates = (DFResultGridView.RowCount - 1);
                DFResultGridView.Rows.Add("", "", "");
                DFResultGridView.Rows.Add("", "Amount of duplicates:", totalDuplicates);
            }

            logger.Log("[Clear!] button was worked successfully.");
        }

        private async Task SimulateLoadingAsync(int durationMilliseconds)
        {
            const int updateInterval = 10;
            int numUpdates = durationMilliseconds / updateInterval;
            int percentIncrement = 10 / numUpdates;

            for (int i = 0; i < numUpdates; i++)
            {
                int percentComplete = (i + 1) * percentIncrement;
                DFProgressBar.PerformStep();

                await Task.Delay(updateInterval);
            }
        }

        //WORK WITH DUPLICATES:
        private void SearchForDuplicates(string directory)
        {
            Dictionary<string, List<string>> filesByHash = new Dictionary<string, List<string>>();
            duplicateFilePaths.Clear();

            foreach (string filePath in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        string fileHash = GetFileHash(stream);

                        if (!filesByHash.ContainsKey(fileHash))
                        {
                            filesByHash[fileHash] = new List<string>();
                        }
                        else
                        {
                            duplicateFilePaths.Add(filePath);
                        }

                        filesByHash[fileHash].Add(filePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Scanning failed: {ex.Message}", "Scanning Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            logger.Log("[SearchForDuplicates] function was called successfully.");
        }
        private string GetFileHash(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(stream);
                logger.Log("[GetFileHash] function was called successfully.");
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        private void DisplayDuplicates(List<string> duplicateFilePaths)
        {
            DFResultGridView.Rows.Clear();

            foreach (string filePath in duplicateFilePaths)
            {
                string fileName = Path.GetFileName(filePath);
                long fileSizeBytes = new FileInfo(filePath).Length;
                double fileSizeMB = Math.Round((double)fileSizeBytes / (1024 * 1024), 2);
                string fileExtension = Path.GetExtension(filePath);
                DFResultGridView.Rows.Add(fileName, fileExtension, fileSizeMB);
            }

            logger.Log("[DisplayDuplicates] function was called successfully.");
        }
        /// ////////////////

        //WORK WITH FILE TYPES:
        private Dictionary<string, (int count, double totalSize)> GetFileCountByCategory(string diskPath)
        {
            Dictionary<string, (int count, double totalSize)> fileCountAndSizeByCategory = new Dictionary<string, (int, double)>();

            try
            {
                string[] allFiles = Directory.GetFiles(diskPath, "*.*", SearchOption.AllDirectories);

                foreach (string filePath in allFiles)
                {
                    try
                    {
                        string extension = Path.GetExtension(filePath).ToLower();
                        string category = GetCategoryByExtension(extension);
                        long fileSizeInBytes = new FileInfo(filePath).Length;
                        double fileSizeInMegabytes = fileSizeInBytes / (1024.0 * 1024.0);

                        if (fileCountAndSizeByCategory.ContainsKey(category))
                        {
                            var (count, totalSize) = fileCountAndSizeByCategory[category];
                            fileCountAndSizeByCategory[category] = (count + 1, totalSize + fileSizeInMegabytes);
                        }
                        else
                        {
                            fileCountAndSizeByCategory[category] = (1, fileSizeInMegabytes);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore exceptions for folder size calculation
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            logger.Log("[GetFileCountByCategory] function was called successfully.");
            return fileCountAndSizeByCategory;
        }
        private string GetCategoryByExtension(string extension)
        {
            switch (extension)
            {
                case ".mp3":
                case ".wav":
                case ".flac":
                case ".aac":
                case ".m4a":
                case ".ogg":
                case ".wma":
                case ".aiff":
                    return "Music";
                case ".jpg":
                case ".jpeg":
                case ".jfif":
                case ".pjpeg":
                case ".pjp":
                case ".png":
                case ".gif":
                case ".apng":
                case ".avif":
                case ".svg":
                case ".webp":
                case ".bmp":
                case ".ico":
                case ".cur":
                case ".tif":
                case ".tiff":
                    return "Photos";
                case ".txt":
                case ".doc":
                case ".docx":
                case ".html":
                case ".htm":
                case ".odt":
                case ".pdf":
                case ".xls":
                case ".xlsx":
                case ".ods":
                case ".ppt":
                case ".pptx":
                    return "Documents";
                case ".mp4":
                case ".avi":
                case ".mov":
                case ".wmv":
                case ".mkv":
                case ".flv":
                case ".mpeg":
                case ".m4v":
                case ".webm":
                    return "Videos";
                case ".zip":
                case ".rar":
                case ".7z":
                case ".tar":
                case ".gz":
                case ".bz2":
                case ".xz":
                case ".tgz":
                case ".tar.gz":
                case ".tar.bz2":
                case ".tar.xz":
                    return "Archived";
                default:
                    return "Other";
            }
        }
        private void DisplayResultsInDataGridView(Dictionary<string, (int count, double totalSize)> results)
        {
            DFResultGridView.Rows.Clear();

            foreach (var pair in results)
            {
                DFResultGridView.Rows.Add(pair.Key, pair.Value.count, pair.Value.totalSize.ToString("0.##"));
            }

            logger.Log("[DisplayResultsInDataGridView] function was called successfully.");
        }
        private double CalculateTotalFiles()
        {
            int totalFilesAmount = 0;

            foreach (DataGridViewRow row in DFResultGridView.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    string cacheType = row.Cells[0].Value.ToString();
                    string sizeString = row.Cells[1].Value.ToString();

                    if (int.TryParse(sizeString.Replace(" MB", ""), out int sizeMB))
                    {
                        totalFilesAmount += sizeMB;
                    }
                }
            }

            logger.Log("[CalculateTotalFiles] function was called successfully.");
            return totalFilesAmount;
        }
        /// ////////////////

        //CLEANING:
        private void ClearDuplicateFiles(List<string> duplicateFilePaths)
        {
            foreach (string filePath in duplicateFilePaths)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete file {filePath}: {ex.Message}", "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            MessageBox.Show("Duplicates have been cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            duplicateFilePaths.Clear();
            DisplayDuplicates(duplicateFilePaths);

            logger.Log("[ClearDuplicateFiles] function was called successfully.");
        }
        private bool CheckForAccessibility(DataGridView dataGridView)
        {
            bool column0Exists = false;
            bool column1Exists = false;
            bool column2Exists = false;

            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (column.Name == "File type:")
                {
                    column0Exists = true;
                }
                else if (column.Name == "Files count:")
                {
                    column1Exists = true;
                }
                else if (column.Name == "Files total size (MB):")
                {
                    column2Exists = true;
                }
            }

            if (column0Exists || column1Exists || column2Exists)
            {
                MessageBox.Show("You can't clean file types!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true;
            }

            logger.Log("[CheckForAccessibility] function was called successfully.");
            return false;
        }
        /// ////////////////

        //MAKING BACKUPS:
        private void BackupDuplicateFiles(List<string> duplicateFilePaths)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    string backupDirectory = folderBrowserDialog.SelectedPath;

                    try
                    {
                        //backupDirectory = Path.Combine(backupDirectory, "DuplicatesBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                        Directory.CreateDirectory(backupDirectory);

                        // Копіюємо кожен файл-дублікат у директорію бекапу
                        foreach (string filePath in duplicateFilePaths)
                        {
                            string fileName = Path.GetFileName(filePath);
                            string destFilePath = Path.Combine(backupDirectory, fileName);
                            File.Copy(filePath, destFilePath, true);
                        }

                        MessageBox.Show($"Backup completed successfully!", "Backup Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Backup failed: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            logger.Log("[BackupDuplicateFiles] function was called successfully.");
        }
        /// ////////////////

        //FORM CLOSER:
        private void FormClosingFunction(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                Application.Exit();
            }
        }
    }
}