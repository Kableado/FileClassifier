using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace FileClassifier
{
    public partial class FrmFileClassifier : Form
    {
        public string InitialPath
        {
            get { return txtPath.Text; }
            set { txtPath.Text = value; }
        }

        public FrmFileClassifier()
        {
            InitializeComponent();
            txtPath.Text = Directory.GetCurrentDirectory();
            Buttons_ProcessEnabled(false);
        }

        public static void BuildPath(string path)
        {
            List<string> splittedPath = new List<string>();

            while (Directory.Exists(path) == false)
            {
                {
                    splittedPath.Insert(0, path);
                    DirectoryInfo dir = Directory.GetParent(path);
                    path = dir.FullName;
                }
            }
            foreach (string pathAux in splittedPath)
            {
                Directory.CreateDirectory(pathAux);
            }
        }

        #region CreationDate

        public static DateTime? GetNormalizedFileDateTime(string filePath)
        {
            string format = "yyyyMMdd-HHmmss";
            string filename = Path.GetFileNameWithoutExtension(filePath);
            if (filename.Length < format.Length) { return null; }
            string strDate = filename.Substring(0, format.Length);
            DateTime dt;
            if (DateTime.TryParseExact(strDate, format, null, DateTimeStyles.None, out dt) == false) { return null; }
            return dt;
        }

        /// <summary>
        /// Returns the EXIF Image Data of the Date Taken.
        /// </summary>
        /// <param name="getImage">Image (If based on a file use Image.FromFile(f);)</param>
        /// <returns>Date Taken or Null if Unavailable</returns>
        public static DateTime? ExifDateTaken(Image getImage)
        {
            int DateTakenValue = 0x9003; //36867;

            if (!getImage.PropertyIdList.Contains(DateTakenValue))
                return null;

            string dateTakenTag = System.Text.Encoding.ASCII.GetString(getImage.GetPropertyItem(DateTakenValue).Value);
            string[] parts = dateTakenTag.Split(':', ' ');
            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);
            int hour = int.Parse(parts[3]);
            int minute = int.Parse(parts[4]);
            int second = int.Parse(parts[5]);

            return new DateTime(year, month, day, hour, minute, second);
        }

        public static DateTime GetFileCreationDate(string filePath)
        {
            DateTime dtFile;

            DateTime? dtNormalizedFile = GetNormalizedFileDateTime(filePath);
            if (dtNormalizedFile != null)
            {
                return dtNormalizedFile.Value;
            }

            DateTime dtCretation = File.GetCreationTime(filePath);
            DateTime dtLastMod = File.GetLastWriteTime(filePath);
            dtFile = (dtCretation < dtLastMod) ? dtCretation : dtLastMod;

            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif")
            {
                Image img = Image.FromFile(filePath);
                DateTime? dtTaken = ExifDateTaken(img);
                if (dtTaken != null)
                {
                    dtFile = (DateTime)dtTaken;
                }
                img.Dispose();
            }

            return dtFile;
        }

        #endregion

        #region Processing

        private bool _running = false;

        public class FileDesc
        {
            public string Path { get; set; }

            private string _fileName = null;
            private string _fileNameLower = null;

            public string FileName
            {
                get { return _fileName; }
                set { _fileName = value; _fileNameLower = _fileName.ToLower(); }
            }

            public string FileNameLower
            {
                get { return _fileNameLower; }
            }

            public string Extension { get; set; }
            public DateTime Date { get; set; }
        }

        public class FileCluster
        {
            public DateTime DateStart { get; set; }
            public DateTime DateEnd { get; set; }
            public string DirName { get; set; }

            private List<FileDesc> _files = new List<FileDesc>();
            public List<FileDesc> Files { get { return _files; } }
        }

        private DateTime lastMessage;

        private void ScanFile(string filePath, List<FileDesc> files)
        {
            string fileName = Path.GetFileName(filePath);
            DateTime dtFile = GetFileCreationDate(filePath);
            string ext = Path.GetExtension(filePath).ToLower();
            string fileDirPath = Path.GetDirectoryName(filePath);

            var fileDesc = new FileDesc
            {
                Path = filePath,
                FileName = fileName,
                Extension = ext,
                Date = dtFile,
            };
            files.Add(fileDesc);

            DateTime dtNow = DateTime.UtcNow;
            if (dtNow > lastMessage.AddSeconds(1))
            {
                lsbOutput.AddLine(string.Format("Detected {0} files", files.Count));
                lastMessage = dtNow;
            }
        }

        private void ScanDirectory(string path, List<FileDesc> files)
        {
            string[] filePaths = Directory.GetFiles(path);

            foreach (string filePath in filePaths)
            {
                if (!_running) { break; }
                ScanFile(filePath, files);
            }

            string[] directoryPaths = Directory.GetDirectories(path);
            foreach (string directoryPath in directoryPaths)
            {
                if (!_running) { break; }
                ScanDirectory(directoryPath, files);
            }
        }

        private void Process(bool doMove)
        {
            string path = txtPath.Text;
            if (!Directory.Exists(path))
            {
                MessageBox.Show("El directorio no existe");
                return;
            }

            lsbOutput.Clean();
            lastMessage = DateTime.UtcNow;
            List<FileDesc> files = new List<FileDesc>();
            ScanDirectory(path, files);
            if (_running == false) { return; }

            lsbOutput.AddLine(string.Format("Detected total {0} files", files.Count));

            // Duplicated files
            List<List<FileDesc>> duplicated = new List<List<FileDesc>>();
            for(int i = 0; i< (files.Count - 1); i++)
            {
                List<FileDesc> currentDups = null;
                for (int j = (i+1); j < files.Count; j++)
                {
                    if (files[i].FileNameLower == files[j].FileNameLower)
                    {
                        if (currentDups == null)
                        {
                            currentDups = new List<FileDesc>();
                            currentDups.Add(files[i]);
                        }
                        currentDups.Add(files[j]);
                    }
                }
                if(currentDups != null) { duplicated.Add(currentDups); }
            }
            if (duplicated.Count > 0)
            {
                foreach (List<FileDesc> dups in duplicated)
                {
                    lsbOutput.AddLine(string.Format("Duplicated file: {0}", dups[0].FileName));
                    foreach (FileDesc file in dups)
                    {
                        lsbOutput.AddLine(string.Format("         Path: {0}", file.Path), file.Path);
                    }
                }
            }
            else
            {
                lsbOutput.AddLine("No duplicates found");
            }

            List<string> imageExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".bpm", ".gif", ".tga", ".webp", };
            List<string> movieExtensions = new List<string> { ".avi", ".mkv", ".mp4", ".mov", ".webm", ".flv", };
            List<FileDesc> mediaFiles = files
                .Where(fd => imageExtensions.Contains(fd.Extension) || movieExtensions.Contains(fd.Extension))
                .OrderBy(fd => fd.Date)
                .ToList();
            lsbOutput.AddLine(string.Format("Detected {0} media", mediaFiles.Count));

            List<FileDesc> noMediaFiles = files
               .Where(fd => imageExtensions.Contains(fd.Extension) == false && movieExtensions.Contains(fd.Extension) == false)
                .OrderBy(fd => fd.Date)
                .ToList();
            lsbOutput.AddLine(string.Format("Detected {0} no media", noMediaFiles.Count));

            // Find clusters
            int maxHoursDiff = 15;
            List<FileCluster> clusters = new List<FileCluster>();
            FileCluster currentCluster = new FileCluster();
            clusters.Add(currentCluster);
            FileDesc previousFile = null;
            foreach (FileDesc currentFile in mediaFiles)
            {
                if (previousFile == null)
                {
                    currentCluster.Files.Add(currentFile);
                    previousFile = currentFile;
                    continue;
                }

                TimeSpan tsDiff = currentFile.Date - previousFile.Date;
                if (tsDiff.TotalHours > maxHoursDiff)
                {
                    // New cluster
                    currentCluster = new FileCluster();
                    clusters.Add(currentCluster);
                    currentCluster.Files.Add(currentFile);
                    previousFile = currentFile;
                    continue;
                }

                // Add to current cluster
                currentCluster.Files.Add(currentFile);
                previousFile = currentFile;
            }
            foreach (FileCluster cluster in clusters)
            {
                if (cluster.Files.Count == 0) { continue; }
                cluster.DateStart = cluster.Files.Min(fd => fd.Date);
                cluster.DateEnd = cluster.Files.Max(fd => fd.Date);
                TimeSpan tsCluster = cluster.DateEnd - cluster.DateStart;
                if (tsCluster.TotalDays > 1)
                {
                    cluster.DirName = cluster.DateStart.ToString("yyyy-MM");
                }
                else
                {
                    cluster.DirName = cluster.DateStart.ToString("yyyy-MM-dd");
                }
            }
            foreach (FileCluster cluster in clusters)
            {
                if (cluster.Files.Count == 0) { continue; }
                string clusterPath = Path.Combine(path, cluster.DirName);
                lsbOutput.AddLine(string.Format("Cluster: {0}", clusterPath));
                if (doMove)
                {
                    BuildPath(clusterPath);
                }
                foreach (FileDesc file in cluster.Files)
                {
                    lsbOutput.AddLine(string.Format("         Path: {0} ", file.Path), file.Path);
                    if (doMove)
                    {
                        string newFilePath = Path.Combine(clusterPath, file.FileName);
                        File.Move(file.Path, newFilePath);
                    }
                }
            }


            lsbOutput.AddLine("################ Finish ################");
        }

        public void Buttons_ProcessEnabled(bool running)
        {
            btnCancel.Enabled = running;
            btnTest.Enabled = (running == false);
            btnGo.Enabled = (running == false);
        }

        private void Run(bool doMove)
        {
            if (_running) { return; }
            _running = true;
            Buttons_ProcessEnabled(true);

            Thread thread = new Thread(() => { Process(doMove); });
            thread.Start();

            while (thread.IsAlive)
            {
                Application.DoEvents();
                Thread.Sleep(500);
            }

            if (_running == false)
            {
                lsbOutput.AddLine("################ Cancel ################");
                Buttons_ProcessEnabled(false);
                return;
            }
            _running = false;
            Buttons_ProcessEnabled(false);
        }

        private void Stop()
        {
            _running = false;
        }

        #endregion Processing

        #region UI events

        private void btnTest_Click(object sender, EventArgs e)
        {
            Run(false);
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            Run(true);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Stop();
        }

        #endregion UI events

        private void lsbOutput_DoubleClick(object sender, EventArgs e)
        {
            string strFile = lsbOutput.GetCurrentData() as string;
            if (string.IsNullOrEmpty(strFile)) { return; }
            System.Diagnostics.Process.Start(strFile);
        }
    }
}
