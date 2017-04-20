using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Renci.SshNet;

namespace OPCService
{
    public class Result
    {
        private static object locker = new object();

        public static void Output(string groupName, string data, DateTimeOffset timestamp, bool isError = false)
        {
            string outpath = Common.GetConfig("datapath");
            outpath = Path.Combine(outpath, timestamp.ToString("yyyyMM"), timestamp.ToString("dd"), timestamp.ToString("HH"));
            
            DirectoryInfo di = new DirectoryInfo(outpath);
            if (!di.Exists)
            {
                di.Create();
            }

            StreamWriter sw = null;
            string tdir = "data";
            string fname = String.Format("data_{0}.csv", timestamp.ToString("yyyyMMdd_HHmmss"));
            if (isError)
            {
                tdir = "alert";
                fname = String.Format("alert_{0}.csv", timestamp.ToString("yyyyMMdd_HHmmss"));
            }
            string fTarget = Path.Combine(di.FullName, fname);
            try
            {
                using (sw = new StreamWriter(fTarget, true))
                {
                    sw.WriteLine(data);
                }
            }
            catch (Exception em) {
                Log.Write("Error write file for " + tdir + " : " + em.Message, LogType.ERROR);
            }

            //TransferFile(new FileInfo(fTarget), tdir);
            //var task = Task.Run(async () => await TransferFile(new FileInfo(fTarget), tdir));
            //if (task.IsCompleted) task.Dispose();
        }

        public static void Output(string fname, string data)
        {
            try
            {
                lock (locker)
                {
                    File.AppendAllText(fname, Environment.NewLine + data.Replace("\n", Environment.NewLine));
                }
            }
            catch (Exception em)
            {
                Log.Write("Error write file : " + em.Message);
            }
        }

        protected static void TransferFile(FileInfo file, string targetDir, bool isResend = false)
        {
            string assetDir = Common.GetConfig("assetfolder");
            string hostName = "go.eaciit.com";
            string keyFile = Path.Combine(assetDir, "orange.pem");
            string username = "orange-user";
            int port = 22;
            string targetRootDir = "/data1/orange_data/hfd/";
            string targetLocation = targetRootDir + targetDir + "/" + file.Name;

            string privateKey = File.ReadAllText(keyFile);

            PrivateKeyFile pkf = new PrivateKeyFile(keyFile);

            List<AuthenticationMethod> methods = new List<AuthenticationMethod>();
            methods.Add(new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile[]{ pkf }));

            ConnectionInfo conn = new ConnectionInfo(hostName, port, username, methods.ToArray());
            bool isErrorSentFile = false;
            try
            {
                using (SftpClient client = new SftpClient(conn))
                {
                    client.Connect();
                    if (client.IsConnected)
                    {
                        using (var fs = new FileStream(file.FullName, FileMode.Open))
                        {
                            client.BufferSize = 4 * 1024;
                            client.UploadFile(fs, targetLocation, null);
                        }

                        //if (!isResend)
                        //{
                        //    ThreadStart ts = new ThreadStart(delegate { ResendFailed(); });
                        //    Thread thr = new Thread(ts);
                        //    thr.Start();
                        //}
                    }
                    else
                    {
                        isErrorSentFile = true;
                        Log.Write("Cannot connect to the server!", LogType.ERROR);
                    }

                    client.Disconnect();
                }
            }
            catch (Exception em)
            {
                isErrorSentFile = true;
                Log.Write("Error occured when transfering file :" + em.Message, LogType.ERROR);
            }

            if (isErrorSentFile)
            {
                AddSentFailed(file.FullName);
            }
        }

        protected static void AddSentFailed(string fileFullName)
        {
            string outpath = Common.GetConfig("datapath");
            string queuepath = Path.Combine(outpath, "queue");
            string fqueue = Path.Combine(queuepath, "failed.log");

            using (StreamWriter sw = File.AppendText(fqueue))
            {
                sw.WriteLine(fileFullName);
            }
        }

        protected static void ResendFailed()
        {
            string outpath = Common.GetConfig("datapath");
            string queuepath = Path.Combine(outpath, "queue");
            string fqueue = Path.Combine(queuepath, "failed.log");

            FileInfo fi = new FileInfo(fqueue);
            if (fi.Exists)
            {
                string newQueue = fi.FullName.Replace(".log", String.Format("_backup_{0}{1}", DateTime.Now.ToString("yyyyMMdd_HHmmss_ffffff"), ".log"));
                fi.MoveTo(newQueue);
                fi.Delete();

                FileInfo fiNew = new FileInfo(newQueue);
                string line;
                using (StreamReader sr = new StreamReader(fiNew.FullName))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        //string tdir = "data";
                        //if (line.Contains("alert")) tdir = "alert";
                        //TransferFile(new FileInfo(line), tdir, true);
                    }
                }                
            }
        }

        protected static bool CheckConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
