using System.Collections.ObjectModel;

namespace Octoshift.Models
{
    public abstract class GraphqlResult<T>
    {
        public T Data { get; init; }
        public Collection<ErrorData> Errors { get; init; }
    }

    public class ErrorData
    {
        public string Type { get; set; }
        public Collection<string> Path { get; init; }
        public Collection<Location> Locations { get; init; }
        public string Message { get; set; }
    }

    public class Location
    {
        public long Line { get; init; }
        public long Column { get; init; }
    }

}
