using System;

namespace OctoshiftCLI.Services;

public class DateTimeProvider
{
    public virtual long CurrentUnixTimeSeconds() => DateTimeOffset.Now.ToUnixTimeSeconds();
}
