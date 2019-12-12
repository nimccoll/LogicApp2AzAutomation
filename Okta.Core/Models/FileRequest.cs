using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Okta.Core.Models
{
    [Serializable]
    public class FileRequest
    {
        public string Id { get; set; }
        public string FileName__c { get; set; }
        public string UserName__c { get; set; }
        public string Status__c { get; set; }
        public string FileUrl__c { get; set; }
    }
}
