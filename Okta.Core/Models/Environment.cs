using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Okta.Core.Models
{
    [Serializable]
    public class Environment
    {
        public string Id { get; set; }
        public string UserName__c { get; set; }
        public string VirtualMachineName__c { get; set; }
        public string Status__c { get; set; }
    }
}
