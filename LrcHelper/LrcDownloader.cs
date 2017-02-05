﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using Ludoux.LrcHelper.NeteaseMusic;
using System.Diagnostics;

namespace LrcHelper
{
    public partial class LrcDownloader : Form
    {
        public LrcDownloader()
        {
            InitializeComponent();
        }
        private CancellationTokenSource cancelToken = new CancellationTokenSource();
        private void GETbutton_Click(object sender, EventArgs e)
        {
            StatusInfolabel.Text = "StatusInfo";
            StatusPDFinishedCountlabel.Text = "0";
            StatusPDTotalCountlabel.Text = "0";
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int ID = Convert.ToInt32(IDtextBox.Text);
            if (MusicradioButton.Checked)
            {
                Music m = new Music(ID);
                int status;
                string Log = DownloadLrc(ID, 100, ".\\" + m.GetFileName() + ".lrc",out status);
                sw.Stop();
                if (Log == "")
                    StatusInfolabel.Text = "Done Status:" + status + "\r\nUsed Time:" + sw.Elapsed.TotalSeconds +"sec";
                else
                    StatusInfolabel.Text = Log + " Status:" + status + "\r\nUsed Time:" + sw.Elapsed.TotalSeconds + "sec";
            }
            else if(PlaylistradioButton.Checked)
            {

                Task.Factory.StartNew(() =>
                {
                    ParallelOptions parOpts = new ParallelOptions();
                    parOpts.CancellationToken = cancelToken.Token;
                    parOpts.MaxDegreeOfParallelism = System.Environment.ProcessorCount;//上面三行针对TPL并行类库

                    List<string> Log = new List<string>();
                    Playlist pl = new Playlist(ID);
                    List<int> iDList = pl.SongIDInPlaylist;
                    string folderName = pl.GetFolderName();
                    for (int i = 0; i < iDList.Count; i++)
                        Log.Add("");//先写空白，后面并行直接写[i]
                    if (!System.IO.Directory.Exists(".\\" + folderName))
                        System.IO.Directory.CreateDirectory(".\\" + folderName);
                    this.Invoke((Action)delegate
                    {
                        StatusPDTotalCountlabel.Text = iDList.Count.ToString();//写Status
                        Cancelbutton.Enabled = true;//下面使用TPL类库，可以取消
                    });
                    try
                    {
                        Parallel.For(0, iDList.Count, i =>
                        {
                            parOpts.CancellationToken.ThrowIfCancellationRequested();
                            Music m = new Music(iDList[i]);
                            int status;
                            string ErrorLog = DownloadLrc(iDList[i], 100, ".\\" + folderName + @"\" + m.GetFileName() + ".lrc", out status);
                            if (System.IO.File.Exists(".\\" + folderName + @"\" + m.GetFileName() + ".lrc"))
                                Log[i] = string.Format("{0,-7}|{1,-12}|{2,-50}|{3,-6}|√" + ErrorLog, i + 1, iDList[i], m.Name, status);
                            else
                                Log[i] = string.Format("{0,-7}|{1,-12}|{2,-50}|{3,-6}|×" + ErrorLog, i + 1, iDList[i], m.Name, status);
                            this.Invoke((Action)delegate
                            {
                                StatusPDFinishedCountlabel.Text = (Convert.ToInt32(StatusPDFinishedCountlabel.Text) + 1).ToString();
                            });
                        });
                    }
                    catch (OperationCanceledException ex)
                    {
                        this.Invoke((Action)delegate
                        {
                            StatusInfolabel.Text = "OperationCanceled:" + ex.Message;
                        });
                    }
                    finally
                    {
                        cancelToken.Dispose();
                        this.Invoke((Action)delegate
                        {
                            Cancelbutton.Enabled = false;
                            GETbutton.Enabled = false;
                        });
                    }

                    sw.Stop();
                    StringBuilder OutLog = new StringBuilder();
                    OutLog.Append("PlaylistID:" + ID + "\r\nPlaylistName:" + pl.Name + "\r\nTotalCount:" + iDList.Count + "\r\n0为无人上传歌词,1为有词,2为纯音乐,-1错误,-2未命中\r\n");
                    OutLog.Append(string.Format("\r\n{0,-7}|{1,-12}|{2,-50}|{3,-6}|ErrorInfo", "SongNum", "SongID", "SongName", "LrcSts"));
                    for (int i = 0; i < Log.Count; i++)
                        OutLog.Append("\r\n" + Log[i]);
                    OutLog.Append("\r\n\r\n" + DateTime.Now.ToString() + "  Used Time:" + sw.Elapsed.TotalSeconds + "sec\r\n[re:Made by LrcHelper @https://github.com/ludoux/lrchelper]\r\n[ve:" + FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion + "]\r\nEnjoy music with lyrics now!(*^_^*)");
                    System.IO.File.WriteAllText(".\\" + folderName + @"\Log.txt", OutLog.ToString(), Encoding.UTF8);
                    this.Invoke((Action)delegate
                    {
                        StatusInfolabel.Text = "Done\r\nRead Log.txt to learn more.";
                    });
                });
                
            }
        }
        private string DownloadLrc(int MusicID,int DelayMsc, string File, out int status)
        {
            Lyric l = new Lyric(MusicID);
            
            l.GetOnlineLyric();
            string lyricText = l.GetDelayedLyric(DelayMsc);
            if (lyricText != "")
            {
                System.IO.File.WriteAllText(File, lyricText+ "\r\n[re:Made by LrcHelper @https://github.com/ludoux/lrchelper]\r\n[ve:"+System.Diagnostics.FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion+ "]", Encoding.UTF8);
                status = l.GetLyricStatus();
                return "";
            }
            else
            {
                status = l.GetLyricStatus();
                return l.GetErrorLog();
            }
        }

        private void Cancelbutton_Click(object sender, EventArgs e)
        {
            cancelToken.Cancel();//停止所有工作者进程
        }
    }
}