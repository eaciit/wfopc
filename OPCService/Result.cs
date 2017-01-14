using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OPCService
{
    public class Result
    {
        public static void Output(string data, DateTimeOffset timestamp, bool isError = false)
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
                string fname = String.Format("data_{0}.log", timestamp.ToString("yyyyMMdd_HHmmss"));
                if (isError)
                {
                    fname = String.Format("alert_{0}.log", timestamp.ToString("yyyyMMdd_HHmmss"));
                }
                using (sw = File.AppendText(Path.Combine(di.FullName, fname)))
                {
                    sw.WriteLine(data);           
                }
            }
            catch { }
        }
    }
}
