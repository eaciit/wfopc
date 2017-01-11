using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OPCService
{
    public class Result
    {
        public static void Output(string data, DateTimeOffset timestamp)
        {
            string outpath = System.Configuration.ConfigurationManager.AppSettings["datapath"];
            outpath = Path.Combine(outpath, timestamp.ToString("yyyyMM"), timestamp.ToString("dd"), timestamp.ToString("HH"));
            DirectoryInfo di = new DirectoryInfo(outpath);
            if (!di.Exists)
            {
                di.Create();
            }

            StreamWriter sw = null;
            try
            {
                using (sw = File.AppendText(Path.Combine(di.FullName, String.Format("data_{0}.log", timestamp.ToString("yyyyMMdd_HHmm")))))
                {
                    sw.WriteLine(data);           
                }
            }
            catch { }
        }
    }
}
