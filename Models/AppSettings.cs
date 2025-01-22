using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models;

 public class AppSettings
{
    public GPTApiKeys ApiKeys { get; set; }
}

public class GPTApiKeys
{
    public string Key1 { get; set; }
    public string Key2 { get; set; }
}


