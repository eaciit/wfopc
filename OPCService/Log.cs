using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OPCService
{
    public enum LogType { START, STOP, WARNING, ERROR, SUBSCRIBE, WRITE, OTHER };
    public class Log
    {
        public static void Write(string msg, LogType type = LogType.WRITE)
        {
            string logpath = System.Configuration.ConfigurationManager.AppSettings["logpath"];
            
            StreamWriter sw = null;
            try
            {
                using (sw = File.AppendText(Path.Combine(logpath, String.Format("service_{0}.log", DateTime.Today.ToString("yyyyMMdd")))))
                {
                    AddLog(type, msg, sw);
                }   
            }
            catch {}
        }

        private static void AddLog(LogType type, string msg, StreamWriter sw)
        {
            string log = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " : " + type.ToString();
            switch (type)
            {
                case LogType.START:
                    log += " : starting service";
                    break;
                case LogType.STOP:
                    log += " : stopping service";
                    break;
                case LogType.ERROR:
                    log += " : error occured";
                    break;
                case LogType.WARNING:
                    log += " : warning";
                    break;
                case LogType.WRITE:
                    log += " : read and write data";
                    break;
                case LogType.SUBSCRIBE:
                    log += " : subscribe";
                    break;
                default:
                    log += " : another logs";
                    break;
            }

            log += (msg!=""?" : ":"") + msg;

            sw.WriteLine(log);
        }
    }
}
