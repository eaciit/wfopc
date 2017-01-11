using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace OPCService
{
    public partial class Service1 : ServiceBase
    {
        private OPCReader ord;
        public Service1()
        {
            InitializeComponent();
            ord = new OPCReader();
        }

        protected override void OnStart(string[] args)
        {
            Log.Write("Starting services", LogType.START);
            ord.Init();
        }

        protected override void OnStop()
        {
            Log.Write("Stopping services", LogType.STOP);
            try
            {
                ord.Disconnect();
            }   
            catch { }
        }
    }
}
