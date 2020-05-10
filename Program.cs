using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Security.Policy;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;

namespace AsyncFramework
{
    public class MyLittleClock
    {
        private TimeSpan _timeOutSeconds = new TimeSpan(0, 0, 10);
        public CancellationTokenSource cts = new CancellationTokenSource();
        private Stopwatch stopWatch = new Stopwatch();
        public CancellationToken ct;
        public int pendingTasks = 99;
        public bool allFinished = false;
        public bool allTaskAdded { get; set; }

        public bool hasStarted { get; set; }
        public DateTime startTime { set; get; }
        public Object userData { set; get; }
        public int TimeoutSeconds
        {
            get
            {
                return _timeOutSeconds.Seconds;
            }
            set
            {
                if (value <= 0)
                    return;
                _timeOutSeconds = new TimeSpan(0, 0, value);
            }
        }

        public delegate void AtEachCheckPoint(object userdata);
        public event AtEachCheckPoint CheckPointHandler;

        public MyLittleClock(object userdata)
        {
            allTaskAdded = false;
            userData = userdata;
            CheckPointHandler += OnCheckPoint;
            ct = cts.Token;
            //ct.Register(() => OnTimeOver(userdata));
            //Start();
        }

        public virtual void OnCheckPoint(object userdata)
        {
            double _time = stopWatch.Elapsed.TotalMinutes;
            _time = Math.Round(_time, 1);
            Console.WriteLine("check point reached! Now is " + DateTime.Now.ToString("HH:mm:ss") + " Program running: "+ _time.ToString()+ " minutes...");
            Reset();
            List<MyTask> _taskPool = userdata as List<MyTask>;
            if (allTaskAdded && pendingTasks == 0)
            {
                cts.Cancel();
            }
        }
        public void OnTimeOver(object userdata)
        {
            double _time = stopWatch.Elapsed.TotalMinutes;
            _time = Math.Round(_time, 1);
            Console.WriteLine("OnTimeOver called! Now is " + DateTime.Now.ToString("HH: mm:ss") + " Total Time using: "+ _time.ToString()+ " minutes...");
            stopWatch.Stop();
            Stop();
        }
        public async void Start()
        {
            startTime = DateTime.Now;
            hasStarted = true;
            stopWatch.Start();
            //var task = await Task.Run(() => { TimeTicking(); return cts.IsCancellationRequested; }, cts.Token);
            var task = await Task.Run(() => {
                while (!allFinished)
                {
                    try
                    {
                        TimeTicking();
                        Thread.Sleep(2000);
                        ct.ThrowIfCancellationRequested();
                    }
                    catch (ThreadAbortException e)
                    {
                        Console.WriteLine(e.ToString());
                        break;
                    }
                    catch (ThreadInterruptedException e)
                    {
                        Console.WriteLine(e.ToString());
                        break;
                    }
                    catch (OperationCanceledException e)
                    {
                        Console.WriteLine(e.ToString());
                        break;
                    }
                    finally
                    {
                    }
                }
                Console.WriteLine("Timer has been Stopped!");
                return ct.IsCancellationRequested;
            }, ct);
        }

        public void Stop()
        {
            hasStarted = false;
            allFinished = true;
            cts.Cancel();
            //ct.ThrowIfCancellationRequested();
        }

        public void Reset()
        {
            hasStarted = true;
            startTime = DateTime.Now;
        }

        private void TimeTicking()
        {
            if (hasStarted)
            {
                //Console.WriteLine("i'm ticking now!");
                if (CheckTimeOut())
                {
                    CheckPointHandler(userData);
                }
            }
        }
        private bool CheckTimeOut()
        {
            return (DateTime.Now - startTime).TotalSeconds >= TimeoutSeconds;
        }
    }

    class MyTask
    {
        public DownloadManager downloadManager = null;
        public bool taskWorkingNow = false;
        public string typeOfTask { get; set; }
        //public int interval = 0;
        private MyLittleClock Ticker = null;
        public CancellationTokenSource taskCts = new CancellationTokenSource();
        public CancellationToken taskCt = new CancellationToken();
        DateTime taskStartTime = DateTime.Now;
        public TimeSpan taskLivingSpan;
        public int retryTimes { get; set; }
        public bool taskComplete { get; set; }

        public string savingFilePath { get; set; }
        public string savingDirectory { get; set; }

        public Uri downloadLink { get; set; }
        public string cookie = "__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144";
        public string referLink = "";

        public MyTask(string _typeOfTask, int _taskLivingSpan, string _filePath, string _directoryPath, Uri _downloadlink, string _referLink, MyLittleClock t)
        {
            typeOfTask = _typeOfTask;
            taskLivingSpan = new TimeSpan(0, 0, _taskLivingSpan);
            Ticker = t;
            taskCt = taskCts.Token;
            //taskCt.Register();

            savingFilePath = _filePath;
            savingDirectory = _directoryPath;
            downloadLink = _downloadlink;
            referLink = _referLink;
            retryTimes = 0;
            taskComplete = false;
        }

        //Get Current Time for the task
        public void ResetStartTime()
        {
            taskStartTime = DateTime.Now;
        }

        public void TimeOutChecker(object userdata)
        {
            if ((DateTime.Now - taskStartTime).TotalSeconds > taskLivingSpan.TotalSeconds)
            {
                if (!taskCts.IsCancellationRequested)
                {
                    taskCts.Cancel();
                }
            }
        }

        public static string LocalFileReader(string filePath)
        {
            string text = "";
            FileStream myfile = new FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var fileEncoding = Encoding.GetEncoding("utf-8");
            StreamReader sr = new StreamReader(myfile, fileEncoding, true);
            while (!sr.EndOfStream)
            {
                text += sr.ReadLine();
                text += "\n";
            }

            sr.Close();
            myfile.Close();
            return text;
        }

        public static void LocalFileWriter(string filePath, string content)
        {
            FileStream myfile = new FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            var fileEncoding = Encoding.GetEncoding("utf-8");
            StreamWriter sw = new StreamWriter(myfile, fileEncoding);
            sw.WriteLine(content);
            sw.Close();
            myfile.Close();
        }
    }

    class DownloadManager
    {
        public Uri downloadLink { get; set; }
        public string filePath { get; set; }
        public WebClient webclient = new WebClient();
        public string directoryPath { get; set; }
        public CancellationTokenSource taskCts;
        public CancellationToken taskCt;
        public delegate void DownloadDelegate();
        public bool missionComplete = false;
        public DownloadDelegate DownloadStart = null;
        public string referpath = "";

        public DownloadManager(string _type, Uri _dlink, string _filePath, string _directoryPath, string _refer, string _cookie, CancellationTokenSource _cts)
        {
            taskCts = _cts;
            taskCt = _cts.Token;
            downloadLink = _dlink;
            referpath = _refer;
            filePath = _filePath;
            directoryPath = _directoryPath;
            webclient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36 Edge/18.18363");
            if (_refer != "")
                webclient.Headers.Add("Refer", _refer);
            if (_cookie != "")
                webclient.Headers.Add("Cookie", _cookie);
            
            switch (_type)
            {
                case "photo":
                    DownloadStart = Download_Photo;
                    break;
                case "torrent":
                    DownloadStart = Download_TorrentFile;
                    break;
            }
        }

        private bool FileNormalCheck(string path)
        {
            bool normal = false;
            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Length > 200)
                    normal = true;
            }
            return normal;
        }
        public void Download_TorrentFile()
        {
            //webclient.Headers.Add("Referer", downloadlink);
            //webclient.Headers.Add("Cookie", "__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144");
            WebRequest myrequest = WebRequest.Create(new Uri(referpath));
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            Task<WebResponse> _responseAsync = null;
            WebResponse _response = null;
            Task _responseTask = Task.Run(() =>
            {
                _responseAsync = myrequest.GetResponseAsync();
                _response = _responseAsync.Result;
                return ct.IsCancellationRequested;
            }, ct);
            cts.CancelAfter(30 * 1000);
            object o = new object();
            lock(o)
            {
                if (!ct.IsCancellationRequested)
                    _responseTask.Wait(ct);
            }


            if (_responseAsync == null)
                return;

            if (!FileNormalCheck(filePath))
            {
                string password = _responseAsync.Result.Headers["Set-Cookie"];

                password = password.Replace("; path=/", "");
                Regex x = new Regex("__cfduid = [\\s\\S]*(?<=(;))");
                string _id = new Regex("__cfduid=[\\s\\S]*?(?=;)").Match(password).Value;
                password = new Regex("PHPSESSID=[\\s\\S]*").Match(password).Value;

                webclient.Headers["Cookie"] = _id + ";" + password + ";";
                Task.Run(() =>
                    {
                        try
                        {
                            
                            webclient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadComplete);
                            webclient.DownloadFileAsync(downloadLink, filePath, taskCt);
                            
                            //webclient.DownloadFile(downloadLink, filePath);
                            //missionComplete = true;
                        }
                        catch (TimeoutException e)
                        {
                            taskCts.Cancel();
                        }
                    },taskCt
                );
                
                //webclient.DownloadFile(downloadLink, filePath);
            }

            /*myrequest.GetResponse();
            if (!File.Exists(filePath))
                {
                    string password = myrequest.GetResponse().Headers["Set-Cookie"];
                    
                    //buffer = webclient.DownloadData(downloadlink);

                    //string t = Encoding.GetEncoding("utf-8").GetString(buffer);
                    //string password = webclient.ResponseHeaders["Set-Cookie"];
                    
                    password = password.Replace("; path=/", "");
                    //webclient.Headers["Cookie"]="__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144;"+password+";";
                    password = new Regex("PHPSESSID=[\\s\\S]*").Match(password).Value;
                    //webclient.Headers["Cookie"] = password;
                    webclient.Headers["Cookie"] = "__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144;" + password + ";";
                    try
                    {
                        if (!File.Exists(filePath))
                            webclient.DownloadFile(downloadLink, filePath);
                    }
                    catch 
                    { 
                    }
                }
                */
        }
        public void DownloadComplete(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Console.WriteLine(e.Error.Message);
                if (e.Error.Message.Contains("404"))
                    missionComplete = true;
            }
            else
                missionComplete = true;
            if (!taskCts.IsCancellationRequested)
                taskCts.Cancel();
        }

        public void Download_Photo()
        {
            WebClient webclient = new WebClient();
            if (!FileNormalCheck(filePath))
            {
                Task.Run(() =>
                {
                    try
                    {
                        webclient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadComplete);
                        webclient.DownloadFileAsync(downloadLink, filePath, taskCt);
                    }
                    catch (TimeoutException e)
                    {
                        taskCts.Cancel();
                    }
                });
            }
        }
    }
    class Program
    {
        public static bool allFinished = false;
        public static List<MyTask> TaskPool = new List<MyTask>();
        public static MyLittleClock t = new MyLittleClock(TaskPool);
        public static List<MyTask> PendingTasks = new List<MyTask>();
        public const int MAXCONNECTION = 5;
        public static int pDownloadQty = 0, tDownloadQty = 0;
        public static List<string> secondPageLinks = new List<string>();
        public static List<string> secondPageHtml = new List<string>();
        public static int startPage = 1, endPage = 2;
        public static string websiteLink = "https://cl.330f.tk/";

        static void Main(string[] args)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            string maindownloadUrl = "";
            string oldWebsiteLink = "", urlfile = "D:\\my t66y\\oldWebsiteLink.txt";
            if (File.Exists(urlfile))
            {
                oldWebsiteLink = MyTask.LocalFileReader(urlfile);
                oldWebsiteLink = oldWebsiteLink.Replace("\n", "");
            }
            Console.WriteLine("Input Download Url, or press Enter to continue last Setting: ");
            string newWebsiteLink = Console.ReadLine();
            newWebsiteLink = newWebsiteLink.Replace("\n", "");
            if (newWebsiteLink != "" && newWebsiteLink != oldWebsiteLink)
            {
                websiteLink = newWebsiteLink;
                MyTask.LocalFileWriter(urlfile,newWebsiteLink);
            }
            else
                if (oldWebsiteLink != "")
                    websiteLink = oldWebsiteLink;

            Console.WriteLine("Please enter start page: ");
            startPage = int.Parse(Console.ReadLine());
            Console.WriteLine("Please enter how many pages you want to get downloaded: ");
            endPage = startPage + int.Parse(Console.ReadLine()) - 1;

            t.Start();
            DOTaskWithAsync(t);
            t.allTaskAdded = true;
            t.ct.Register(() =>
                {
                    t.OnTimeOver(null);
                    Summurize(null);
                }
            );
            Console.ReadLine();
        }

        private static async void TaskPoolReview(object userdata)
        {
            object o = new object();
            lock(o)
            {
                while (TaskPool.Count < MAXCONNECTION && PendingTasks.Count > 0)
                {
                    MyTask _task = PendingTasks[0];
                    PendingTasks.Remove(_task);
                    TaskPool.Add(_task);
                    t.CheckPointHandler += _task.TimeOutChecker;
                }
            }

            t.pendingTasks = TaskPool.Count + PendingTasks.Count;
            List<MyTask> removelist = new List<MyTask>();
            foreach (MyTask _task in TaskPool)
            {
                if (_task.retryTimes < 3 && !_task.taskComplete)
                {
                    if (!_task.taskWorkingNow)
                    {
                        _task.ResetStartTime();
                        _task.taskWorkingNow = true;
                        _task.taskCts = new CancellationTokenSource();
                        _task.taskCt = _task.taskCts.Token;
                        _task.downloadManager = new DownloadManager(_task.typeOfTask, _task.downloadLink, _task.savingFilePath, _task.savingDirectory, _task.referLink, _task.cookie, _task.taskCts);
                        var _dTask = await Task.Run(() =>
                        {
                            _task.downloadManager.DownloadStart();
                            return _task.downloadManager.missionComplete;
                        }, _task.taskCt);
                    }
                    else
                    {
                        if (_task.downloadManager.missionComplete == true)
                            _task.taskComplete = true;
                        if (_task.taskCt.IsCancellationRequested)
                        {
                            _task.retryTimes++;
                            _task.taskWorkingNow = false;
                            _task.taskCts.Dispose();
                        }
                    }
                }
                else
                {
                    t.CheckPointHandler -= _task.TimeOutChecker;
                    if (_task.taskCts != null)
                    {
                        if (!_task.taskCt.IsCancellationRequested)
                            _task.taskCts.Cancel();
                        _task.taskCts.Dispose();
                    }
                    removelist.Add(_task);
                    if (_task.taskComplete)
                        if (_task.typeOfTask == "photo")
                            pDownloadQty++;
                        else if (_task.typeOfTask == "torrent")
                            tDownloadQty++;
                }
            }
            lock (o)
            {
                foreach (MyTask _task in removelist)
                {
                    TaskPool.Remove(_task);
                }
            }
            Console.WriteLine("We have finished {0} jobs, {1} wait to go!", pDownloadQty+tDownloadQty, PendingTasks.Count);
        }

        public static string T66YWebDownloader(string _link)
        {
            byte[] buffer = null;
            WebClient webclient = new WebClient();
            webclient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36 Edge/18.18363");
            Task<byte[]> _dTask = webclient.DownloadDataTaskAsync(new Uri(_link));
            buffer = _dTask.Result;
            string decodedhtml = Encoding.GetEncoding("gbk").GetString(buffer);
            webclient.Dispose();
            return decodedhtml;
        }
        public static void FirstLayerAnalyzer(string decodedhtml, string _link)
        {
            string serverlink = new Regex("https://[\\S]*?/").Match(_link).ToString();
            //Analisys
            //通过正则表达式获取内容
            Regex r = new Regex("");
            MatchCollection matches = new Regex("_blank\" id=\"\">([\\s\\S]*?)</a>").Matches(decodedhtml);
            MatchCollection links = new Regex("<h3><a href=\"([\\s\\S]*?)\" target=\"_blank\" id=\"\">([\\s\\S]*?)</a></h3>").Matches(decodedhtml);
            int count = links.Count;
            string[] input = new string[count];
            for (int i = 0; i < count; i++)
            {
                input[i] = links[i].Result("$2");
                //Console.WriteLine(input[i]);
                //Match wanted = new Regex("(双|(?<!(0-9)(3P|3p)))+").Match(input[i]);
                //Match wanted = new Regex("(双|菊|肛|(?<!(\\d))(3p|3P))+").Match(input[i]);
                Match wanted = new Regex("(菊|肛|奸|(双插)|(SPA|spa)|(屁眼)|(大神)|(全集)|(合集)|(留学)|(老外)|(为国争光)|(康爱福)|(调教)|(三穴)|(三通)|(轮插))+").Match(input[i]);
                if (wanted.Value != "")
                {
                    string sublink = serverlink + links[i].Result("$1");
                    secondPageLinks.Add(sublink);
                }
            }
        }
        private static bool FileNormalCheck(string path)
        {
            bool normal = false;
            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Length > 200)
                    normal = true;
            }
            return normal;
        }

        public static string SecondLayerDownloader(string _link)
        {
            string decodedHtml = "";
            byte[] buffer = null;
            WebClient webclient = new WebClient();
            webclient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36 Edge/18.18363");
            Task<byte[]> _dTask = webclient.DownloadDataTaskAsync(new Uri(_link));
            buffer = _dTask.Result;
            string decodedhtml = Encoding.GetEncoding("gbk").GetString(buffer);
            webclient.Dispose();
            return decodedHtml;
        }
        public static void SecondLayerAnalyzer(string _currenthtml)
        {
            string currenthtml = _currenthtml;
            string title;
            //Get the title
            Match titlematch = new Regex("(?<=<meta name=\"description\" content=\")[\\s\\S]*(?= 草榴社區 t66y\\.com\" />)").Match(currenthtml);
            title = titlematch.Value;
            title = title.Replace("/", "_");
            title = title.Replace("[", "_");
            title = title.Replace("]", "_");
            title = title.Replace("\\", "");
            title = title.Replace("?", "");
            title = title.Replace("*", "");

            //Get rid of  all the Quotes
            string htmlbackup = currenthtml;
            Match match1 = new Regex("<br><h6 class=\"quote\">Quote:</h6><blockquote>[\\s\\S]*</blockquote><br>").Match(currenthtml);
            if (match1.Value != "")
                currenthtml = currenthtml.Replace(match1.Value, "");

            //Get all the images
            MatchCollection matchjpg = new Regex("(?<=ess-data=')((?!ess-data=')[\\s\\S])*?\\.(jpg|png)(?='>)").Matches(currenthtml);
            //Very interesting ((?!xxx).) is a good trick
            if (matchjpg.Count == 0)
            {
                currenthtml = htmlbackup;
                matchjpg = new Regex("(?<=ess-data=')((?!ess-data=')[\\s\\S])*?\\.(jpg|png)(?='>)").Matches(currenthtml);
            }
            string imgfilename = "";
            string filedictory = "d:\\my t66y\\" + title + "\\";
            if (!Directory.Exists(filedictory))
                Directory.CreateDirectory(filedictory);

            for (int i = 0; i < matchjpg.Count; i++)
            {
                match1 = new Regex("(?<=/)((?!/).)+\\.(jpg|png)").Match(matchjpg[i].Value);
                imgfilename = match1.Value;
                string _path = "d:\\my t66y\\" + title + "\\" + imgfilename;
                if (!FileNormalCheck(_path))
                {
                    MyTask newTask = new MyTask("photo", 60, _path, filedictory, new Uri(matchjpg[i].Value), "", t);
                    PendingTasks.Add(newTask);
                }
            }

            //Get download link
            match1 = new Regex("http://www\\.rmdown\\.com/[\\s\\S]*?(?=</a>)").Match(currenthtml);
            string downloadlink = match1.Value;

            if (downloadlink != "")
            {
                //Save downloadLink
                if (!File.Exists(filedictory + "downloadlink.txt"))
                    MyTask.LocalFileWriter(filedictory + "downloadlink.txt", downloadlink);

                //Try to save Torrents file
                string _path = "d:\\my t66y\\" + title + "\\" + "torrent.torrent";
                string hashlink = new Regex("(?<=http://www\\.rmdown\\.com/link\\.php\\?hash=)[\\s\\S]*").Match(downloadlink).Value;
                if (!FileNormalCheck(_path))
                {
                    string torrentslink = "http://www.rmdown.com/download.php?reff=110&ref=" + hashlink;
                    MyTask newTask = new MyTask("torrent", 60, _path, filedictory, new Uri(torrentslink), downloadlink, t);
                    PendingTasks.Add(newTask);
                }
            }
            //TaskPoolReview(null);
        }

        public static string LocalTaskImporter()
        {
            string currenthtml = "";
            string filepath = "D:\\page2.txt";
            currenthtml = MyTask.LocalFileReader(filepath);
            return currenthtml;
        }

        public static void Summurize(object userdata)
        {
            Console.WriteLine("Bravo! we are reaching the End!");
            Console.WriteLine("We have downloaded {0} photos and {1} torrents this time, Enjoy!", pDownloadQty, tDownloadQty);
            Console.ReadLine();
        }

        public static void DOTaskWithAsync(MyLittleClock t)
        {
            Console.WriteLine(" Task really Start!");
            //string _html = LocalTaskImporter();
            for (int i = startPage; i < endPage; i++)
            {
                string downloadpage = websiteLink + "thread0806.php?fid=25&search=&page=" + i.ToString();
                FirstLayerAnalyzer(T66YWebDownloader(downloadpage), downloadpage);
            }
            for (int i = 0; i < secondPageLinks.Count; i++)
            {
                secondPageHtml.Add(T66YWebDownloader(secondPageLinks[i]));
            }
            for (int i = 0; i < secondPageHtml.Count; i++)
            {
                SecondLayerAnalyzer(secondPageHtml[i]);
            }

            t.CheckPointHandler += TaskPoolReview;
            /*
            await Task.Run(() => {
                MyTask slowTask = new MyTask(5000, 10, t);
                List<MyTask> taskPool = t.userData as List<MyTask>;
                if (taskPool != null)
                    taskPool.Add(slowTask);
                    slowTask.DoTask();
                }, t.ct);
            */
        }
    }
}