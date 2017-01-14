using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TitaniumAS.Opc.Client.Common;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace OPCService
{
    public class OPCAgentConfig
    {
        public string Host;
        public string OPCName;
        public Int32 Period;
        public string AssetFolder;
    }

    public class OPCReader
    {
        protected OPCAgentConfig cfg;
        protected OpcDaServer server;
        protected List<string> ErrorTags;

        public void Init()
        {
            cfg = new OPCAgentConfig();
            cfg.Host = Common.GetConfig("host");
            cfg.OPCName = Common.GetConfig("opcname");
            cfg.Period = Convert.ToInt32(Common.GetConfig("period"));
            cfg.AssetFolder = Common.GetConfig("assetfolder");

            ErrorTags = new List<string>();

            if (!String.IsNullOrEmpty(cfg.Host))
            {
                //Log.Write(cfg.OPCName + ":" + cfg.Host, LogType.WRITE);
                server = new OpcDaServer(UrlBuilder.Build(cfg.OPCName, cfg.Host));
                server.Connect();
                BuildSubcribtion(server, cfg);
                //System.Threading.Thread.Sleep(cfg.Period * 10);
            }
            else
            {
                Log.Write("Cannot find host address.", LogType.ERROR);
            }
        }

        public void Disconnect()
        {
            if (server != null)
            {
                server.Disconnect();
            }
        }

        protected List<OpcDaGroup> BuildSubcribtion(OpcDaServer server, OPCAgentConfig cfg)
        {
            List<OpcDaGroup> groups = new List<OpcDaGroup>();
            try
            {
                string objectFilePath = cfg.AssetFolder + "\\objects.txt";
                string tagFilePath = cfg.AssetFolder + "\\tags.txt";
                string tagErrorFilePath = cfg.AssetFolder + "\\tags-error.txt";
                string objLine, tagLine, tagErrorLine;

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

                System.IO.StreamReader fileTagError = new System.IO.StreamReader(tagErrorFilePath);
                while ((tagErrorLine = fileTagError.ReadLine()) != null)
                {
                    tagErrorLine = tagErrorLine.Trim();
                    if (!tagErrorLine.StartsWith("//"))
                    {
                        tags.Add(tagErrorLine);
                        ErrorTags.Add(tagErrorLine);
                    }
                }
                fileTagError.Close();

                foreach (String obj in objs)
                {
                    List<string> objtags = new List<string>();
                    foreach (string tag in tags)
                    {
                        String objTag = String.Concat(obj, ".", tag);
                        objtags.Add(objTag);
                    }
                    OpcDaGroup g = Subscribe(obj, server, objtags.ToArray());
                    string msg = "Subscribe for " + obj;
                    //Log.Write(msg, LogType.SUBSCRIBE);
                }
            }
            catch (Exception exc)
            {
                string err = "Error: " + exc.Message;
                Log.Write(err, LogType.ERROR);
            }

            return groups;
        }

        protected OpcDaGroup Subscribe(String groupname, OpcDaServer server, String[] tags)
        {
            var group = server.AddGroup(groupname);
            group.IsActive = true;
            group.UpdateRate = TimeSpan.FromMilliseconds(cfg.Period);
            group.ValuesChanged += Group_ValuesChanged;

            List<OpcDaItemDefinition> defs = new List<OpcDaItemDefinition>();
            foreach (String tag in tags)
            {
                var tagdef = new OpcDaItemDefinition { ItemId = tag, IsActive = true };
                defs.Add(tagdef);
            }

            var results = group.AddItems(defs.ToArray());
            int idx = 0;
            foreach (OpcDaItemResult res in results)
            {
                if (res.Error.Failed)
                {
                    string err = String.Format("Error adding item {0}: {1}", defs[idx].ItemId, res.Error);
                    Log.Write(err, LogType.ERROR);
                }
                idx++;
            }

            return group;
        }

        protected void Group_ValuesChanged(object sender, OpcDaItemValuesChangedEventArgs e)
        {
            foreach (var v in e.Values)
            {
                bool isError = false;
                if (ErrorTags.Count > 0)
                {
                    foreach (string err in ErrorTags)
                    {
                        if (v.Item.ItemId.ToUpper().Trim().Contains(err.ToUpper().Trim()))
                        {
                            isError = true;
                            break;
                        }
                    }
                }

                //Log.Write(String.Format("Getting value for {0} at {1}", v.Item.ItemId, v.Timestamp), LogType.WRITE);
                string data = String.Format("{2}|{0}|{1}", v.Timestamp, v.Item.ItemId, v.Value);
                Result.Output(data, v.Timestamp, isError);
            }
        }
    }
}
