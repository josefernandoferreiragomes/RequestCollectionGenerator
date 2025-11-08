using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RequestCollectionGenerator.Models;

public class ApiLogEntry
{
    public DateTime TimeStamp { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
    public int ResultCode { get; set; }
    public string RequestedUrl { get; set; } = string.Empty;
    public string HeaderContent { get; set; } = string.Empty;
    public string BodyContent { get; set; } = string.Empty;
}
