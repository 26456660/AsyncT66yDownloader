using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace AsyncFramework
{
    public class MyLittleClock
    {
        private TimeSpan _timeOutSeconds = new TimeSpan(0, 0, 4);
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
                        Thread.Sleep(1000);
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
        public int interval = 0;
        private MyLittleClock Ticker = null;
        CancellationTokenSource taskCts = new CancellationTokenSource();
        CancellationToken taskCt;
        DateTime taskStartTime = DateTime.Now;
        public TimeSpan taskLivingSpan;

        public MyTask(int _taskInterval, int _taskLivingSpan, MyLittleClock t)
        {
            interval = _taskInterval;
            taskLivingSpan = new TimeSpan(0,0, _taskLivingSpan);
            Ticker = t;
            taskCt = taskCts.Token;
            //taskCt.Register();
            t.CheckPointHandler += TimeOutChecker;
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
                    if (this.interval < 5000)
                        Console.WriteLine("fast task has been cancelled!");
                    else
                        Console.WriteLine("slow task has been cancelled!");
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
                if (interval < 5000)
                    Console.WriteLine("fast task {0}  has been done!, it's on thread{1}!", number, Thread.CurrentThread.ManagedThreadId);
                else
                    Console.WriteLine("task {0}  has been done!, it's on thread{1}!", number, Thread.CurrentThread.ManagedThreadId);
                Thread.Sleep(interval);
            }
            catch(OperationCanceledException ce)
            {
                Console.WriteLine("task has been cancelled!");
            }
        }
    }

    class Program
    {
        public static bool allFinished = false;
        public static List<MyTask> TaskPool = new List<MyTask>();

        static void Main(string[] args)
        {
            MyLittleClock t = new MyLittleClock(TaskPool);
            Console.WriteLine("Task Start !");
            //DotaskWithThread();
            
            DOTaskWithAsync(t);
            Console.WriteLine("Task End !");
            Console.ReadLine();
        }

        public static async void DOTaskWithAsync(MyLittleClock t)
        {
            Console.WriteLine("Await Taskfunction Start");

            await Task.Run(() => {
                MyTask slowTask = new MyTask(5000, 10, t);
                List<MyTask> taskPool = t.userData as List<MyTask>;
                if (taskPool != null)
                    taskPool.Add(slowTask);
                    slowTask.DoTask();
                }, t.ct);
            
            await Task.Run(() => {
                MyTask fastTask = new MyTask(1000, 15, t);
                TaskPool.Add(fastTask);
                fastTask.DoTask();
            }, t.ct);
            t.allTaskAdded = true;
            Console.WriteLine("All task added");
            if (TaskPool.Count == 0)
                t.allFinished = true;
        }
    }
}