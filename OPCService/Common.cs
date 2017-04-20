using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OPCService
{
    public class Common
    {
        public static string GetConfig(string key)
        {
            var config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            var retval = config.AppSettings.Settings[key].Value;
            if (String.IsNullOrEmpty(retval))
            {
                retval = "";
            }

            return retval;
        }
    }
}
