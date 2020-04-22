using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Security.Policy;

namespace AsyncFramework
{
    public class MyLittleClock
    {
        private TimeSpan _timeOutSeconds = new TimeSpan(0, 0, 10);
        public CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken ct;
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

        public delegate void TimeoutCaller(object userdata);
        public event TimeoutCaller CheckPointHandler;

        public MyLittleClock(object userdata)
        {
            allTaskAdded = false;
            userData = userdata;
            CheckPointHandler += OnCheckPoint;
            ct = cts.Token;
            ct.Register(() => OnTimeOver(userdata));
            Start();
        }

        public virtual void OnCheckPoint(object userdata)
        {
            Console.WriteLine("check point reached!");
            Reset();
            List<MyTask> _taskPool = userdata as List<MyTask>;
            if (allTaskAdded && _taskPool.Count == 0)
                Stop();
        }
        public virtual void OnTimeOver(object userdata)
        {
            Stop();
        }
        public async void Start()
        {
            startTime = DateTime.Now;
            hasStarted = true;
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
                Console.WriteLine("i'm ticking now!");
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
        public string typeOfTask { get; set; }
        //public int interval = 0;
        private MyLittleClock Ticker = null;
        CancellationTokenSource taskCts = new CancellationTokenSource();
        CancellationToken taskCt;
        DateTime taskStartTime = DateTime.Now;
        public TimeSpan taskLivingSpan;
        public int retryTimes { get; set; }
        public string savingFilePath { get; set; }
        public Uri downloadLink { get; set; }
        public string cookie = "__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144";
        public string referLink = "";

        public MyTask(string _typeOfTask, int _taskLivingSpan, string _filePath, Uri _downloadlink, string _referLink, MyLittleClock t)
        {
            typeOfTask = _typeOfTask;
            taskLivingSpan = new TimeSpan(0, 0, _taskLivingSpan);
            Ticker = t;
            taskCt = taskCts.Token;
            //taskCt.Register();
            t.CheckPointHandler += TimeOutChecker;
            savingFilePath = _filePath;
            downloadLink = _downloadlink;
            referLink = _referLink;
            retryTimes = 0;
        }
        public void TimeOutChecker(object userdata)
        {
            if ((DateTime.Now - taskStartTime).TotalSeconds > taskLivingSpan.TotalSeconds)
            {
                if (!taskCts.IsCancellationRequested)
                {
                    List<MyTask> taskPool = Ticker.userData as List<MyTask>;
                    if (taskPool != null && taskPool.Contains(this))
                    {
                        taskPool.Remove(this);
                        if (taskPool.Count == 0 && Ticker.allTaskAdded)
                            Ticker.allFinished = true;
                    }
                    taskCts.Cancel();
                    Ticker.CheckPointHandler -= TimeOutChecker;
                }
            }

        }
        public async void DoTask()
        {
            for (int i = 0; i <= 100; i++)
            {
                if (!taskCt.IsCancellationRequested)
                {
                    await Task.Run(() =>
                    {
                        Dotaskfunction(i);
                    }, taskCt);
                }
            }
        }
        public void Dotaskfunction(int number)
        {
            try
            {
                taskCt.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException ce)
            {
                Console.WriteLine("task has been cancelled!");
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
        public string downloadLink { get; set; }
        public string filePath { get; set; }
        public WebClient webclient = new WebClient();
        public string directoryPath { get; set; }

        public DownloadManager(string _dlink, string _filePath, string _directoryPath, string _refer, string _cookie)
        {
            downloadLink = _dlink;
            filePath = _filePath;
            directoryPath = _directoryPath;
            if (_refer != "")
                webclient.Headers.Add("Refer", _refer);
            if (_cookie != "")
                webclient.Headers.Add("Cookie", _cookie);

        }
        public void DownloadTorrentFile()
        {
            //webclient.Headers.Add("Referer", downloadlink);
            //webclient.Headers.Add("Cookie", "__cfduid=de6459d21de4fe09c8cf6754f71bbae1a1584457144");
            WebRequest myrequest = WebRequest.Create(downloadLink);
            var a = Task.Run(() => myrequest.GetResponseAsync());
            myrequest.Timeout = 30 * 1000;

            //myrequest.GetResponse();
                if (!File.Exists(filePath))
                {
                    string password = myrequest.GetResponse().Headers["Set-Cookie"];
                    /*
                    buffer = webclient.DownloadData(downloadlink);

                    string t = Encoding.GetEncoding("utf-8").GetString(buffer);
                    string password = webclient.ResponseHeaders["Set-Cookie"];
                    */
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
                }
        }
    }
    class Program
    {
        public static bool allFinished = false;
        public static List<MyTask> TaskPool = new List<MyTask>();
        public static MyLittleClock t = new MyLittleClock(TaskPool);
        public static List<MyTask> PendingTasks = new List<MyTask>();
        
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            string maindownloadUrl = "";
            Console.WriteLine("Input Download Url, or press Enter to continue last Setting");
            int startPageNo = 1;
            Console.Write("Please enter start page: ");
            int taskPageQty = 5;
            Console.Write("Please enter how many pages you want to get downloaded: ");
            //DotaskWithThread();


            DOTaskWithAsync(t);
            Console.WriteLine("Task End !");
            Console.ReadLine();
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
                if (!File.Exists(_path))
                {
                    MyTask newTask = new MyTask("photo", 60, _path, new Uri(matchjpg[i].Value), "", t);
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
                if (!File.Exists(_path))
                {
                    string torrentslink = "http://www.rmdown.com/download.php?reff=110&ref=" + hashlink;
                    MyTask newTask = new MyTask("Torrent", 60, _path, new Uri(torrentslink), downloadlink, t);
                    PendingTasks.Add(newTask);
                }
            }
        }

        private static void SecondLayer(string link, bool debugmode, int retrytimes)
        {

           //         buffer = webclient.DownloadData(link);
             //       currenthtml = Encoding.GetEncoding("gbk").GetString(buffer);
        }

        public static string LocalTaskImporter()
        {
            string currenthtml = "";
            string filepath = "D:\\page2.txt";
            currenthtml = MyTask.LocalFileReader(filepath);
            return currenthtml;
        }

        public static async void DOTaskWithAsync(MyLittleClock t)
        {
            Console.WriteLine(" Task really Start!");
            string _html = LocalTaskImporter();
            SecondLayerAnalyzer(_html);
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