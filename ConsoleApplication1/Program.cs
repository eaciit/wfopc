using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;
using TitaniumAS.Opc.Client.Common;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace EACIIT.OPCAgent
{

    public class OPCAgentConfig
    {
        public string Host;
        public string OPCName;
        public Int32 Period;
        public string AssetFolder;
    }
    
    class Program
    {
        static OPCAgentConfig cfg;

        static void Main(string[] args)
        {
            cfg = new OPCAgentConfig();
            cfg.Host = ConfigurationManager.AppSettings["host"];
            cfg.OPCName = ConfigurationManager.AppSettings["opcname"];
            cfg.Period = Convert.ToInt32(ConfigurationManager.AppSettings["period"]);
            cfg.AssetFolder = ConfigurationManager.AppSettings["assetfolder"];

            var server = new OpcDaServer(UrlBuilder.Build(cfg.OPCName, cfg.Host));
            server.Connect();
            BuildSubcribtion(server, cfg);
            System.Threading.Thread.Sleep(cfg.Period * 10);
            server.Disconnect();
            Console.WriteLine("Press any key ....");
            Console.ReadLine();
        }

        public static List<OpcDaGroup> BuildSubcribtion(OpcDaServer server, OPCAgentConfig cfg)
        {
            List<OpcDaGroup> groups = new List<OpcDaGroup>();
            try
            {
                string objectFilePath = cfg.AssetFolder + "\\objects.txt";
                string tagFilePath = cfg.AssetFolder + "\\tags.txt";
                string objLine, tagLine;

                List<String> objs = new List<string>();

                System.IO.StreamReader fileObj = new System.IO.StreamReader(objectFilePath);
                while ((objLine = fileObj.ReadLine()) != null)
                {
                    objLine = objLine.Trim();
                    if (!objLine.StartsWith("//"))
                        objs.Add(objLine);
                }
                fileObj.Close();

                List<String> tags = new List<string>();
                System.IO.StreamReader fileTag = new System.IO.StreamReader(tagFilePath);
                while ((tagLine = fileTag.ReadLine()) != null)
                {
                    tagLine = tagLine.Trim();
                    if (!tagLine.StartsWith("//"))
                        tags.Add(tagLine);
                }
                fileTag.Close();

                foreach (String obj in objs)
                {
                    List<string> objtags = new List<string>();
                    foreach (string tag in tags)
                    {
                        String objTag = String.Concat(obj, ".", tag);
                        objtags.Add(objTag);
                    }
                    OpcDaGroup g = Subscribe(obj, server, objtags.ToArray());
                    Console.WriteLine("Subscribe for " + obj);
                }
            }catch(Exception exc)
            {
                Console.WriteLine("Error: " + exc.Message);
            }

            return groups;
        }

        public static OpcDaGroup Subscribe(String groupname, OpcDaServer server, String[] tags)
        {
            var group = server.AddGroup(groupname);
            group.IsActive = true;
            group.UpdateRate = TimeSpan.FromMilliseconds(cfg.Period);
            group.ValuesChanged += Group_ValuesChanged;

            List<OpcDaItemDefinition> defs = new List<OpcDaItemDefinition>();
            foreach(String tag in tags)
            {
                var tagdef = new OpcDaItemDefinition { ItemId = tag, IsActive = true };
                defs.Add(tagdef);
            }

            var results = group.AddItems(defs.ToArray());
            int idx = 0;
            foreach(OpcDaItemResult res in results)
            {
                if (res.Error.Failed) {
                    Console.WriteLine("Error adding item {0}: {1}", defs[idx].ItemId,  res.Error);
                        }
                idx++;
            }

            return group;
        }

        private static void Group_ValuesChanged(object sender, OpcDaItemValuesChangedEventArgs e)
        {
            string outpath = System.Configuration.ConfigurationManager.AppSettings["datapath"];
            outpath = Path.Combine(outpath, DateTime.Today.ToString("yyyyMM"), DateTime.Today.ToString("dd"));

            DirectoryInfo di = new DirectoryInfo(outpath);
            if (!di.Exists)
            {
                di.Create();
            }

            StreamWriter sw = null;
            string tdir = "data";
            string fname = String.Format("data_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmmss_ffffff"));
            string fTarget = Path.Combine(di.FullName, fname);
            try
            {
                using (sw = File.AppendText(fTarget))
                {
                    foreach (var v in e.Values)
                    {
                        string data = String.Format("{2}|{0}|{1}", v.Timestamp, v.Item.ItemId, v.Value);
                        sw.WriteLine(data);
                    }
                }
            }
            catch(Exception em)
            {
                Console.WriteLine(em.Message);
            }
        }
    }
}
