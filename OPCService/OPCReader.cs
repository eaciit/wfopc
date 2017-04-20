using System;
using System.Collections.Concurrent; 
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using TitaniumAS.Opc.Client.Common;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;
using System.Net;
using System.Net.Http.Headers;
using System.IO;

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
                server = new OpcDaServer(UrlBuilder.Build(cfg.OPCName, cfg.Host));
                server.Connect();
                BuildSubcribtion(server, cfg);
            }
            else
            {
                Log.Write("Cannot find host address.", LogType.ERROR);
            }
        }

        bool IsErrorTags(string tag)
        {
            bool isError = false;
            string[] errorTags = new string[] { "ALARMCODE", "TURBINESTATE", "RETURNHEARTBEAT" };
            foreach (string item in errorTags)
            {
                if (tag.Trim().ToUpper().Contains(item))
                {
                    isError = true;
                    break;
                }
            }

            return isError;
        }

        public void Disconnect()
        {
            if (server != null)
            {
                //try
                //{   
                //    worker.CancelAsync();
                //    worker.Dispose();
                //}
                //catch { }
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
                    if (!res.Error.ToString().Contains("The item ID is not defined in the server address space"))
                    {
                        string err = String.Format("Error adding item {0}: {1}", defs[idx].ItemId, res.Error);
                        Log.Write(err, LogType.WARNING);
                    }
                }
                idx++;
            }

            return group;
        }

        protected void Group_ValuesChanged(object sender, OpcDaItemValuesChangedEventArgs e)
        {
            OpcDaItemValue[] values = e.Values;

            if (values.Length > 0)
            {
                {
                    // send data to the server
                    string currTs = values[0].Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    PrepareSendData(values, currTs);

                    // send data as file in local disk
                    WriteToFile(values);
                }
            }
        }

        void WriteToFile(OpcDaItemValue[] values)
        {
            StringBuilder dataValue = new StringBuilder(),
                          errorValue = new StringBuilder();
            string dataDelim = "",
                   errorDelim = "",
                   ts = "";
            DateTimeOffset currTimeStamp = DateTime.Now;
            if (values.Length > 0)
            {
                foreach (var v in values)
                {
                    string groupName = v.Item.Group.Name;
                    string data = String.Format("{0},{1},{2}", v.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), v.Item.ItemId, Convert.ToDouble(v.Value));
                    currTimeStamp = v.Timestamp;

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

                    if (isError)
                    {
                        errorValue.AppendFormat("{0}{1}", errorDelim, data);
                        errorDelim = "\n";
                    }
                    else
                    {
                        dataValue.AppendFormat("{0}{1}", dataDelim, data);
                        dataDelim = "\n";
                    }
                }

                string outpath = Common.GetConfig("datapath");
                outpath = Path.Combine(outpath, currTimeStamp.ToString("yyyyMM"), currTimeStamp.ToString("dd"), currTimeStamp.ToString("HH"));
                ts = currTimeStamp.ToString("yyyyMMdd_HHmmss_ffff");
                string tsErr = currTimeStamp.ToString("yyyyMMdd");

                string fdata = String.Format("data_{0}.csv", ts);
                string ferror = String.Format("alert_{0}.csv", tsErr);

                DirectoryInfo di = new DirectoryInfo(outpath);
                if (!di.Exists)
                {
                    di.Create();
                }

                if (dataValue.Length > 0)
                {
                    Result.Output(Path.Combine(di.FullName, fdata), dataValue.ToString());
                }

                if (errorValue.Length > 0)
                {
                    Result.Output(Path.Combine(di.FullName, ferror), errorValue.ToString());
                }
            }
        }

        void PrepareSendData(OpcDaItemValue[] values, string currTimeStamp)
        {
            try
            {
                if (values.Length > 0)
                {
                    var listValues = new List<ValueHelper>();
                    foreach (var v in values)
                    {
                        if (!String.IsNullOrEmpty(v.Item.ItemId.Trim()))
                        {
                            string[] items = v.Item.ItemId.Split('.');
                            if (items.Length > 3)
                            {
                                string turbine = items[2];
                                string tag = items[4];

                                string sTimestamp = v.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                                string sValue = (v.Value == null ? "-999999.00" : Convert.ToString(v.Value));

                                listValues.Add(new ValueHelper()
                                {
                                    TimeStamp = sTimestamp,
                                    Turbine = turbine,
                                    Tag = tag,
                                    Value = Convert.ToDouble(sValue)
                                });
                            }
                        }
                    }

                    if (listValues.Count > 0)
                    {
                        JsonSerializerSettings set = new JsonSerializerSettings();
                        string data = JsonConvert.SerializeObject(listValues, Formatting.None, set);
                        SendData(data);
                    }
                }
            }
            catch (Exception em)
            {
                Log.Write("Error exception send data values : " + em.Message + " :: " + em.StackTrace.ToString(), LogType.ERROR);
            }
        }

        async void SendData(string data)
        {
            string url = "http://orangewfm-dev.eaciitapp.com/hfd/";
            //string url = "http://ostrowfm-realtime.eaciitapp.com/hfd/";
            //string url = "http://localhost:8018/hfd/";

            string targetDir = "pushdata";
            string fixUrl = url + targetDir;

            try
            {
                // push data to server
                using (var client = new HttpClient())
                {
                    var request = new Dictionary<string, string>
                        {
                            { "Data", data },
                        };

                    var content = new FormUrlEncodedContent(request);
                    var response = await client.PostAsync(fixUrl, content);
                }
            }
            catch (Exception em)
            {
                Log.Write("Error sending data : " + em.Message + "::" + em.StackTrace.ToString(), LogType.ERROR);
            }
        }

        internal class ValueHelper {
            public string TimeStamp { get; set; }
            public string Turbine { get; set; }            
            public string Tag { get; set; }
            public double Value { get; set; }
        }
    }
}
