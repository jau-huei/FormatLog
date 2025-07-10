namespace FormatLog
{
    public class LogFlushExceptionInfo
    {
        public string ExceptionMessage { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public List<Log> Logs { get; set; } = new();
    }
}