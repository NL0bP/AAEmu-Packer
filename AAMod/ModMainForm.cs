﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using AAPakEditor;

namespace AAMod
{
    public partial class ModMainForm : Form
    {
        static string appVerDate = "V20190518";
        static string GamePakFileName = "game_pak";
        static string ModPakFileName = string.Empty;
        static string RestorePakFileName = string.Empty;
        static string ModFileFolderName = "aamod/";
        static string SFXInfoFileName = ModFileFolderName + "aamod.exe"; // if present, this needs to be the first file in the pak
        static string ModInfoFileName = ModFileFolderName + "aamod.txt";
        static string ModPNGImageFileName = ModFileFolderName + "aamod.png";
        static string ModJPGImageFileName = ModFileFolderName + "aamod.jpg";
        static string ModNewFilesFileName = ModFileFolderName + "newfiles.txt";
        private string DefaultTitle;
        private string helpText = "Command-Line Arguments:\n\n" +
            "-m <modfile> : use custom mod file\n\n" +
            "-g <gamepak> : use specified game_pak. If no gamepak is provided, a file open dialog will pop up to ask the location\n\n" +
            "-r <restorefile> : Use a custom restore file (not recommended)\n\n" +
            "-? : This help message";
        private AAPak gamepak = new AAPak("");
        private AAPak modpak = new AAPak("");
        private AAPak restorepak = new AAPak("");
        private List<AAPakFileInfo> FilesToMod = new List<AAPakFileInfo>();
        private List<AAPakFileInfo> FilesToInstall = new List<AAPakFileInfo>();
        private List<AAPakFileInfo> FilesToUnInstall = new List<AAPakFileInfo>();
        private List<AAPakFileInfo> FilesAddedWithInstall = new List<AAPakFileInfo>();
        private List<string> RestoreNewFilesList = new List<string>();

        public ModMainForm()
        {
            InitializeComponent();
        }

        private bool HandleArgs()
        {
            bool showHelp = false;

            var args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                var larg = args[i].ToLower();
                string nextArg = "";
                if (i < args.Length-1)
                {
                    nextArg = args[i + 1];
                }


                switch(larg)
                {
                    case "-m":
                        // Mod source
                        ModPakFileName = nextArg;
                        i++;
                        break;
                    case "-g":
                        // Game_Pak
                        GamePakFileName = nextArg;
                        i++;
                        break;
                    case "-r":
                        // restore pak
                        RestorePakFileName = nextArg;
                        i++;
                        break;
                    case "-?":
                    case "-h":
                    case "-help":
                    case "/?":
                    case "/h":
                    case "/help":
                        // Help
                        showHelp = true;
                        break;
                }

            }


            if (showHelp)
            {
                Hide();
                MessageBox.Show(helpText, "AAMod - Command-Line Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            // if nothing specified, try self
            if (ModPakFileName == string.Empty)
            {
                ModPakFileName = Application.ExecutablePath;
                Text = DefaultTitle + " - Self-Extractor - " + Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            }
            else
            {
                Text = DefaultTitle + " - " + ModPakFileName;
            }

            if (!File.Exists(ModPakFileName))
            {
                MessageBox.Show("Mod-file not found\n" + ModPakFileName, "aamod Open Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return false;
            }
            if (!modpak.OpenPak(ModPakFileName,true))
            {
                if (ModPakFileName == Application.ExecutablePath)
                {
                    // show a different error if you tried to open it without any parameters and tried to use the exe as a pak and failed
                    Visible = false;
                    MessageBox.Show("This program cannot be used as a stand-alone program.\n" +
                        "You either need to provide command-line arguments or this file needs to be generated by AAPakEditor to contain pak information.\n\n" +
                        "Use -? to get a list of possible arguments.", "AAMod Open Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
                else
                {
                    MessageBox.Show("Failed to open mod-file for reading\n" + ModPakFileName, "aamod Open Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
                return false;
            }


            if (modpak.FileExists(ModInfoFileName))
            {
                var infoStream = modpak.ExportFileAsStream(ModInfoFileName);
                var tw = AAPak.StreamToString(infoStream);
                tDescription.Text = tw;
            }
            else
            {
                tDescription.Text = "No description provided for this mod file.";
            }
            tDescription.SelectionLength = 0;

            if (modpak.FileExists(ModPNGImageFileName))
            {
                var picStream = modpak.ExportFileAsStream(ModPNGImageFileName);
                var img = Image.FromStream(picStream);
                modIcon.Image = img;
            }
            else
            if (modpak.FileExists(ModJPGImageFileName))
            {
                var picStream = modpak.ExportFileAsStream(ModJPGImageFileName);
                var img = Image.FromStream(picStream);
                modIcon.Image = img;
            }


            while (!File.Exists(GamePakFileName))
            {
                if (openGamePakDlg.ShowDialog() == DialogResult.OK)
                {
                    GamePakFileName = openGamePakDlg.FileName;
                }
                else
                {
                    return false;
                }
            }
            Refresh();
            lInstallLocation.Text = "Loading, please wait ...";
            lInstallLocation.Refresh();
            if (gamepak.OpenPak(GamePakFileName, false))
            {
                lInstallLocation.Text = GamePakFileName;
            }
            else
            {
                MessageBox.Show("Failed to open ArcheAge game pak for writing\n" + GamePakFileName, "game_pak Open Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return false;
            }

            if (RestorePakFileName == string.Empty)
            {
                RestorePakFileName = Path.GetDirectoryName(GamePakFileName).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + ".aamod"+ Path.DirectorySeparatorChar + "restore_pak" ;
            }

            if (File.Exists(RestorePakFileName))
            {
                if (!restorepak.OpenPak(RestorePakFileName, false))
                {
                    MessageBox.Show("Failed to open mod uninstall file for writing\n" + RestorePakFileName+"\n\nThis is most likely due to a corrupted restore file. Previously backed-up data is lost !", "restore_pak Open Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    restorepak.ClosePak();
                    if (MessageBox.Show("Do you want to create a new restore file ?\nThis step is required to be able to continue.","Re-Create backup file",MessageBoxButtons.YesNo,MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (!restorepak.NewPak(RestorePakFileName))
                        {
                            MessageBox.Show("Failed to re-create mod uninstall file\n" + RestorePakFileName, "restore_pak Create Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(RestorePakFileName));
                }
                catch
                {
                    MessageBox.Show("Failed to create mod uninstall directory for \n" + RestorePakFileName, "restore_pak Directory Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return false;
                }
                // create a new one if none exists yet
                if (!restorepak.NewPak(RestorePakFileName))
                {
                    MessageBox.Show("Failed to create mod uninstall file\n" + RestorePakFileName, "restore_pak Create Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return false;
                }
            }

            ReadNewFilesFromRestore();
            // TODO: continue work here

            return true;
        }

        private void ReadNewFilesFromRestore()
        {
            RestoreNewFilesList.Clear();
            AAPakFileInfo rnfl = restorepak.nullAAPakFileInfo;
            if (restorepak.GetFileByName(ModNewFilesFileName, ref rnfl))
            {
                var rf = restorepak.ExportFileAsStream(rnfl);
                var s = AAPak.StreamToString(rf);
                RestoreNewFilesList.AddRange(s.Split('\n').ToArray());
            }
        }

        private void WriteNewFilesForRestore()
        {
            var ms = AAPak.StringToStream(string.Join("\n", RestoreNewFilesList.ToArray()));
            restorepak.AddFileFromStream(ModNewFilesFileName, ms, DateTime.Now, DateTime.Now, false, out var pfi);
        }

        private void ModMainForm_Load(object sender, EventArgs e)
        {
            DefaultTitle = Text;
            Icon = AAMod.Properties.Resources.aamod_icon;
        }

        private void ModMainForm_Shown(object sender, EventArgs e)
        {
            UseWaitCursor = true;
            Cursor = Cursors.WaitCursor;
            // returns false if errors in initialization
            if (!HandleArgs())
            {
                Environment.ExitCode = 1;
                Close();
                return;
            }

            ValidateInstallOptions();

            Cursor = Cursors.Default;
            UseWaitCursor = false;
        }

        private void ValidateInstallOptions()
        {
            // TODO: Enabled buttons depending on the state of game_pak and restore_pak compared to the aamod pak
            // Get file list from mod pak
            FilesToMod.Clear();
            foreach(var fi in modpak.files)
            {
                if (fi.name.StartsWith(ModFileFolderName))
                {
                    // Don't include own mod files
                }
                else
                {
                    // TODO: compare to gamepak to check if installed or not
                    FilesToMod.Add(fi);
                }
            }

            FilesToInstall.Clear();
            FilesAddedWithInstall.Clear();
            foreach (var fi in FilesToMod)
            {
                AAPakFileInfo gfi = gamepak.nullAAPakFileInfo ;
                if (gamepak.GetFileByName(fi.name, ref gfi))
                {
                    if ( (fi.size != gfi.size) || (!fi.md5.SequenceEqual(gfi.md5)) )
                    {
                        FilesToInstall.Add(fi);
                    }
                }
                else
                {
                    FilesToInstall.Add(fi);
                    FilesAddedWithInstall.Add(fi);
                }
            }

            FilesToUnInstall.Clear();
            foreach (var fi in FilesToMod)
            {
                if (restorepak.FileExists(fi.name))
                    FilesToUnInstall.Add(fi);
            }

            if (FilesToInstall.Count > 0)
            {
                btnInstall.Enabled = true;
                string s = string.Empty;
                if (FilesToMod.Count != 1)
                    s += "Mod " + FilesToMod.Count.ToString() + " file(s)";
                else
                    s += "Mod " + FilesToMod.Count.ToString() + " file";
                if (FilesAddedWithInstall.Count > 0)
                    s += ", with " + FilesAddedWithInstall.Count.ToString() + " new";
                lInstallInfo.Text = s;
            }

            // Check if these same files are all present in the restore pak
            // If not, disable the uninstall option (likely not installed)
            // TODO: 
            btnUninstall.Enabled = (FilesToUnInstall.Count > 0);
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            btnInstall.Enabled = false;
            pb.Minimum = 0;
            pb.Maximum = FilesToMod.Count ;
            pb.Step = 1;
            lInstallInfo.Text = "Creating restore files";
            foreach(var fi in FilesToInstall)
            {
                // If file exists in gamepak, make a backup
                AAPakFileInfo gamefi = gamepak.nullAAPakFileInfo;
                if (gamepak.GetFileByName(fi.name, ref gamefi))
                {
                    var fileBackupStream = gamepak.ExportFileAsStream(fi.name);
                    fileBackupStream.Position = 0;
                    AAPakFileInfo restoreFileInfo = restorepak.nullAAPakFileInfo;
                    if (!restorepak.AddFileFromStream(fi.name, fileBackupStream, DateTime.FromFileTime(gamefi.createTime), DateTime.FromFileTime(gamefi.modifyTime), false, out restoreFileInfo))
                    {
                        MessageBox.Show("Error making backup of " + fi.name);
                    }
                }

                var fileModStream = modpak.ExportFileAsStream(fi.name);
                fileModStream.Position = 0;
                AAPakFileInfo newModFile = gamepak.nullAAPakFileInfo;
                if (!gamepak.AddFileFromStream(fi.name, fileModStream, DateTime.FromFileTime(fi.createTime), DateTime.FromFileTime(fi.modifyTime), false, out newModFile))
                {
                    MessageBox.Show("Error modding file " + fi.name);
                }

                pb.PerformStep();
                pb.Refresh();
                //System.Threading.Thread.Sleep(50);
            }

            lInstallInfo.Text = "Updating game file header";
            lInstallInfo.Refresh();
            gamepak.SaveHeader();

            lInstallInfo.Text = "Updating restore file data";
            lInstallInfo.Refresh();
            ReadNewFilesFromRestore();
            // Add newly added files to the newfiles.txt
            foreach (var fi in FilesAddedWithInstall)
            {
                if (RestoreNewFilesList.IndexOf(fi.name) < 0)
                    RestoreNewFilesList.Add(fi.name);
            }
            WriteNewFilesForRestore();
            restorepak.SaveHeader();
            pb.Value = pb.Maximum;

            MessageBox.Show("Mod has been installed", "Install", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ValidateInstallOptions();
        }

        private void ModMainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            gamepak.ClosePak();
            modpak.ClosePak();
            restorepak.ClosePak();
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            btnInstall.Enabled = false;
            pb.Minimum = 0;
            pb.Maximum = FilesToMod.Count;
            pb.Step = 1;
            lInstallInfo.Text = "Uninstalling mod files";
            foreach (var fi in FilesToMod)
            {
                // If file exists in gamepak, make a backup
                AAPakFileInfo rfi = restorepak.nullAAPakFileInfo;
                if (restorepak.GetFileByName(fi.name,ref rfi))
                {
                    var fileRestoreStream = restorepak.ExportFileAsStream(fi.name);
                    fileRestoreStream.Position = 0;
                    AAPakFileInfo restoreFileInfo = gamepak.nullAAPakFileInfo;
                    if (!gamepak.AddFileFromStream(fi.name, fileRestoreStream, DateTime.FromFileTime(rfi.createTime), DateTime.FromFileTime(rfi.modifyTime), false, out restoreFileInfo))
                    {
                        MessageBox.Show("Error restoring file " + fi.name);
                    }
                    else
                    {
                        restorepak.DeleteFile(rfi);
                    }
                }

                pb.PerformStep();
                pb.Refresh();
                // System.Threading.Thread.Sleep(50);
            }

            // Remove added files

            if (restorepak.FileExists(ModNewFilesFileName))
            {
                ReadNewFilesFromRestore();
                                
                foreach (var fn in FilesToMod)
                {
                    if (RestoreNewFilesList.IndexOf(fn.name) < 0)
                        continue;
                    AAPakFileInfo delFileInfo = gamepak.nullAAPakFileInfo;
                    if (gamepak.GetFileByName(fn.name,ref delFileInfo))
                    {
                        gamepak.DeleteFile(delFileInfo);
                        RestoreNewFilesList.Remove(fn.name);
                    }
                }

                WriteNewFilesForRestore();
            }

            lInstallInfo.Text = "Updating pak file headers";
            lInstallInfo.Refresh();
            restorepak.SaveHeader();
            gamepak.SaveHeader();
            pb.Value = pb.Maximum;

            MessageBox.Show("Mod has been uninstalled", "Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ValidateInstallOptions();

        }

        private void ModMainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.V) && (e.Control) && (e.Alt))
                MessageBox.Show("AAMod Installer " + appVerDate);
        }
    }
}
